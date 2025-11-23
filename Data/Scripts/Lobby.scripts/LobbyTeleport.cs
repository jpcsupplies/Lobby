using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.ModAPI;
using VRage.Game;
using ProtoBuf;   // ← for packet serialization
using System.Linq;


namespace Lobby.scripts     // ← this is the correct namespace from your project
{
    // All teleport-related code will live here.
    // This class is 100% shared – compiled on both client and server.
    public static class LobbyTeleport
    {
        // --------------------------------------------------------------------
        // Message IDs – must be unique across the entire mod
        // --------------------------------------------------------------------
        private const ushort TELEPORT_REQUEST_HOP = 18410;
        private const ushort TELEPORT_EXECUTE = 18413;

        private static System.IO.TextWriter logWriter = null;
        private const string LOG_FILE = "LobbyTeleport.log";

        private static bool registered = false;
        private const ushort TELEPORT_ABSOLUTE_REQUEST = 18415;
       
        [ProtoContract]
        private struct AbsoluteTeleportRequestPacket
        {
            [ProtoMember(1)] public long IdentityId;
            [ProtoMember(2)] public Vector3D TargetPos;
        }
       
        [ProtoContract]
        private struct HopRequestPacket
        {
            [ProtoMember(1)] public long IdentityId;
            [ProtoMember(2)] public double DistanceMetres;
        }

        [ProtoContract]
        private struct HopExecutePacket
        {
            [ProtoMember(1)] public long GridEntityId;     // 0 = jetpack
            [ProtoMember(2)] public Vector3D TargetPosition;
        }

        [ProtoContract]
        private struct HopFeedbackPacket
        {
            [ProtoMember(1)] public string Message;
        }
        // --------------------------------------------------------------------
        // Public entry points (these are what /hop or jump buttons will call)
        // --------------------------------------------------------------------
        /// <summary>
        /// Request a "hop" forward X metres for the calling player (and their grid if piloting).
        /// Works offline instantly, on dedicated server uses full preload + sync process.
        /// </summary>
        public static void RequestHop(long playerIdentityId, double distanceMetres)
        {
            IMyPlayer player = null;
            var list = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(list);
            foreach (var p in list)
            {
                if (p.IdentityId == playerIdentityId)
                {
                    player = p;
                    break;
                }
            }

            if (player == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", "Error: Player not found");
                return;
            }

            // Get the entity the player is actually controlling (cockpit, remote, character, etc.)
            var controlledEntity = player.Controller?.ControlledEntity;
            if (controlledEntity == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", "Error: Nothing controlled");
                return;
            }

            // Determine forward direction from whatever the player is controlling
            Vector3D forward = controlledEntity.Entity.WorldMatrix.Forward;
            Vector3D currentPos = controlledEntity.Entity.GetPosition();
            Vector3D targetPos = currentPos + forward * distanceMetres;

            // OFFLINE / SELF-HOSTED: instant move
            if (!MyAPIGateway.Multiplayer.MultiplayerActive || MyAPIGateway.Session.IsServer)
            {
                IMyCubeGrid grid = controlledEntity.Entity as IMyCubeGrid;               // direct grid (e.g. remote control on small ship with 1 block)
                if (grid == null)
                {
                    // Might be sitting in a cockpit/block → get parent grid
                    var cubeBlock = controlledEntity.Entity as IMyCubeBlock;
                    if (cubeBlock != null)
                        grid = cubeBlock.CubeGrid;
                }

                if (grid != null)
                {
                    InstantHopGrid(grid, targetPos);
                    MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", $"Grid hopped {distanceMetres:F0}m forward");
                }
                else
                {
                    // Jetpack / free floating
                    controlledEntity.Entity.PositionComp.SetPosition(targetPos);
                    MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", $"Hop {distanceMetres:F0}m forward (jetpack)");
                }
                return;
            }

            // Dedicated server → send secure request packet
            var packet = new HopRequestPacket
            {
                IdentityId = playerIdentityId,
                DistanceMetres = distanceMetres
            };

            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(packet);
            SendMessageToServer(TELEPORT_REQUEST_HOP, data);

            MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", $"Hop {distanceMetres:F0}m requested from server...");
        }

