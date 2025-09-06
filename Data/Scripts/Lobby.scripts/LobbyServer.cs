namespace Lobby.scripts
{
    using System;
    using System.Text;
    using System.Collections.Generic;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using VRageMath;
    using VRage.ObjectBuilders;
    using Sandbox.Common.ObjectBuilders;
    using VRage.Game;
    using VRage.Game.Components;
    using ProtoBuf;
    using Sandbox.Game.Entities;
    using VRage.Game.Entity;
    using System.Linq; // Added for Skip
    using VRage.ModAPI; // Added for IMyEntity

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
                SaveConfigText("[cubesize] 150000000\n[edgebuffer] 2000\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
            }

            BroadcastConfig();
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MESSAGE_ID, HandleMessage);
            base.UnloadData();
        }

        private void HandleMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            if (message.StartsWith("RequestConfig"))
            {
                string configText = LoadConfigText();
                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigData:" + configText), ulong.Parse(message.Split(':')[1]));
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
        }

        private string LoadConfigText()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return "[cubesize] 150000000\n[edgebuffer] 2000\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]";
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
            MyAPIGateway.Multiplayer.SendMessageToOthers(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigData:" + configText));
        }
    }
}