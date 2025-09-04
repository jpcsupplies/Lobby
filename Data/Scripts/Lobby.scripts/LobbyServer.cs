namespace Lobby.scripts
{
    using System;
    using System.Text;
    using System.Collections.Generic; // For List<>
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using VRageMath; // For Vector3D
    using VRage.ObjectBuilders; // For MyObjectBuilder_CubeGrid, MyPositionAndOrientation
    using Sandbox.Common.ObjectBuilders; // For MyObjectBuilder_TextPanel, MyObjectBuilder_CubeBlock
    using VRage.Game; // For MyCubeSize, MyPersistentEntityFlags2
    using VRage.Game.Components; // For MySessionComponentDescriptor
    using ProtoBuf;
    using Sandbox.Game.Entities; // For IMyCubeGrid, IMyTextPanel
    using VRage.Game.Entity; // For GetCubeBlock

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class LobbyServer : MySessionComponentBase
    {
        private const string CONFIG_FILE = "LobbyDestinations.cfg";
        private const ushort MESSAGE_ID = 12345; // Unique ID for network messages

        public override void BeforeStart()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            // Register network handler on server
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MESSAGE_ID, HandleMessage);

            // Check and create default config on server
            if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyServer)))
            {
                SaveConfigText("[cubesize] 150000000\n[edgebuffer] 2000\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
            }

            // Broadcast config to all connected clients on server start
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
                // Client requests config, send it
                string configText = LoadConfigText();
                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("ConfigData:" + configText), ulong.Parse(message.Split(':')[1])); // Send to requesting client SteamID
            }
            else if (message.StartsWith("SaveConfig:"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                if (MyAPIGateway.Session.GetUserPromoteLevel(steamId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), steamId);
                    return;
                }

                string text = message.Split(':')[2];
                SaveConfigText(text);
                BroadcastConfig(); // Broadcast updated config to all clients
            }
            else if (message.StartsWith("RequestLed it:"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                if (MyAPIGateway.Session.GetUserPromoteLevel(steamId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, Encoding.UTF8.GetBytes("AccessDenied"), steamId);
                    return;
                }

                // Server spawns LCD
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

                // Notify client
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