        // Instant grid hop – works 100% offline and on self-hosted (and will be reused on server later)
        private static void InstantHopGrid(IMyCubeGrid mainGrid, Vector3D targetWorldPosition)
        {
            if (mainGrid?.Physics == null) return;

            var grids = MyAPIGateway.GridGroups.GetGroup(mainGrid, GridLinkTypeEnum.Physical);
            var currentMainPos = mainGrid.GetPosition();
            var offset = targetWorldPosition - currentMainPos;

            // Preload destination area (helps client streaming + prevents physics glitches)
            var preloadBox = mainGrid.PositionComp.WorldAABB;
            preloadBox.Translate(offset);
            MyAPIGateway.Physics.EnsurePhysicsSpace(preloadBox);

            foreach (var g in grids)
            {
                if (g?.PositionComp == null) continue;

                // Move to exact world position
                g.PositionComp.SetPosition(g.GetPosition() + offset);

                // CRITICAL: Zero velocity & angular velocity – eliminates spin/prediction glitches
                if (g.Physics != null)
                {
                    g.Physics.LinearVelocity = Vector3D.Zero;
                    g.Physics.AngularVelocity = Vector3D.Zero;
                }
            }

            Log($"InstantHopGrid: {grids.Count} grid(s) moved to {targetWorldPosition.X:F0}, {targetWorldPosition.Y:F0}, {targetWorldPosition.Z:F0}");
        }

        /// <summary>
        /// Request absolute teleport to exact world position (for /override, /depart, wormholes, etc.).
        /// Works offline instantly, dedicated server syncs perfectly.
        /// </summary>
        public static void RequestAbsoluteTeleport(long playerIdentityId, Vector3D targetWorldPos)
        {
            var player = GetPlayerByIdentityId(playerIdentityId);
            if (player == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", "Error: Player not found");
                return;
            }

            // Offline / self-hosted → instant
            if (!MyAPIGateway.Multiplayer.MultiplayerActive || MyAPIGateway.Session.IsServer)
            {
                ExecuteAbsoluteTeleport(player, targetWorldPos);
                return;
            }

            // Dedicated → send to server
            var packet = new AbsoluteTeleportRequestPacket
            {
                IdentityId = playerIdentityId,
                TargetPos = targetWorldPos
            };
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(packet);
            SendMessageToServer(TELEPORT_ABSOLUTE_REQUEST, data);

            MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", $"Jump requested → {targetWorldPos.X:F0}, {targetWorldPos.Y:F0}, {targetWorldPos.Z:F0}");
        }

        private static void ExecuteAbsoluteTeleport(IMyPlayer player, Vector3D targetPos)
        {
            if (player?.Controller?.ControlledEntity?.Entity == null) return;

            var controlled = player.Controller.ControlledEntity.Entity;

            IMyCubeGrid grid = controlled as IMyCubeGrid;
            if (grid == null)
            {
                var block = controlled as IMyCubeBlock;
                if (block != null) grid = block.CubeGrid;
            }

            if (grid != null)
            {
                InstantHopGrid(grid, targetPos);
            }
            else if (controlled != null)
            {
                controlled.PositionComp.SetPosition(targetPos);
            }
        }

        private static void HandleAbsoluteTeleportRequest(ushort handlerId, byte[] data, ulong senderSteamId, bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer) return;

            var packet = MyAPIGateway.Utilities.SerializeFromBinary<AbsoluteTeleportRequestPacket>(data);

            if (MyAPIGateway.Session.GetUserPromoteLevel(senderSteamId) < MyPromoteLevel.SpaceMaster)
            {
                Log("Absolute teleport denied – not SpaceMaster");
                return;
            }

            var player = GetPlayerByIdentityId(packet.IdentityId);
            if (player?.Controller?.ControlledEntity?.Entity == null)
            {
                Log("Absolute teleport: player not found or not controlling anything");
                return;
            }

            // Preload
            BoundingBoxD box = new BoundingBoxD(packet.TargetPos - 5000, packet.TargetPos + 5000);
            MyAPIGateway.Physics.EnsurePhysicsSpace(box);

            // Execute on server (syncs to clients)
            ExecuteAbsoluteTeleport(player, packet.TargetPos);

            // Feedback
            SendFeedback(senderSteamId, "Absolute teleport complete");

            Log($"Absolute teleport complete for {senderSteamId} to {packet.TargetPos.X:F0}, {packet.TargetPos.Y:F0}, {packet.TargetPos.Z:F0}");
        }

