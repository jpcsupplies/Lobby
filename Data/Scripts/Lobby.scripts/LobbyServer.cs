
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
        public static List<NavigationWarning> ServerNavigationWarnings = new List<NavigationWarning>();


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
            ParseNavigationWarningsServer(LoadConfigText());
         
            LobbyTeleport.InitNetworking();
            LobbyPhysics.InitNetworking();
        }

        //Built the list of Navigation hazards server side so we know where to apply physics effects in the game world.
        private void ParseNavigationWarningsServer(string configText)
        {
            ServerNavigationWarnings.Clear();

            var lines = configText.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inNavigationWarnings = false;

            foreach (var lineRaw in lines)
            {
                string trimmed = lineRaw.Trim();

                if (trimmed.StartsWith("[Navigation Warnings]", StringComparison.OrdinalIgnoreCase))
                {
                    inNavigationWarnings = true;
                    continue;
                }

                if (inNavigationWarnings && trimmed.StartsWith("[") && !trimmed.StartsWith("[Navigation Warnings]"))
                {
                    inNavigationWarnings = false;
                }

                if (!inNavigationWarnings || string.IsNullOrWhiteSpace(trimmed)) continue;

                var parts = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                string coords = parts[0];
                string radiusStr = parts[1];
                string message = string.Join(" ", parts.Skip(2));

                string type = "General";
                double pullpower = 0;
                string coords2 = "";
                double Ex = 0, Ey = 0, Ez = 0, Eradius = 0;

                if (parts.Length > 2)
                {
                    string possibleType = parts[2].ToLowerInvariant();

                    if (possibleType == "r" || possibleType == "radiation")
                    {
                        type = "Radiation";
                        message = string.Join(" ", parts.Skip(3));
                    }
                    else if (possibleType == "b" || possibleType == "blackhole")
                    {
                        type = "Blackhole";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            message = string.Join(" ", parts.Skip(5));
                        }
                        else if (parts.Length > 3)
                        {
                            double.TryParse(parts[3], out pullpower);
                            message = string.Join(" ", parts.Skip(4));
                        }
                    }
                    else if (possibleType == "w" || possibleType == "whitehole")
                    {
                        type = "Whitehole";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            double.TryParse(parts[5], out Eradius);
                            message = string.Join(" ", parts.Skip(6));
                        }
                        else if (parts.Length > 6)
                        {
                            double.TryParse(parts[3], out pullpower);
                            coords2 = parts[4];
                            var coordExit = coords2.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (coordExit.Length == 3)
                            {
                                double.TryParse(coordExit[0], out Ex);
                                double.TryParse(coordExit[1], out Ey);
                                double.TryParse(coordExit[2], out Ez);
                            }
                            message = string.Join(" ", parts.Skip(5));
                        }
                    }
                    else if (possibleType == "e" || possibleType == "eject")
                    {
                        type = "Ejector";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            message = string.Join(" ", parts.Skip(5));
                        }
                        else if (parts.Length > 3)
                        {
                            double.TryParse(parts[3], out pullpower);
                            message = string.Join(" ", parts.Skip(4));
                        }
                        pullpower = -pullpower;
                    }
                }

                var coordParts = coords.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (coordParts.Length == 3)
                {
                    double x, y, z, radius;
                    if (double.TryParse(coordParts[0], out x) &&
                        double.TryParse(coordParts[1], out y) &&
                        double.TryParse(coordParts[2], out z) &&
                        double.TryParse(radiusStr, out radius) && radius > 0)
                    {
                        ServerNavigationWarnings.Add(new NavigationWarning
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            Radius = radius,
                            Message = message,
                            Type = type,
                            Power = pullpower,
                            ExitX = Ex,
                            ExitY = Ey,
                            ExitZ = Ez,
                            ExitRadius = Eradius
                        });

                        // Fixed-exit Whitehole → auto-add Ejector at exit
                        if (type == "Whitehole" && Ex != 0 && radius > 0)
                        {
                            ServerNavigationWarnings.Add(new NavigationWarning
                            {
                                X = Ex,
                                Y = Ey,
                                Z = Ez,
                                Radius = radius * 0.5,
                                Message = "Wormhole Exit – Repulsion Field",
                                Type = "Ejector",
                                Power = -pullpower * 1.5
                            });
                        }
                    }
                }
            }
        }

        //remove this later for cleanup
        private void Old_ParseNavigationWarningsServer(string configText)
        {
            ServerNavigationWarnings.Clear();

            var lines = configText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inNavigationWarnings = false;

            foreach (var lineRaw in lines)
            {
                string trimmed = lineRaw.Trim();
                if (trimmed.StartsWith("[Navigation Warnings]", StringComparison.OrdinalIgnoreCase))
                {
                    inNavigationWarnings = true;
                    continue;
                }
                if (inNavigationWarnings && trimmed.StartsWith("[") && !trimmed.StartsWith("[Navigation Warnings]"))
                {
                    inNavigationWarnings = false;
                }
                if (!inNavigationWarnings || string.IsNullOrWhiteSpace(trimmed)) continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                string coords = parts[0];
                string radiusStr = parts[1];
                string message = string.Join(" ", parts.Skip(2));

                string type = "General";
                double pullpower = 0;
                string coords2 = "";
                double Ex = 0, Ey = 0, Ez = 0, Eradius = 0;

                if (parts.Length > 2)
                {
                    string possibleType = parts[2].ToLowerInvariant();
                    if (possibleType == "r" || possibleType == "radiation")
                    {
                        type = "Radiation";
                        message = string.Join(" ", parts.Skip(3));
                    }
                    else if (possibleType == "b" || possibleType == "blackhole")
                    {
                        type = "Blackhole";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            message = string.Join(" ", parts.Skip(5));
                        }
                        else if (parts.Length > 3)
                        {
                            double.TryParse(parts[3], out pullpower);
                            message = string.Join(" ", parts.Skip(4));
                        }
                    }
                    else if (possibleType == "w" || possibleType == "whitehole")
                    {
                        type = "Whitehole";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            double.TryParse(parts[5], out Eradius);
                            message = string.Join(" ", parts.Skip(6));
                        }
                        else if (parts.Length > 6)
                        {
                            double.TryParse(parts[3], out pullpower);
                            coords2 = parts[4];
                            var coordExit = coords2.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (coordExit.Length == 3)
                            {
                                double.TryParse(coordExit[0], out Ex);
                                double.TryParse(coordExit[1], out Ey);
                                double.TryParse(coordExit[2], out Ez);
                            }
                            message = string.Join(" ", parts.Skip(5));
                        }
                    }
                    else if (possibleType == "e" || possibleType == "eject")
                    {
                        type = "Ejector";
                        if (parts.Length > 3 && parts[3].ToLowerInvariant() == "anomaly")
                        {
                            double.TryParse(parts[4], out pullpower);
                            message = string.Join(" ", parts.Skip(5));
                        }
                        else if (parts.Length > 3)
                        {
                            double.TryParse(parts[3], out pullpower);
                            message = string.Join(" ", parts.Skip(4));
                        }
                        pullpower = -pullpower; // flip to push
                    }
                }

                var coordParts = coords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (coordParts.Length == 3)
                {
                    double x, y, z, radius;
                    if (double.TryParse(coordParts[0], out x) &&
                        double.TryParse(coordParts[1], out y) &&
                        double.TryParse(coordParts[2], out z) &&
                        double.TryParse(radiusStr, out radius) && radius > 0)
                    {
                        ServerNavigationWarnings.Add(new NavigationWarning
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            Radius = radius,
                            Message = message,
                            Type = type,
                            Power = pullpower,
                            ExitX = Ex,
                            ExitY = Ey,
                            ExitZ = Ez,
                            ExitRadius = Eradius
                        });
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MESSAGE_ID, HandleMessage);

            LobbyTeleport.UnloadNetworking();
            LobbyPhysics.UnloadNetworking();
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
            else if (message == "RequestNavWarnings")
            {
                byte[] Mydata = MyAPIGateway.Utilities.SerializeToBinary(ServerNavigationWarnings);
                string b64 = Convert.ToBase64String(Mydata);
                MyAPIGateway.Multiplayer.SendMessageToOthers(MESSAGE_ID, Encoding.UTF8.GetBytes("NavWarningsSync:" + b64));
                return;
            }
           /* else if (message.StartsWith("RequestNavWarnings:"))
            {
                ulong steamId = ulong.Parse(message.Split(':')[1]);
                byte[] MyData = MyAPIGateway.Utilities.SerializeToBinary(ServerNavigationWarnings);
                MyAPIGateway.Multiplayer.SendMessageTo(MESSAGE_ID, MyData, steamId);
            } */
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