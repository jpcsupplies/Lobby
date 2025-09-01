namespace Lobby.scripts
{
    using System;
    using System.Text;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using VRageMath;
    using VRage.ObjectBuilders;
    using VRage.Game.Components;
    using ProtoBuf;
    using Sandbox.Game.Entities;
    using Sandbox.Common.ObjectBuilders;
    using VRage.Game;

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