        // --------------------------------------------------------------------
        // Internal networking setup – called once from Lobby.cs AND LobbyServer.cs
        // --------------------------------------------------------------------
        public static void InitNetworking()
        {
            if (registered || MyAPIGateway.Multiplayer == null) return;

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(TELEPORT_REQUEST_HOP, HandleHopRequest);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(TELEPORT_EXECUTE, HandleExecuteTeleport);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(TELEPORT_ABSOLUTE_REQUEST, HandleAbsoluteTeleportRequest);
            registered = true;
            // Open log file for server-side debug
            if (MyAPIGateway.Session.IsServer)
            {
                try
                {
                    logWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(LOG_FILE, typeof(LobbyTeleport));
                    Log("=== LobbyTeleport logging started ===");
                }
                catch { }
            }
        }

        public static void UnloadNetworking()
        {
            if (!registered || MyAPIGateway.Multiplayer == null) return;

            // Unregister uses the old simple delegate – pass null (safe and standard)
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(TELEPORT_REQUEST_HOP, null);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(TELEPORT_EXECUTE, null);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(TELEPORT_ABSOLUTE_REQUEST, null);

            // Close log file
            if (logWriter != null)
            {
                try { logWriter.Close(); } catch { }
                logWriter = null;
            }
            registered = false;
        }

        private static void HandleHopRequest(ushort handlerId, byte[] data, ulong senderSteamId, bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer) return;

            var packet = MyAPIGateway.Utilities.SerializeFromBinary<HopRequestPacket>(data);

            if (MyAPIGateway.Session.GetUserPromoteLevel(senderSteamId) < MyPromoteLevel.SpaceMaster)
            {
                Log("DENIED – not SpaceMaster");
                SendFeedback(senderSteamId, "Hop denied: insufficient permissions");
                return;
            }

            var player = GetPlayerByIdentityId(packet.IdentityId);
            if (player?.Controller?.ControlledEntity == null)
            {
                Log("ERROR: Player not found or not controlling anything");
                return;
            }

            Vector3D forward = player.Controller.ControlledEntity.Entity.WorldMatrix.Forward;
            Vector3D targetPos = player.GetPosition() + forward * packet.DistanceMetres;

            // Preload on server
            BoundingBoxD box = new BoundingBoxD(targetPos - 5000, targetPos + 5000);
            MyAPIGateway.Physics.EnsurePhysicsSpace(box);

            // Move on server (syncs to all clients automatically)
            ExecuteHopLocal(player, targetPos);

            // Feedback to sender only this code seems to be running twice?
            //SendFeedback(senderSteamId, $"Hop complete: {packet.DistanceMetres:F0}m forward");

