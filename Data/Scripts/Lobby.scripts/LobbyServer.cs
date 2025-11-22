
using System;
using System.Text;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.ObjectBuilders;
using Sandbox.Common.ObjectBuilders;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using System.Linq; // Added for Skip
using VRage.ModAPI; // Added for IMyEntity

namespace Lobby.scripts
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class LobbyServer : MySessionComponentBase
    {
        private const string CONFIG_FILE = "LobbyDestinations.cfg";
        private const ushort MESSAGE_ID = 12345;

        public override void BeforeStart()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            MyAPIGateway.Multiplayer.RegisterMessageHandler(MESSAGE_ID, HandleMessage);

            if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
            {
                SaveConfigText(LobbyScript.DefaultConfig);
                //[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
            }

            BroadcastConfig();
            LobbyTeleport.InitNetworking();
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MESSAGE_ID, HandleMessage);

            LobbyTeleport.UnloadNetworking();
            base.UnloadData();
        }

        private void HandleMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            if (message.StartsWith("RequestConfig"))
            {
                string configText = LoadConfigText();
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                bool isAdmin = MyAPIGateway.Session.GetUserPromoteLevel(steamId) >= MyPromoteLevel.SpaceMaster;
                if (!isAdmin)
                {
                    var lines = configText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    configText = "";
                    foreach (var line in lines)
                    {
                        if (!line.StartsWith("[ServerPasscode]"))
                        {
                            configText += line + "\n";
                        }
                    }
                }
                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigData:" + configText), steamId);
            }
            else if (message.StartsWith("SaveConfig:"))
            {
                var parts = message.Split(':');
                if (parts.Length < 3)
                    return;
                ulong steamId = ulong.Parse(parts[1]);
                if (MyAPIGateway.Session.GetUserPromoteLevel(steamId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), steamId);
                    return;
                }

                // Reconstruct text by joining all parts except SteamId and EntityId
                string text = string.Join(":", parts, 2, parts.Length - 3); // From index 2 to before EntityId
                long entityId = parts.Length > 3 ? long.Parse(parts[parts.Length - 1]) : 0;
                SaveConfigText(text);
                if (entityId != 0)
                {
                    IMyEntity grid = MyAPIGateway.Entities.GetEntityById(entityId);
                    if (grid != null && grid is IMyCubeGrid)
                    {
                        grid.Close();
                    }
                }
                BroadcastConfig();
            }
            else if (message.StartsWith("IsAdmin:"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                bool isAdmin = MyAPIGateway.Session.GetUserPromoteLevel(steamId) >= MyPromoteLevel.SpaceMaster;
                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes($"AdminStatus:{steamId}:{isAdmin}"), steamId);
            }
            else if (message.StartsWith("ltest reset"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                if (MyAPIGateway.Session.GetUserPromoteLevel(steamId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), steamId);
                    return;
                }
                try
                {
                    // Regenerate config with defaults
                    string defaultConfig = LoadConfigText(true); // Forces default regardless of file
                    SaveConfigText(defaultConfig);
                    BroadcastConfig();
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigReset:Defaults regenerated"), steamId);
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage("LobbyServer", $"Config reset failed: {e.Message}");
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigReset:Failed"), steamId);
                }
            }
            else if (message.StartsWith("RequestLed it:"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                if (MyAPIGateway.Session.GetUserPromoteLevel(steamId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), steamId);
                    return;
                }

                IMyPlayer player = null;
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players, p => p != null && p.SteamUserId == steamId);
                if (players.Count > 0)
                    player = players[0];
                if (player == null)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("Led itFailed"), steamId);
                    return;
                }

                var character = player.Character;
                if (character == null)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("Led itFailed"), steamId);
                    return;
                }

                Vector3D position = character.GetPosition() + character.WorldMatrix.Forward * 2 + character.WorldMatrix.Up * 0.5;
                Vector3D forward = -character.WorldMatrix.Forward;
                Vector3D up = character.WorldMatrix.Up;

                var gridBuilder = new MyObjectBuilder_CubeGrid()
                {
                    GridSizeEnum = MyCubeSize.Large,
                    IsStatic = true,
                    PersistentFlags = MyPersistentEntityFlags2.InScene,
                    PositionAndOrientation = new MyPositionAndOrientation(position, forward, up),
                    CubeBlocks = new List<MyObjectBuilder_CubeBlock>
                    {
                        new MyObjectBuilder_TextPanel()
                        {
                            EntityId = 0,
                            SubtypeName = "LargeLCDPanel",
                            Min = new Vector3I(0, 0, 0),
                            BlockOrientation = new MyBlockOrientation(Base6Directions.Direction.Backward, Base6Directions.Direction.Up)
                        }
                    }
                };
                var spawnedGrid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridBuilder) as IMyCubeGrid;
                if (spawnedGrid == null)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("Led itFailed"), steamId);
                    return;
                }

                var textPanel = spawnedGrid.GetCubeBlock(new Vector3I(0, 0, 0))?.FatBlock as IMyTextPanel;
                if (textPanel != null)
                {
                    textPanel.CustomName = "[config]";
                    textPanel.WriteText(LoadConfigText());
                    textPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                    textPanel.FontSize = 1f;
                    textPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
                }

                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("Led itSuccess"), steamId);
            }
            // Step 2: Add this handler (uncomment to test)
            else if (message.StartsWith("ApplyChange:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 4)
                {
                    string attributeType = parts[1];
                    ulong playerSteamId = ulong.Parse(parts[2]);
                    float amount = float.Parse(parts[3]);

                    var players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players, p => p.SteamUserId == playerSteamId);
                    var player = players.FirstOrDefault();

                    if (attributeType == "Radiation")
                    {
                        if (player != null && player.Character != null)
                        {
                            // var comp = player.Character.Components.Get<Sandbox.Game.Entities.Character.Components.MyCharacterStatComponent>();
                            var comp = player.Character.Components.Get<Sandbox.Game.Components.MyCharacterStatComponent>();
                            if (comp != null)
                            {
                                comp.Radiation.Value += amount;
                                // Optional log (remove for production)
                                // MyAPIGateway.Utilities.ShowMessage("LobbyServer", $"Debug: Server Radiation.Value += {amount:F2} for {player.DisplayName}, total: {comp.Radiation.Value:F2}");
                            }
                        }
                    }
                    else if (attributeType == "Damage")
                    {
                        // Placeholder for direct damage
                        // if (player != null && player.Character != null)
                        // {
                        //     var damageInfo = new MyDamageInformation
                        //     {
                        //         Amount = amount,
                        //         Type = MyDamageType.Generic
                        //     };
                        //     MyAPIGateway.Entities.DamageEntity(player.Character, damageInfo);
                        // }
                    }
                    // Add more else if for "Hunger", "Energy", "Hydrogen", "Oxygen" as placeholders
                    else if (attributeType == "Hunger")
                    {
                        // Placeholder: Future hunger logic
                    }
                    else if (attributeType == "Energy")
                    {
                        // Placeholder: Future energy logic
                    }
                    else if (attributeType == "Hydrogen")
                    {
                        // Placeholder: Future hydrogen logic
                    }
                    else if (attributeType == "Oxygen")
                    {
                        // Placeholder: Future oxygen logic
                    }
                }
            }
            //else 
            //Should be its own thing, so else cascade doesn't skip
            if (message.StartsWith("MoveGrid:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 6)
                {
                    ulong playerSteamId = ulong.Parse(parts[1]);
                    long gridEntityId = long.Parse(parts[2]);
                    double x = double.Parse(parts[3]);
                    double y = double.Parse(parts[4]);
                    double z = double.Parse(parts[5]);
                    bool movePlayerIfFree = bool.Parse(parts[6]);

                    if (MyAPIGateway.Session.GetUserPromoteLevel(playerSteamId) < MyPromoteLevel.SpaceMaster)
                    {
                        MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), playerSteamId);
                        return;
                    }

                    var grid = MyAPIGateway.Entities.GetEntityById(gridEntityId) as IMyCubeGrid;
                    if (grid != null)
                    {
                        ApplyMoveGrid(grid, x, y, z, movePlayerIfFree);
                        MyAPIGateway.Utilities.ShowMessage("LobbyServer", $"Debug: Server moved grid {gridEntityId} to {x:F0},{y:F0},{z:F0}, player free: {movePlayerIfFree}");
                    }
                }
            }            
        }

        private void ApplyMoveGrid(IMyCubeGrid mainGrid, double x, double y, double z, bool movePlayerIfFree = false)
        {
            if (mainGrid == null)
                return;

            // Save current positions and calculate new ones
            var entities = new List<IMyEntity> { mainGrid };
            var currentPositions = new List<Vector3D> { mainGrid.GetPosition() };
            var newPositions = new List<Vector3D> { new Vector3D(x, y, z) };

            // Add subgrids
            var subgrids = new HashSet<VRage.ModAPI.IMyEntity>();
            MyAPIGateway.Entities.GetEntities(subgrids, g => g is IMyCubeGrid && ((IMyCubeGrid)g).Parent == mainGrid);
            var subgridList = subgrids.OfType<IMyCubeGrid>().ToList();
            foreach (var subgrid in subgridList)
            {
                entities.Add(subgrid);
                currentPositions.Add(subgrid.GetPosition());
                Vector3D relativeOffset = subgrid.GetPosition() - mainGrid.GetPosition();
                newPositions.Add(newPositions[0] + relativeOffset);
            }

            // Aggregate bounding box for new positions (FTL sample)
            BoundingBoxD aggregateBox = new BoundingBoxD();
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity != null)
                {
                    var box = entity.PositionComp.WorldAABB;
                    box.Translate(newPositions[i] - currentPositions[i]);
                    aggregateBox.Include(box);
                }
            }

            // Ensure physics space at new location (FTL sample)
            MyAPIGateway.Physics.EnsurePhysicsSpace(aggregateBox);

            // Set new positions (FTL sample)
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity != null)
                {
                    entity.PositionComp.SetPosition(newPositions[i]);
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Updated {entity.EntityId} to {newPositions[i]:F0}");
                }
            }

            // Optional: Move player if free (not in grid)
            // this seems to be broken at the moment since our input field for target is coded only for a grid type
            // need a redesign once we confirm seated sync is working server side so the same module can move players or
            // grids just as easily to allow the whitehole/wormhole nav hazard to work
            if (movePlayerIfFree)
            {
                var player = MyAPIGateway.Session.Player;
                if (player != null && player.Character != null && player.Character.Parent == null)
                {
                    player.Character.SetPosition(newPositions[0]); // New main grid pos
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Moved free player to new pos");
                }
            }
        }

        //old move method only works offline or self hosted
        private void OldApplyMoveGrid(IMyCubeGrid grid, double x, double y, double z, bool movePlayerIfFree = false)
        {
            if (grid == null)
                return;

            // Save orientation
            MatrixD currentMatrix = grid.WorldMatrix;

            // New position
            Vector3D newPos = new Vector3D(x, y, z);
            MatrixD newMatrix = currentMatrix;
            newMatrix.Translation = newPos;

            // Find subgrids (filter attached)
            var subgrids = new HashSet<VRage.ModAPI.IMyEntity>();
            MyAPIGateway.Entities.GetEntities(subgrids, g => g is IMyCubeGrid && ((IMyCubeGrid)g).Parent == grid);

            var subgridList = subgrids.OfType<IMyCubeGrid>().ToList();

            // Move main grid
            grid.WorldMatrix = newMatrix;

            // Re-position subgrids
            foreach (var subgrid in subgridList)
            {
                MatrixD subMatrix = subgrid.WorldMatrix;
                subMatrix.Translation = subMatrix.Translation + (newPos - grid.GetPosition());
                subgrid.WorldMatrix = subMatrix;
            }

            // Force sync on dedicated server
            //grid.NeedsWorldMatrixUpdate = true;
            //MyAPIGateway.Entities.UpdateEntity(grid);

            // Optional: Move player if free (not in grid)
            if (movePlayerIfFree)
            {
                var player = MyAPIGateway.Session.Player;
                if (player != null && player.Character != null && player.Character.Parent == null)
                {
                    player.Character.SetPosition(newPos);
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Moved free player to new pos");
                }
            }
        }

        private string LoadConfigText(bool reset = false)
        {
            try
            {
                if (!reset && MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return LobbyScript.DefaultConfig;
                // "[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]";
            }
            catch { return ""; }
        }

        private void SaveConfigText(string text)
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
                {
                    writer.Write(text);
                }
            }
            catch { }
        }

        private void BroadcastConfig()
        {
            string configText = LoadConfigText();
            var lines = configText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            configText = "";
            foreach (var line in lines)
            {
                if (!line.StartsWith("[ServerPasscode]"))
                {
                    configText += line + "\n";
                }
            }
            MyAPIGateway.Multiplayer.SendMessageToOthers(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigData:" + configText));
        }
    }
}