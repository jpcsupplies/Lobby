using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI; // for IMyEntity

namespace Lobby.scripts
{
    public static class LobbyPhysics
    {
        // Message IDs (unique, no overlap with LobbyTeleport)
        private const ushort PHYSICS_VELOCITY_ADD = 18420;
        private const ushort PHYSICS_ROTATION_APPLY = 18421;

        private static bool registered = false;

        // --------------------------------------------------------------------
        // Public placeholders (called from commands/hazards later)
        // --------------------------------------------------------------------
        public static void AddVelocity(long playerIdentityId, Vector3D direction, float amount)
        {
            // PLACEHOLDER – shows message for testing
            MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"AddVelocity placeholder: {amount}m/s in dir {direction}");
        }

        /// <summary>
        /// Apply realistic post-jump stagger: strong side roll (left/right) + light secondary wobble
        /// Used by /override and debug /phys stagger
        /// </summary>
        public static void ShipStagger(long playerIdentityId)
        {
            var player = GetPlayerByIdentityId(playerIdentityId);
            if (player == null) return;

            var grid = GetControlledGrid(player);
            if (grid == null || grid.Physics == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", "No grid to stagger");
                return;
            }

            Random rand = new Random();

            // 1. Strong side roll (left or right) – 0 to 25 deg/s
            bool rollLeft = rand.Next(2) == 0;
            Vector3D rollAxis = rollLeft ? grid.WorldMatrix.Left : grid.WorldMatrix.Right;
            //float rollDeg = (float)rand.NextDouble() * 25f;
            float rollDeg = 12f + (float)rand.NextDouble() * 13f; // 12.0 to 24.999

            // 2. Light secondary wobble (up/down/forward/backward) – 0 to 12 deg/s
            Vector3D[] secondaryAxes = { grid.WorldMatrix.Up, -grid.WorldMatrix.Up, grid.WorldMatrix.Forward, -grid.WorldMatrix.Forward };
            Vector3D secondaryAxis = secondaryAxes[rand.Next(secondaryAxes.Length)];
            float secondaryDeg = (float)rand.NextDouble() * 12f;

            // Combine into one angular velocity vector
            float toRad = 0.017453292f;
            Vector3D totalAngularVel = (Vector3D.Normalize(rollAxis) * rollDeg +
                                       Vector3D.Normalize(secondaryAxis) * secondaryDeg) * toRad;

            grid.Physics.AngularVelocity = totalAngularVel;

            //MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"Ship stagger: {rollDeg:F1}°/s roll + {secondaryDeg:F1}°/s wobble");
        }

        //general way to apply rotation for potential future effects
        public static void ApplyRotation(long playerIdentityId, Vector3D worldAxis, float degreesPerSecond)
        {
            var player = GetPlayerByIdentityId(playerIdentityId);
            if (player == null) return;

            var grid = GetControlledGrid(player);
            if (grid == null || grid.Physics == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", "No grid controlled");
                return;
            }

            float radPerSec = degreesPerSecond * 0.017453292f;
            Vector3D angularVel = worldAxis * radPerSec;

            grid.Physics.AngularVelocity = angularVel;

            MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"Grid reeling {degreesPerSecond:F1}°/s");
        }

        // --------------------------------------------------------------------
        // Gravity Well – simple pull zone (blackhole prototype)
        // --------------------------------------------------------------------
        private static readonly HashSet<long> ActiveGravityWells = new HashSet<long>();
        private static long nextWellId = 1;

        // --------------------------------------------------------------------
        // Gravity Well – simple one-shot pull (offline testable, no timer)
        // --------------------------------------------------------------------
        public static void CreateGravityWell(Vector3D center, float radius, float strength)
        {
            var entities = new HashSet<VRage.ModAPI.IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);

            int affected = 0;

            foreach (var entity in entities)
            {
                if (entity == null || entity.Physics == null || entity.MarkedForClose)
                    continue;

                Vector3D pos = entity.GetPosition();
                Vector3D dir = center - pos;
                double dist = dir.Length();

                if (dist >= radius || dist < 1)
                    continue;

                Vector3D pullDir = Vector3D.Normalize(dir);
                float pull = strength * (float)(1.0 - dist / radius);

                IMyCubeGrid grid = entity as IMyCubeGrid;
                if (grid != null && grid.Physics != null)
                {
                    grid.Physics.LinearVelocity += pullDir * pull * 0.1f;
                    affected++;
                    continue; // skip character check if it's a grid
                }

                // Character check (jetpack player)
                IMyCharacter character = entity as IMyCharacter;
                if (character != null && character.Physics != null)
                {
                    character.Physics.LinearVelocity += pullDir * pull * 0.1f;
                    affected++;
                }
            }

            MyAPIGateway.Utilities.ShowMessage("LobbyPhysics",
                $"Gravity pull applied – {affected} entities affected");
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

        private static IMyCubeGrid GetControlledGrid(IMyPlayer player)
        {
            var entity = player.Controller?.ControlledEntity?.Entity;
            var block = entity as IMyCubeBlock;
            if (block != null) return block.CubeGrid;
            return entity as IMyCubeGrid;
        }

        // --------------------------------------------------------------------
        // Networking placeholders (safe, empty handlers)
        // --------------------------------------------------------------------
        public static void InitNetworking()
        {
            if (registered || MyAPIGateway.Multiplayer == null) return;

            // Empty handlers for now – real logic later
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PHYSICS_VELOCITY_ADD,
                (ushort id, byte[] data, ulong sender, bool ignore) => { });

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PHYSICS_ROTATION_APPLY,
                (ushort id, byte[] data, ulong sender, bool ignore) => { });

            registered = true;
        }

        public static void UnloadNetworking()
        {
            if (!registered || MyAPIGateway.Multiplayer == null) return;

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PHYSICS_VELOCITY_ADD, null);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PHYSICS_ROTATION_APPLY, null);

            registered = false;
        }
    }
}