            Log($"Hop complete for {senderSteamId}: {targetPos.X:F0}, {targetPos.Y:F0}, {targetPos.Z:F0}");
        }

        private static void HandleHopRequestDebug(ushort handlerId, byte[] data, ulong senderSteamId, bool sendToOthers)
        {
            //this is an older alternate method to do hops kept for reference as it works too
            if (!MyAPIGateway.Session.IsServer) return;

            var packet = MyAPIGateway.Utilities.SerializeFromBinary<HopRequestPacket>(data);
            Log($"Hop request from {senderSteamId}: {packet.DistanceMetres:F0}m");

            if (MyAPIGateway.Session.GetUserPromoteLevel(senderSteamId) < MyPromoteLevel.SpaceMaster)
            {
                Log("LobbyTeleport: Denied – not SpaceMaster");
                SendFeedback(senderSteamId, "Hop denied: insufficient permissions");
                return;
            }

            // Find player
            IMyPlayer player = null;
            var list = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(list);
            foreach (var p in list)
            {
                if (p.IdentityId == packet.IdentityId && p.SteamUserId == senderSteamId)
                {
                    player = p;
                    break;
                }
            }

            if (player?.Controller?.ControlledEntity?.Entity == null)
            {
                Log("Player not found or not controlling anything");
                SendFeedback(senderSteamId, "Hop failed: not controlling a grid/character");
                return;
            }

            Vector3D forward = player.Controller.ControlledEntity.Entity.WorldMatrix.Forward;
            Vector3D targetPos = player.GetPosition() + forward * packet.DistanceMetres;

            Log($"Target calculated: {targetPos.X:F0}, {targetPos.Y:F0}, {targetPos.Z:F0}");
            SendFeedback(senderSteamId, $"Hop approved – jumping {packet.DistanceMetres:F0}m forward");

            // Execute on server
            ExecuteHopLocal(player, targetPos);

            // Send to client
            var exec = new HopExecutePacket
            {
                GridEntityId = GetGridId(player),
                TargetPosition = targetPos
            };
            byte[] execData = MyAPIGateway.Utilities.SerializeToBinary(exec);
            SendMessageToClient(senderSteamId, TELEPORT_EXECUTE, execData);
        }

        // Executes the actual movement on the current machine (server or offline client)
        private static void ExecuteHopLocal(IMyPlayer player, Vector3D targetPos)
        {
            if (player?.Controller?.ControlledEntity?.Entity == null)
                return;

            var controlled = player.Controller.ControlledEntity.Entity;

            // Try to get grid
            IMyCubeGrid grid = controlled as IMyCubeGrid;
            if (grid == null)
            {
                var block = controlled as IMyCubeBlock;
                if (block != null)
                    grid = block.CubeGrid;
            }

            if (grid != null)
            {
                // Move entire grid + subgrids
                InstantHopGrid(grid, targetPos);
            }
            else if (controlled != null)
            {
                // Jetpack / free character
                controlled.PositionComp.SetPosition(targetPos);
            }
        }

        private static void HandleExecuteTeleport(ushort handlerId, byte[] data, ulong senderSteamId, bool sendToOthers)
        {
            // First: try to read as feedback packet (string message from server)
            try
            {
                var fb = MyAPIGateway.Utilities.SerializeFromBinary<HopFeedbackPacket>(data);
                MyAPIGateway.Utilities.ShowMessage("LobbyTeleport", fb.Message);
                return;
            }
            catch { } // not feedback → continue
            /*  disable to debug double hop glitch
            // Second: it's a movement packet
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<HopExecutePacket>(data);

            if (packet.GridEntityId == 0)
            {
                // Jetpack
                var list = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(list);
                var player = list.FirstOrDefault(p => p.SteamUserId == senderSteamId);
                if (player?.Character != null)
                    player.Character.PositionComp.SetPosition(packet.TargetPosition);
            }
            else
            {
                // Grid
                var grid = MyAPIGateway.Entities.GetEntityById(packet.GridEntityId) as IMyCubeGrid;
                if (grid != null)
                    InstantHopGrid(grid, packet.TargetPosition);
            } */
        }

        // --------------------------------------------------------------------
        // Helper methods that will be shared
        // --------------------------------------------------------------------
        private static void SendMessageToServer(ushort messageId, byte[] data)
        {
            MyAPIGateway.Multiplayer.SendMessageToServer(
                messageId,
                data,
                true); // true = secure
        }

        private static void SendMessageToClient(ulong steamId, ushort messageId, byte[] data)
        {
            MyAPIGateway.Multiplayer.SendMessageTo(
                messageId,
                data,
                steamId,
                true);
        }

        private static IMyPlayer GetPlayerByIdentityId(long identityId)
        {
            var list = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(list);
            foreach (var p in list)
            {
                if (p.IdentityId == identityId) return p;
            }
            return null;
        }

        // Sends a visible chat message back to the requesting client
        private static void SendFeedback(ulong steamId, string message)
        {
            var fb = new HopFeedbackPacket { Message = $"[Server] {message}" };
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(fb);
            SendMessageToClient(steamId, TELEPORT_EXECUTE, data); // reuse the same channel – client will show it
        }

        // Returns the grid EntityId (or 0 if jetpack)
        private static long GetGridId(IMyPlayer player)
        {
            if (player?.Controller?.ControlledEntity?.Entity == null)
                return 0;

            var block = player.Controller.ControlledEntity.Entity as IMyCubeBlock;
            if (block != null)
                return block.CubeGrid.EntityId;

            var grid = player.Controller.ControlledEntity.Entity as IMyCubeGrid;
            return grid?.EntityId ?? 0;
        }

        private static void Log(string message)
        {
            if (logWriter != null)
            {
                try
                {
                    logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                    logWriter.Flush();
                }
                catch { }
            }
        }
    }
}