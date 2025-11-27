/*
 *  Gateway Lobby Mod 
 *  by PhoenixX (JPC Dev)
 *  For use with Space Engineers Game
 *  Todo: 
 *  instant transfers,  (not happening causes crashes)
 *  navigation warnings, (done)
 *  warning approaching interstellar space warning @ 1000 metres (could be fun)
 *  ability to totally disable departure or interstellar prompts etc. (done)
 *  Move depart notifications optionally to use Draygo text hud API mod (767740490) instead of chat. (could be fun)
 *  ship server transfers (help!), 
 *  save/loading settings/destinations server side (partial)
 *  configurable interstellar boundry points (they are static at 1000kms currently) (done)
 *  a way to notify owners of a visitor at their station popup (might be fun)
 *  Economy(504209260) API: permission to dock - configurable public/private connectors, guns off/on etc for a fee (need Economy api, may be interesting)
 *  Economy(504209260) API: faction territory, entry taxes, GPS indicators etc (Need Economy API)
 *  Economy(504209260) API: charge for travel
 *  Note to self use Ctrl + K, Ctrl + D to for re-tabbing globally
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
//using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using VRage.Audio;
using VRage.Game.ObjectBuilders;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRageMath;
//using System.Globalization;
//using System.IO;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game.Entity;
using ProtoBuf;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // required for MyTransparentGeometry/MySimpleObjectDraw to be able to set blend type.

using Sandbox.Game.Entities.Character.Components; // For MyCharacterStatComponent
using VRage.Game.Components; // For IMyHazardReceiver
using VRage.Utils; // For MyStringHash

namespace Lobby.scripts
{
    [ProtoContract]
    public class Destination
    {
        [ProtoMember(1)] public string Address; // e.g., "1.2.3.4:12345"
        [ProtoMember(2)] public string NetworkName; // e.g., "Orion"
        [ProtoMember(3)] public string Description; // e.g., "Ramblers Frontier"
    }

    [ProtoContract]
    public class NavigationWarning
    {
        [ProtoMember(1)] public double X;
        [ProtoMember(2)] public double Y;
        [ProtoMember(3)] public double Z;
        [ProtoMember(4)] public double Radius;
        [ProtoMember(5)] public string Message;
        [ProtoMember(6)] public string Type; // "Radiation", "R", "General", "B", "W", "E"

        // New – only used for B/W/E zones, ignored for others
        [ProtoMember(7)] public double Power = 0;           // pull/push strength
        [ProtoMember(8)] public double ExitX = 0;           // Whitehole fixed exit
        [ProtoMember(9)] public double ExitY = 0;
        [ProtoMember(10)] public double ExitZ = 0;
        [ProtoMember(11)] public double ExitRadius = 0;
    }

    /*
    [ProtoContract]
    public class NavigationWarning
    {
        [ProtoMember(1)] public double X;
        [ProtoMember(2)] public double Y;
        [ProtoMember(3)] public double Z;
        [ProtoMember(4)] public double Radius;
        [ProtoMember(5)] public string Message;
        [ProtoMember(6)] public string Type; // New: "Radiation" or "General"
    }*/
    [ProtoContract]
    public class GlobalGPS
    {
        [ProtoMember(1)] public double X;
        [ProtoMember(2)] public double Y;
        [ProtoMember(3)] public double Z;
        [ProtoMember(4)] public string ColorName; // e.g., "Lime"
        [ProtoMember(5)] public string Name; // Quoted name, e.g., "Mars"
        [ProtoMember(6)] public string Description; // Remaining text, e.g., "Planet Mars"
    }


    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LobbyScript : MySessionComponentBase
    {
        //Change Version here ------------
        private const string MyVerReply = "Gateway Lobby 3.574a (+Physics B/W/E) By Captain X (aka PhoenixX)";  //mod version
        //Change Version end -------------

        int counter = 0;  //Keeps track of how long since the last full run of main processing loop
        bool initDone = false; //Has the script finished loading its business
        bool seenPopup = false; //have we already displayed a popup in this zone?
        public long lastStationId = 0; // Tracks the last station LCD that triggered a popup
        bool noZone = true; //no zone in sight?
        //private bool handlerRegistered = false;
        private Timer initTimer; //timer for pausing init long enough for grids to load in
        public bool quiet = true; // shall we nag the player about intersteller space?
        public bool jumping = false; public int chargetime = 20; public DateTime startTime = DateTime.UtcNow; public string lockedtarget = "";

        //forced spool override effect
        public bool spoolup = false; //are we still spinning it up?
        public bool spooling = false; //its stuck spinning
        private int spoolCounter = 0;
        private const int SPOOL_DELAY = 112; // 12 ~0.2 seconds at 60 FPS (ticks per second)
        //public bool OverrideArmed = false;

        public string Zone = "";  //placeholder for description of target server
        public string Target = "none"; //placeholder for server address of target server
        public double CubeSize = 150000000; // Default cube size for boundaries (150,000 km diameter)
        public double EdgeBuffer = 2000; // Default edge buffer for approach warnings (meters)
        public string ServerPasscode = ""; // New field for passcode
        public string NetworkName = ""; // Placeholder for server group name
        public bool SuppressInterStellar = false; // Internal Setting to shut off interstellar pager
        public bool AllowDestinationLCD = true; // Setting for destination LCDs 
        public bool AllowStationPopupLCD = true; // Setting for station popup LCDs
        public bool AllowAdminStationPopup = true; //allow popup if disabled but admin ownded
        public bool AllowAdminDestinationLCD = true; // New setting for admin-created only destination 
        public bool AllowStationClaimLCD = true; // Placeholder for station claim LCDs
        public bool AllowStationFactionLCD = true; // Placeholder for station faction LCDs
        public bool AllowStationTollLCD = true; // Placeholder for station toll LCDs
        //public bool NoInterstellar = true;

        //Targets
        public string GW = "0.0.0.0:0"; public double GWP = -10000000; //X
        public string GE = "0.0.0.0:0"; public double GEP = 10000000; //X
        public string GS = "0.0.0.0:0"; public double GSP = -10000000; //Y
        public string GN = "0.0.0.0:0"; public double GNP = 10000000; //Y
        public string GD = "0.0.0.0:0"; public double GDP = -10000000; //Z
        public string GU = "0.0.0.0:0"; public double GUP = 10000000; //Z

        //Zones
        public string GWD = "none";  // -X Galactic West
        public string GED = "none";  // +X Galactic East
        public string GSD = "none"; // -Y Galactic South
        public string GND = "none"; // +Y Galactic North
        public string GDD = "none";  // -Z Galactic Down
        public string GUD = "none";  // +Z Galactic Up

        public readonly List<NavigationWarning> navigationWarnings = new List<NavigationWarning>(); // New list for nav warnings
        private List<GlobalGPS> globalGPS = new List<GlobalGPS>(); // New list for universal GPS

        private Dictionary<long, bool> adminCache = new Dictionary<long, bool>(); // Cache for admin status
        private const string CONFIG_FILE = "LobbyDestinations.cfg";
        private const ushort MESSAGE_ID = 12345; // Same ID as server
        public const string DefaultConfig = "[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]\n[Navigation Warnings]\n[GPS]\n";
        private List<Destination> serverDestinations = new List<Destination>();

        //visual effects system
        bool RadioactiveV = false;
        /*
        private List<Streak> streaks = new List<Streak>();
        private Random rand = new Random(); // For random positioning
        private const int MAX_STREAKS = 10; // Number of streaks on screen
        private float intensity = 1.0f; // 0-1, controls draw delay (higher = more frequent)
        private float drawDelay = 0.1f; // Base delay (seconds), adjusted by intensity
        private float lastDrawTime = 0f; // For timing new streaks
        private DateTime streakStartTime = DateTime.UtcNow; // For elapsed time
        */

        // Streak struct for short-lived streaks
        private struct Streak
        {
            public Vector2 Start;
            public Vector2 End;
            public float StartTime;
            public float Lifetime; // e.g., 0.5 seconds
        }

        //sound effects system
        private Timer soundTimer; // For repeating sound bursts
        MyEntity3DSoundEmitter emitter;
        //private MyEntity3DSoundEmitter lastEmitter; // Track last for interrupt
        //private List<MyEntity3DSoundEmitter> radiationEmitters = new List<MyEntity3DSoundEmitter>(); // Track emitters
        readonly MySoundPair jumpSoundPair = new MySoundPair("IJump");
        readonly MySoundPair WoopSoundPair = new MySoundPair("WoopWoop");
        readonly MySoundPair BadJump = new MySoundPair("DangerJump"); //17 sec
        readonly MySoundPair SpoolLoop = new MySoundPair("BadSpoolLoop"); //1 sec
        readonly MySoundPair Radiation = new MySoundPair("ArcHudVocRadiationCritical");
        readonly MySoundPair Boom = new MySoundPair("ArcWepSmallMissileExplShip");
        readonly MySoundPair Theme = new MySoundPair("SpacePhoenixX");



        readonly MySoundPair SoundTest = new MySoundPair("ArcWepSmallMissileExpl"); //for /ltest sound0 tests
        /*
                    * A few interesting Sound IDs
                    * RealHudVocSolarInbound  solar wind voice warning (uses speech engine, no good)
                    * HudClick
                    * HudMouseClick
                    * ArcNewItemImpact
                    * RealNewItemImpact
                    * ArcHudBleep
                    * ArcHudQuestlogDetail
                    * ArcHudVocRadiationImmunityLow
                    * ArcHudVocRadiationCritical
                    * ArcWepSmallMissileExplShip
                    * ArcWepSmallMissileExpl

                   */

        /// <summary>
        ///     Quick check to see if the script is trying to run server side.
        /// </summary>
        bool AmIaDedicated()
        {

            // Am I a Dedicated Server?.
            if (MyAPIGateway.Utilities != null && MyAPIGateway.Multiplayer != null
                && MyAPIGateway.Session != null && MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
            {
                return true; //definately am a server and im not running it locally, dont do anything!
            }
            //definately not offline or running dedicated server side, I must be a player on a server
            return false;
        }

        /// <summary>
        ///     What to do next after player joins the server.  Client side only.
        /// </summary>
        public void Init()
        {
            MyAPIGateway.Utilities.MessageEntered += GotMessage;

            LobbyTeleport.InitNetworking();
            LobbyPhysics.InitNetworking();

            if (!AmIaDedicated())
            {
                // Reset popup state on init
                // probably redundant if we just connected to the map
                // but a debug can potentially will carry over an invalid state so better to reset it again
                seenPopup = false;
                lastStationId = 0;

                //ok lets warm up the hud.. unless of course we are a server.. which would be stupid
                //no longer using hud to prevent conflicts with other mods
                /* MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Clear();
                 MyAPIGateway.Utilities.GetObjectiveLine().Title = "Initialising";
                 MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Add("Scanning..");
                 MyAPIGateway.Utilities.GetObjectiveLine().Show(); */


                // Register client network handler
                MyAPIGateway.Multiplayer.RegisterMessageHandler(MESSAGE_ID, HandleMessage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(MESSAGE_ID, HandleServerNavWarnings);

                // Request config from server
                MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestConfig:" + MyAPIGateway.Session.Player.SteamUserId));
                // Request navigation warnings from server
                //MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestNavWarnings:" + MyAPIGateway.Session.Player.SteamUserId));
                MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestNavWarnings"));
                ParseConfigText(LoadConfigText()); // Fallback to local if no server response

                // Check and create default config
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
                {
                    SaveConfigText(DefaultConfig);
                    //"[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
                }

                //Lets let the user know whats up. 
                MyAPIGateway.Utilities.ShowMessage("VER", MyVerReply);
                MyAPIGateway.Utilities.ShowMessage("Lobby", "This sector supports gateway stations! Use /Lhelp for details.");

                //Triggers the 1 off scan for Interstellar Space boundry definitions to populate the destination list.

                //Do an initial 5 second pre-warmup waiting on data from server
                initTimer = new Timer(5000); // 5s delay
                initTimer.Elapsed += (s, e) =>
                {
                    // ParseConfigText(LoadConfigText()); // Ensure globals are updated
                    //SetExits();
                    //UpdateLobby(false);
                    //MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: This is the 5 second delay.");
                    //removing the "&& !quiet  from below if"  logically init should only run once except under old
                    //debug logic 
                    if (SetExits()) { MyAPIGateway.Utilities.ShowMessage("Scan", "Interstellar Space Path(s) Found!"); quiet = false; }
                    else { MyAPIGateway.Utilities.ShowMessage("Scan", "No Interstellar Space Detected."); }
                    initTimer.Stop();
                };
                initTimer.AutoReset = false;
                initTimer.Start();

                MyAPIGateway.Utilities.ShowMessage("", "Scan For Interstellar Space Paths..");
                if (SetExits()) { } //silently do a prescan
                //    MyAPIGateway.Utilities.ShowMessage("Note", "Interstellar Space Exit(s) Detected!");
                //   quiet = false;
                // }
                //else { MyAPIGateway.Utilities.ShowMessage("Note", "Scanning for paths through Interstellar Space.."); }
            } //else we are dedicated server


            initDone = true;
        }

        /// <summary>
        ///     Attempts to shut things down neatly when game exits.
        /// </summary>
        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= GotMessage;
            StopLastPlayedSound(); // Ensure sound cleanup to avoid memory leaks/sound bugs

            LobbyTeleport.UnloadNetworking();
            LobbyPhysics.UnloadNetworking();

            if (!AmIaDedicated())
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MESSAGE_ID, HandleMessage);
            }
            base.UnloadData();
        }


        private bool LCDOwnedByAdmin(IMyTextPanel textPanel, bool debug = false)
        {
            if (textPanel == null)
                return false;

            var grid = textPanel.CubeGrid as VRage.Game.ModAPI.IMyCubeGrid;
            if (grid == null || grid.BigOwners.Count == 0)
                return false;

            var player = MyAPIGateway.Session.Player;
            if (player == null)
                return false;

            // Check if the current player owns the grid
            VRage.Game.MyRelationsBetweenPlayerAndBlock relation = (textPanel as VRage.Game.ModAPI.IMyCubeBlock).GetUserRelationToOwner(player.IdentityId);
            bool isOwnedByPlayer = relation == VRage.Game.MyRelationsBetweenPlayerAndBlock.Owner;

            if (isOwnedByPlayer)
            {
                // Local check for player-owned grid (offline/co-op/single-player)
                bool isAdmin = MyAPIGateway.Session.GetUserPromoteLevel(player.SteamUserId) >= MyPromoteLevel.SpaceMaster;
                adminCache[grid.BigOwners[0]] = isAdmin;
                if (debug)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "In LCD Owned By Admin Check.");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Player-owned grid, IsAdmin: {isAdmin}");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: My PromoteLevel: {MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId)}");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Player IdentityId: {player.IdentityId}, Grid Owners: {string.Join(", ", grid.BigOwners)}");
                }
                return isAdmin;
            }



            // For non-player-owned grids (dedicated server, remote admins)
            long ownerId = grid.BigOwners[0];
            if (adminCache.ContainsKey(ownerId))
                return adminCache[ownerId];

            MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes($"IsAdmin:{(ulong)ownerId}"));
            return false;
        }


        //Client Handler
        private void HandleMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            if (message.StartsWith("ConfigData:"))
            {
                string configText = message.Substring("ConfigData:".Length);
                ParseConfigText(configText);
                SetExits();
                //converted to more arcane readable check for troubleshooting 
                //Zone = serverDestinations.Any(d => d.Address != "0.0.0.0:0") ? "Scanning..." : "No interstellar exits defined";
                if (serverDestinations.Any(d => d.Address != "0.0.0.0:0"))
                {
                    Zone = "Scanning...";
                }
                else
                {
                    Zone = "No interstellar exits defined";
                }
                Target = "none";
                UpdateLobby(false);
            }
            if (message.StartsWith("ConfigReset:"))
            {
                MyAPIGateway.Utilities.ShowMessage("Lobby", "Config reset complete: Defaults regenerated.");
            }

            else if (message == "AccessDenied")
            {
                MyAPIGateway.Utilities.ShowMessage("Lobby", "Access denied: Requires Space Master or higher.");
            }
            else if (message == "Led itSuccess")
            {
                MyAPIGateway.Utilities.ShowMessage("Lobby", "Config LCD spawned by server. Interact (F key) to edit, then use /lsave.");
            }
            else if (message == "Led itFailed")
            {
                MyAPIGateway.Utilities.ShowMessage("Lobby", "Failed to spawn config LCD.");
            }
            else if (message.StartsWith("AdminStatus:"))
            {
                var parts = message.Split(':');
                long ownerId = long.Parse(parts[1]);
                bool isAdmin = bool.Parse(parts[2]);
                adminCache[ownerId] = isAdmin;
            }
            else if (message.StartsWith("NavWarningsSync:"))
            {
                try
                {
                    string b64 = message.Substring(15);
                    byte[] binaryData = Convert.FromBase64String(b64);
                    var receivedList = MyAPIGateway.Utilities.SerializeFromBinary<List<NavigationWarning>>(binaryData);

                    navigationWarnings.Clear();
                    navigationWarnings.AddRange(receivedList);

                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Synced {navigationWarnings.Count} nav warnings from server");
                }
                catch
                {
                    // Fallback to local parse if sync fails
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Nav sync failed – using local");
                }
                return;   //does this need to be here? the others dont use it..
            }

        }

        /// <summary>
        ///     Main client side processing loop. Runs the whole show.
        /// </summary>
        public override void UpdateAfterSimulation()
        {

            // Need to think on ambient sounds
            // Some ominous sounding hum thing for near black holes, 
            // some sucking sounding hum for white holes, 
            // a rough sounding effect for when players are teleported, 
            // some general sound for simply being shoved out by Ejector Zones.
            //They would run only client side and possibly loop



            if (!initDone && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                Init();

            // TESTING CLIENT ONLY — dedicated server skips everything here
            if (AmIaDedicated())
            {
                base.UpdateAfterSimulation();
                return;
            }

            //once again, lets not run this bit on a server.. cause that would be dumb
            if (!AmIaDedicated())
            {
                //Visual effects
                // Draws lines indication boundry
                // Not working DrawBoundaryLines();
                //RadioactiveV means we are in an interstellar exit point boundary region.
                //needs further tweaking to adjust intesity by distance
                //probably a simple global number derived by checking how far to edge
                if (RadioactiveV) { DrawStaticStreaks2(1.0f); }  //random non directional particle static streaks

                //visual effects for override jumps; or just for testing.
                if (spoolup || spooling) { DrawStaticStreaks2(0.5f); }  //mostly random but directional
                if (spooling) { DrawStaticStreaks(0.6f); }  // focuses on crosshair

                /*if (!initDone || string.IsNullOrEmpty(Zone)) // Retry if no LCDs detected
                {
                    SetExits();
                    ParseConfigText(LoadConfigText());
                    UpdateLobby(false);
                    initDone = true;
                }
                */

                //my dirty little timer loop - fires roughly each 15 seconds
                //it was 2 seconds but that would flood the chat too much with
                //Departure point notifications
                if (counter >= 900)
                {
                    counter = 0;
                    if (SetExits()) { quiet = false; }  //rechecks in case the lcds didnt load in yet or got added
                    if (UpdateLobby(false))
                    {
                        string reply = "";
                        //More aggressive checks so we don't get interstellar /depart prompts for dead exits
                        if (Target != "0.0.0.0:0" || Target != "none" && Zone != "none")
                        {
                            //reply = "Warning: You have reached the edge of " + Zone + " Interstellar Space"; } no message if no exit
                            //else {                
                            //MyAPIGateway.Utilities.ShowMessage("Debug", $"Tar {Target} (not 0.0.0.0:0) Zone {Zone} (not none)");
                            reply = Zone + " [Type /depart to travel]";
                            if (!jumping) MyAPIGateway.Utilities.ShowMessage("Departure point", reply);
                        }

                    }
                    else
                    {
                        Zone = "none"; //previously Scanning...
                        Target = "none";
                    }
                }
                counter++;

                //this stuff controls times for charging to jump etc its aethetic only so the sound plays before connect 
                //         jumping chargetime startTime lockedtarget  

                if (jumping && chargetime > 0)
                {
                    string reply = "";
                    if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(1))
                    {
                        startTime = DateTime.UtcNow;
                        //if it is a jump override, jam the jump engine until /depart and abort jump countdown
                        if (chargetime <= 2 && spoolup) { spooling = true; jumping = false; spoolup = false; reply = "Charging *&^%!@@ Error *##//|* /override to cancel, /depart to jump anyway."; StopLastPlayedSound(); PlaySound(SpoolLoop, 1.2f); }
                        else
                        {
                            chargetime--;
                            reply = $"Charging {chargetime}";
                        }
                        MyAPIGateway.Utilities.ShowMessage("", reply);
                    }
                }

                else if (chargetime <= 0 && jumping)
                {
                    jumping = false;
                    chargetime = 20;
                    MyAPIGateway.Utilities.ShowMessage("Travelling to ", lockedtarget);
                    //Free up any sound handles before we jump server
                    StopLastPlayedSound();
                    JoinServer(Target);
                }

                if (spooling) //may need to make this a pure else so the above flag hits this frame
                {
                    spoolCounter++;
                    if (spoolCounter >= SPOOL_DELAY)
                    {
                        //string reply = $"Charging {chargetime}";  should be random

                        StopLastPlayedSound();
                        spoolCounter = 0;
                        PlaySound(SpoolLoop, 1.2f);
                    }
                }
                else
                {
                    spoolCounter = 0; // Reset counter when spooling stops
                }
            }
            //moved server side tick control to LobbyServer.cs
            base.UpdateAfterSimulation();
        }

        /// <summary>
        ///     Processes chat text and decides if it should show in game chat or not.
        /// </summary>
        /// 
        private void GotMessage(string messageText, ref bool sendToOthers)
        {
            //string reply;
            // here is where we nail the echo back on commands "return" also exits us from processMessage
            // return true supresses echo back, false allows it.
            if (ProcessMessage(messageText)) { sendToOthers = false; }
            //if (!string.IsNullOrEmpty(reply)) { MyAPIGateway.Utilities.ShowMessage("Lobby", reply); }
        }


        /// <summary>
        ///     Rexxars: IMyMultiplayer.JoinServer called this way to prevent crashes.
        /// </summary>
        /// <param name="ip"></param>
        public static void JoinServer(string ip)
        {
            //Little change to joinserver to allow instant to work at a later date hopefully, courtesy of rexxars brain.
            MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Multiplayer.JoinServer(ip));
        }

        /// <summary>
        ///     Populates Interstellar Space Boundry Destinations. Returns true if it found any, false if it found none.
        /// </summary>
        public bool SetExits()
        {
            // Update globals from serverDestinations (populated by ParseConfigText)
            GW = GE = GN = GS = GU = GD = "0.0.0.0:0"; // Reset defaults
            //GWD = "Galactic West"; GED = "Galactic East"; GND = "Galactic North";
            //GSD = "Galactic South"; GUD = "Galactic Up"; GDD = "Galactic Down";

            double range = CubeSize / 2; // Half for distance from center (e.g., 75,000 km)
            GWP = -range; GSP = -range; GDP = -range;
            GEP = range; GNP = range; GUP = range;

            foreach (var dest in serverDestinations)
            {
                if (dest.Address != "0.0.0.0:0")
                {
                    switch (dest.NetworkName.ToUpper())
                    {
                        case "[GW]": GW = dest.Address; GWD = dest.Description; break;
                        case "[GE]": GE = dest.Address; GED = dest.Description; break;
                        case "[GN]": GN = dest.Address; GND = dest.Description; break;
                        case "[GS]": GS = dest.Address; GSD = dest.Description; break;
                        case "[GU]": GU = dest.Address; GUD = dest.Description; break;
                        case "[GD]": GD = dest.Address; GDD = dest.Description; break;
                    }
                }
            }
            bool hasExits = serverDestinations.Any(d => d.Address != "0.0.0.0:0");
            //expanded for readability
            //Zone = hasExits ? "Scanning..." : "No interstellar exits defined"; 
            //Return True if we have valid exits, and cubesize is not invalid/interstellar space isnt disabled
            if (hasExits && !SuppressInterStellar)
            {
                Zone = "Scanning..."; //old hud logic
                //MyAPIGateway.Utilities.ShowMessage("Debug:", "Has exits!");
                return true;
            }
            else
            {
                Zone = "No interstellar exits defined";
                //MyAPIGateway.Utilities.ShowMessage("Debug:", "Didn't see exits?");
                return false;
            }
            //return hasExits; supposed to return true but odd things happened
            // return serverDestinations.Any(); // True if any destinations are configured
        }

        /// <summary>
        ///     Checks players surroundings for proximity to departure points and special LCDs
        ///     Returns true if it found any, false if not or invalid.
        ///     Can also be passed a true or false to force display of debug/diag info or not
        /// </summary>
        public bool UpdateLobby(bool debug = false)
        {

            //check our position - are we in a hot spot?
            // remove ? so only current player not all players trigger. if (MyAPIGateway.Session.Player?.Controller?.ControlledEntity != null) {
            // this might be useful to later reuse for a check that sends a warning to a faction member that an enemy has entered their territory

            // ? has been added again but may still need to remove it, if multiplayers mis-trigger popup resets
            if (MyAPIGateway.Session.Player.Controller.ControlledEntity != null)
            {
                //reverted to old logic as adding '?' might cause undesired resets if other players nearby
                //and loose track if there is only us..  mostly desperation at this point
                //if (MyAPIGateway.Session.Player?.Controller?.ControlledEntity != null) {
                //   return false;
                // }

                //hard coded target list cause I suck at server side datafiles.. kept for reference only
                /* if (X >= -100 && X<=100 && Y >= -100 && Y<=100 && Z >= 15 && Z <=25) { Zone = "Lawless void"; Target = "221.121.159.238:27270"; return true; }
                 if (X >= -100 && X <= -10 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Black Talon Sector"; Target = "59.167.215.81:27016"; return true; }
                 if (X >= -100 && X <= -10 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Spokane Survivalist Sector"; Target = "162.248.94.205:27065"; return true; }
                 if (X >= 10 && X <= 100 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Pandora Sector"; Target = "91.121.145.20:27016"; return true; }
                 if (X >= 10 && X <= 100 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Ah The Final Frontier"; Target = "192.99.150.136:27039"; return true; }
                 else { Zone = "Scanning..."; Target = "none"; return false;  }  */

                //look for an lcd customName [destination] then grab the text and extract a server address, network and caption
                //display the caption in hud, and set the server address to connect to. May also set a network name somehow 
                //eg. the string/text immediately after the server address which would be cross referenced with configured
                //known networks in the config file (and the associated passkey when that feature is added) 
                //long descriptions are pulled using getText from the screen of the LCD or any text added after network name
                //eg [destination] 14.2.3.1:1234 OGNOrion Orion Pirates Sector
                //or [destination] 14.2.3.1:1234 OGNOrion    With the "orion pirates sector"  stored on the LCD screen instead.
                //where both exist - default to the shorter description (most likely the one in customName) for the chat depart pager

                //var players = new List<IMyPlayer>();
                // MyAPIGateway.Players.GetPlayers(players, p => p != null); //dont need list of players unless we are doing seat/cryo allocations when we transfer ships too.
                // Player position in 3d space assigned to 'player'
                Vector3D position = MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetPosition();
                var updatelist = new HashSet<IMyTextPanel>(); //list of lcds
                string[] LCDTags = new string[] { "[destination]", "(destination)" };
                var sphere = new BoundingSphereD(position, 9); //destination lcds
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);



                //updatelist.Clear(); // Ensure fresh list used in forced update code, disabled atm


                // Collect [destination] LCDs found
                foreach (var block in LCDlist)
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null && LCDTags.Any(tag => textPanel.CustomName?.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        updatelist.Add(textPanel);
                    }
                    //if (debug) { } //debug flag if needed
                }

                //if (debug && updatelist.Count > 0) { } //additional spot for debug if needed
                //Debug info
                if (debug)
                {
                    var player = MyAPIGateway.Session.Player;
                    bool isAdmin = MyAPIGateway.Session.GetUserPromoteLevel(player.SteamUserId) >= MyPromoteLevel.SpaceMaster;
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Pre Station Check.");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Found {LCDlist.Count} entities, {updatelist.Count} LCDs, Tags: {string.Join(",", LCDTags)}");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Player-owned grid, IsAdmin: {isAdmin}");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: My PromoteLevel: {MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId)}");

                }

                //Normal Check [station] LCDs for popup Logic

                var updatelist2 = new HashSet<IMyTextPanel>(); //list of popup [station] lcds
                var sphere2 = new BoundingSphereD(position, StationPrescan()); //popup notification lcds scanrange
                var LCDlist2 = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere2);
                string[] stationTags = new string[] { "[station]", "(station)", "[overrride]" };
                // old check string[] LCDTags2 = new string[] { "[station]", "(station)" };

                foreach (var block in LCDlist2) //popup station notification lcds
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null
                        && textPanel.IsFunctional
                        && textPanel.IsWorking
                        && stationTags.Any(tag => textPanel.CustomName?.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        //noZone = false;  //need to double check what i use this for.
                        updatelist2.Add(textPanel);
                    }
                }

                //orignal [station] logic  this is where things like popup or claim or whatever lcd based tags are checked for
                foreach (var textPanel in updatelist2)
                {
                    //string popup = "";
                    //   var checkArray = (textPanel.GetPublicTitle() + " " + textPanel.GetPrivateTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); //private title removed by keen
                    var checkArray = textPanel.CustomName.ToString().Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string popup = textPanel.GetText() ?? "";
                    if (checkArray.Length >= 1) //if its Not at least 1 its invalid.
                    {
                        foreach (var str in checkArray)
                        {
                            // if (!seenPopup && str.Equals("popup", StringComparison.InvariantCultureIgnoreCase))
                            //check that we have not already seen a popup
                            if (!seenPopup || textPanel.EntityId != lastStationId)
                            {

                                //if this is a popup lcd and popups are enabled or admin-owned with AllowAdminStationPopup true
                                bool isAdminOverride = AllowAdminStationPopup && LCDOwnedByAdmin(textPanel, debug);
                                bool popupCondition = AllowStationPopupLCD || isAdminOverride;
                                if (debug)
                                {
                                    var player = MyAPIGateway.Session.Player;
                                    var grid = textPanel.CubeGrid as VRage.Game.ModAPI.IMyCubeGrid;
                                    MyAPIGateway.Utilities.ShowMessage("Lobby", "In popup Check.");
                                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Player IdentityId: {player.IdentityId}, Grid Owners: {string.Join(", ", grid.BigOwners)}");

                                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Popup condition - AllowStationPopupLCD={AllowStationPopupLCD}, isAdminOverride={isAdminOverride}, popupCondition={popupCondition}");
                                }

                                if (popupCondition && str.Equals("popup", StringComparison.InvariantCultureIgnoreCase))
                                //if this is a popup lcd and popups are enabled show the message
                                //if ((AllowStationPopupLCD || (AllowAdminStationPopup && LCDOwnedByAdmin(textPanel))) && str.Equals("popup", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    MyAPIGateway.Utilities.ShowMissionScreen("Station", "", "Notice", popup, null, "Close");
                                    seenPopup = true;
                                    lastStationId = textPanel.EntityId; // Track this station
                                }
                                else if (textPanel.CustomName.Contains("[station]") && !popupCondition)
                                {
                                    if (debug)
                                    {
                                        MyAPIGateway.Utilities.ShowMessage("Lobby", $"Station popup suppressed (AllowStationPopupLCD: {AllowStationPopupLCD}, AllowAdminStationPopup: {AllowAdminStationPopup}, IsAdmin: {LCDOwnedByAdmin(textPanel)})");
                                    }
                                }
                                //workaround for not having working switch panel pressed / detected code yet for bump/jumps
                                /*else if (textPanel.CustomName.Contains("[override]") && !OverrideArmed) {
                                    OverrideArmed = true;
                                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Someone seems to have set a jump /override here?");

                                }*/
                            }
                        }
                    }
                    // this was here for a reason but debug code not using it atm. else { break; }
                }

                //Check if player has all the admin defined GPS points
                foreach (var gpsPoint in globalGPS)
                {
                    var coords = new Vector3D(gpsPoint.X, gpsPoint.Y, gpsPoint.Z);
                    // if (Vector3D.Distance(position, coords) <= 1000.0)
                    //{
                    //MyAPIGateway.Utilities.ShowMessage("Lobby", "I am spawning a [GPS]");
                    GPS(gpsPoint.X, gpsPoint.Y, gpsPoint.Z, gpsPoint.Name, gpsPoint.Description, true, gpsPoint.ColorName);
                    // }
                }



                //[Navigation Warnings] logic
                //If it is first time pop up warning; set seen flag
                //If it is second time only show console message
                //If no warnings are in current location clear the seen flag.
                //This will also suppress popups if you just saw a station or claim popup, but still show a warning in chat.
                //Potentially may be worth letting multiple trigger in the case of nested warnings eg 100km caution, 50km warning, final 10km goodbye message

                //May be worth spawning a GPS point in red text too? Or would that be better as some other feature? Such as creating GPS points
                //for important POIs like planets?
                bool ClearSeenState = true;   //Default that we didn't just see a warning, so safe to clear seenPopup
                int haznumber = 0;
                foreach (var warning in navigationWarnings)
                {
                    haznumber++;
                    bool Radioactive = false;
                    var MyRadius = warning.Radius; //Used so we can add a fixed safety 
                    string typeCode = warning.Type == "Radiation" ? "R" : "Z"; // Z code for general non defined nav hazard, R for radiation.

                    //G code means some sort of gravity well/effect, also we should give an extra 100 metres safety margin for those as they evil as hell, and score 11/10 on the nope scale
                    if (warning.Type == "Blackhole" || warning.Type == "Whitehole" || warning.Type == "Ejector") { typeCode = "G"; MyRadius += 100; }

                    //In these checks we can set a global flag for gravity type events, or simply do nothing and let the tick loop 
                    //server side handle applying damage/drift/teleport events. Server side is preferable as it load balances the work
                    //better and will immediately apply the effect, before waiting for the user to get the next hazard warning round.
                    //Downside is we need an efficient way to make sure the server is tracking positions.
                    //Another issue is unpiloted grids should also have gravity/damage/teleport effects applied if they enter a G class
                    //Hazard zone, which would simply not work if that check only occured client side.

                    //If we have not seen popup, and we are in a Hazard zone, and the zone type isn't BlackHole show popup.
                    //we dont want to see the popup on a blackhole as we have more important things to do, like try to escaping not click a button
                    if (!seenPopup && warning.Type != "Blackhole" && Vector3D.Distance(position, new Vector3D(warning.X, warning.Y, warning.Z)) <= MyRadius)
                    {
                        if (warning.Type == "Radiation") Radioactive = true;
                        GPS(warning.X, warning.Y, warning.Z, $"Nav Hazard#{typeCode}{haznumber} R:{MyRadius / 1000}KM", warning.Message, true);
                        MyAPIGateway.Utilities.ShowMissionScreen("Navigation Warning", $"[{typeCode}] ", warning.Type, warning.Message, null, "Close");
                        StopLastPlayedSound(); PlaySound(WoopSoundPair, 0.4f);
                        seenPopup = true; //no other popups recently shown except this one 
                        ClearSeenState = false;
                        //MyAPIGateway.Utilities.ShowMessage("Lobby", $"Navigation warning triggered: {warning.Message}");
                        //break; // Only show one at a time (Disable if need to show multiple)
                    }
                    //If it is a blackhole or we already saw a popup just ping the chat with a warning.
                    else if ((seenPopup || warning.Type == "Blackhole") && Vector3D.Distance(position, new Vector3D(warning.X, warning.Y, warning.Z)) <= MyRadius)
                    {
                        if (warning.Type == "Radiation") Radioactive = true;

                        //I can probably disable this one, if they saw the popup already likely already created one, but
                        //in edge cases where a station popup is within a warning zone  it might not create the gps
                        GPS(warning.X, warning.Y, warning.Z, $"Nav Hazard#{typeCode}{haznumber} R:{MyRadius / 1000}KM", warning.Message, true);
                        //popups recently seen somewhere so just use a less annoying chat warning this time.                        
                        MyAPIGateway.Utilities.ShowMessage($"Nav[{typeCode}] Alert", $"*{warning.Type}*: <{warning.Message}>");
                        StopLastPlayedSound();
                        if (typeCode == "R")
                        {
                            //Use Default Radiation Sound for Radiation hazards
                            PlayRadiationTicks();
                        }
                        else
                        {                        
                            //Use Alert Sound
                            PlaySound(WoopSoundPair, 0.2f);
                        }
                        ClearSeenState = false;
                        //break;  // Only show one at a time (Disable if need to show multiple)
                    }
                    if (Radioactive)
                    {
                        // Radiation damage
                        double distance = Vector3D.Distance(position, new Vector3D(warning.X, warning.Y, warning.Z));

                        double edgeThreshold = 10;

                        //If the edge buffer is smaller than the hazard zone, use that as the reduced radiation buffer zone.
                        if (EdgeBuffer > warning.Radius) { edgeThreshold = EdgeBuffer; }
                        else { edgeThreshold = warning.Radius * 0.2; } //otherwise make the buffer zone 20% of the zone radius

                        // Base radiation gain per tick
                        float baseDamage = 14.0f; // Base before random
                        var rand = new Random(DateTime.Now.Millisecond); // Seed with ms for randomness
                        double randomValue = rand.NextDouble() * 5 + 1; // Random 1.0 to 6.0
                        float randomDeduct = (float)Math.Round(randomValue, 2); // Round to 2 decimals (e.g., 3.45)
                        float damage = baseDamage - randomDeduct; // Deduct random (e.g., 15 - 3 = 12)
                        //spin the wheel a few more times since sometimes it gets stuck.
                        randomValue = 0;
                        randomValue = rand.NextDouble() * (randomDeduct * 0.81);
                        randomDeduct = 0;

                        //if player distance from zone centre is bigger than edgeThreshold
                        if (distance > (warning.Radius - edgeThreshold))
                        {
                            //We must be in the outer buffer zone, less radiation here.
                            //MyAPIGateway.Utilities.ShowMessage("Debug", "Bufferzone 80% Damage");
                            damage *= 0.8f; // Reduce at edge
                        }
                        else if (warning.Message.ToLower().StartsWith("anomaly") && distance < edgeThreshold)
                        {
                            //we must be at the centre of an Anomaly, reduce damage by half to make exploration viable.
                            //MyAPIGateway.Utilities.ShowMessage("Debug", $"Anomaly middle 50% damage.");
                            damage = (damage * 0.5f) + 1.0f; // Halve in center for anomaly +1 bias
                        }
                        else
                        {
                            // MyAPIGateway.Utilities.ShowMessage("Debug", $"Distance NOT {distance} > (ZR{warning.Radius} - Buf{edgeThreshold}) 100% Damage"); 
                            //MyAPIGateway.Utilities.ShowMessage("Debug", $"NOT Anomaly Middle or Bufferzone 100% Damage");
                        }
                        //MyAPIGateway.Utilities.ShowMessage("Debug", $"You would have taken {damage} radiation damage.");

                        // Apply radiation damage - Moved to EffectPlayer() for simplicity.
                        EffectPlayer("Radiation", damage);

                    }
                }



                // Process [destination] LCDs if allowed
                if (AllowDestinationLCD)
                {

                    foreach (var textPanel in updatelist)
                    {
                        //we should only display them if allowdestination or failing that allowadmindestination is true
                        if (AllowDestinationLCD || (AllowAdminDestinationLCD && LCDOwnedByAdmin(textPanel)))
                        {
                            var nameArray = textPanel.CustomName.ToString().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (debug) { MyAPIGateway.Utilities.ShowMessage("Lobby", $"Processing LCD: {textPanel.CustomName}, Name Array: {string.Join(",", nameArray)}, Text: {textPanel.GetText() ?? "null"}"); }
                            if (nameArray.Length >= 2)
                            {
                                int nameIdx = nameArray[0].IndexOf("[destination]", StringComparison.InvariantCultureIgnoreCase) >= 0 ? 1 : 0;
                                Target = nameArray[nameIdx]; // Server address
                                Zone = textPanel.GetText() ?? string.Join(" ", nameArray.Skip(nameIdx + 1)); // Description
                                if (debug) { MyAPIGateway.Utilities.ShowMessage("Lobby", $"Set Target: {Target}, Zone: {Zone}"); }
                                noZone = false;
                                return true;
                            }

                            if (debug) { MyAPIGateway.Utilities.ShowMessage("Lobby", $"Invalid CustomName: {textPanel.CustomName}, Length: {nameArray.Length}"); }
                        }
                    }
                }

                // Check interstellar boundaries if no [destination] LCDs found and boundry is valid.
                // Quiet supresses rechecking and messaging chat too much if we already did in the last cycle??
                // SupressInterStellar supresses interstellar logic if the boundry is invalid or disabled.
                // SO - if check for interstellar - visual effect then return if true (on return audio/visual - make it seperate sub)
                //    - not intersteller check if we are in buffer then flip bool flag
                //    - if bool flag display/play audio/visual
                if (!quiet || !SuppressInterStellar)
                {
                    double X = position.X; double Y = position.Y; double Z = position.Z;
                    double range = CubeSize / 2; // Half cube size from center, local variable so we can adjust for range check without accidentally changing global cubesize
                    double buffer = EdgeBuffer; // Use class-level EdgeBuffer in case we need to add corrections code when buffer is bigger than range size at a later point
                    bool inbuffer = false; bool interstellar = false;

                    // For now, check if beyond exit range set target then return
                    //this does not cater for overlapping exits, (ie beyond corners) which should probably just act like
                    //normal space, or only on the longest axis, current behaviour is first come first served which is "good enough"
                    if (X <= -range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GWD; Target = GW; interstellar = true; }
                    else if (X >= range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GED; Target = GE; interstellar = true; }
                    else if (Y <= -range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GSD; Target = GS; interstellar = true; }
                    else if (Y >= range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GND; Target = GN; interstellar = true; }
                    else if (Z <= -range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GDD; Target = GD; interstellar = true; }
                    else if (Z >= range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GUD; Target = GU; interstellar = true; }
                    else { Zone = "none"; Target = "none"; RadioactiveV = false; } //reset none to check for dead exits later before buffer effects

                    // is it an interstellar zone
                    if (interstellar)
                    {
                        //is it an active exits zone?
                        if (Zone != "none") RadioactiveV = true;
                        return true;
                    }
                    // Interstellar buffer zone effects
                    //here we need to check again our proximity to zone to see if we are in buffer zone
                    //we can recycle the logic since the above return will return before this check, so can safely shift the range check by buffer size
                    //so being past the new range line will only happen if its a buffer zone here, making things a lot simpler

                    //First a sanity check so we dont end up with negative range or no bufferzone
                    if (range > buffer) { range -= buffer; }  //use buffer zone configured if it is smaller than range even if zero
                    else if (range >= 100 && buffer > 0) { range -= 50; } //use a token buffer check of 50 so long as range is at least 100 and a non zero buffer is set

                    //another error check, if someone keeps getting this error  may need to correct buffer and try again
                    //if range is zero or less something has gone horribly wrong fix it and throw an error warning
                    //even if interstellar zones are disabled it should still have a cubesize value by default
                    if (range <= 0)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Error", $"Exit Check Range [{range}] is or below zero after checking buffer size [{buffer}]. Correcting buffer to 80% of {CubeSize / 2}m range");
                        if (CubeSize > 0) { range = ((CubeSize / 2) - (CubeSize / 2) * 0.8); } //20%
                        else { range = 50; } //again use a token range check of 50 if things are very wrong
                    }

                    //hold on, do we even have a buffer? 0 means it was disabled.
                    if (buffer > 0)
                    {
                        //set inbuffer true if our position matches a buffer region, any buffer region
                        if (X <= -range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GWD; Target = GW; inbuffer = true; }
                        if (X >= range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GED; Target = GE; inbuffer = true; }
                        if (Y <= -range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GSD; Target = GS; inbuffer = true; }
                        if (Y >= range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GND; Target = GN; inbuffer = true; }
                        if (Z <= -range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GDD; Target = GD; inbuffer = true; }
                        if (Z >= range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GUD; Target = GU; inbuffer = true; }
                    }
                    else { return false; RadioactiveV = false; }  //apparently not, no buffer so lets stop things here

                    //next we need to check if it is a valid exit or a dead exit or if there are any exits at all and only
                    //if we detected that we are in a buffer region
                    //quiet will be true if no interstellar exits exist. (may need to double check that)
                    //Target will still be none if we are nowhere near a buffer, or if there is no exit after this buffer anyway as it is
                    //default to none on dead facings
                    //Suppress interstellar will stop it if interstellar space is disabled

                    if (!quiet && !SuppressInterStellar && Target != "none" && inbuffer)
                    {
                        RadioactiveV = true; //We Need visual effects
                        //warn of nearing Interstellar space proximity
                        string cautionMsg = "Caution: Approaching interstellar space.";
                        cautionMsg += $" Destination {Zone} ahead.";
                        MyAPIGateway.Utilities.ShowMessage("Lobby", cautionMsg);

                        //play clipped 2sec radiation tick sound, volume set by distance to edge
                        //have disabled volume logic until confirm sound is even working
                        double intensity = 1.0;// - (Math.Abs(distToBoundary) / buffer); // 1.0 at boundary, 0 at edge
                                               // Radiation sound (first 2 seconds of ArcHudVocRadiationCritical)
                        StopLastPlayedSound();
                        //PlaySound(Radiation, (float)intensity * 0.4f);

                        PlayRadiationTicks(2, (float)intensity);

                        //this would be where we trigger our visual effects sub if any scaled by distance to edge

                    }
                    else RadioactiveV = false; //not a buffer no visual effects

                    /* old logic remove later once testing passes
                     * old logic allows for individual facing range definition
                     * replaced by cubesize but noted in case behaviour needs to be
                     * re-added
                    if (X <= GWP && X < Y && X < Z) { Zone = GWD; Target = GW; return true; }
                    if (X >= GEP && X > Y && X > Z) { Zone = GED; Target = GE; return true; }
                    if (Y <= GSP && Y < X && Y < Z) { Zone = GSD; Target = GS; return true; }
                    if (Y >= GNP && Y > X && Y > Z) { Zone = GND; Target = GN; return true; }
                    if (Z <= GDP && Z < X && Z < Y) { Zone = GDD; Target = GD; return true; }
                    if (Z >= GUP && Z > X && Z > Y) { Zone = GUD; Target = GU; return true; }
                    */
                }



                // Reset flags if no LCDs or boundaries detected
                // checking updatelist.count may be redundant as we would have already returned if there was any exits
                // only the updatelist2 (station LCDs) really matters here.
                // checking quiet is not true skips check 
                if (updatelist.Count == 0 && updatelist2.Count == 0 && !quiet)
                {
                    noZone = true;
                    if (ClearSeenState) { seenPopup = false; }
                    lastStationId = 0; // Clear station tracking
                                       // OverrideArmed = false; // we moved away from a grid with an [override] LCD
                    Zone = "Scanning...";
                    Target = "none";
                }
                //regardless of finding station LCDs or not, return.  If we found exit LCD dest/edge we already returned by now
                //unless quiet was set (because we already set/showed it)
                return false;
            }
            //old fell through hole logic disable if not needed later - re-added to troubleshoot loss of intro messages
            //no zone is used to detect if we have left the range of any useful lcds
            //if so reset the flags to reduce processing and to allow more than one gateway station using 
            //different options
            //technically this part will only run if no player has spawned in, or if we are a server.
            //Since single/coop/dedicated games all behave differently we a little escape logic here.
            if (noZone)
            {
                seenPopup = false;
                // OverrideArmed = false; // we moved away from a grid with an [override] LCD? failsafe only.
            }
            else { noZone = true; }

            //fell through a hole
            return false;
        }
        /// <summary>
        /// Change the value/level of one of a players attributes by increase/decrease a given amount.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="amount"></param>
        public static void EffectPlayer(string type = "Radiation", float amount = 0.0f)
        {
            //Assume we only want current player so don't need to collect player ID as input value.
            //But will need to send it to server.
            //Applies changes to characters attributes.  Default Radiation.
            if (type == "Radiation")
            {
                // Apply radiation damage 
                // Step 1: Send request to server (uncomment to test)                
                ulong playerSteamId = MyAPIGateway.Session.Player.SteamUserId;
                string message = $"ApplyChange:Radiation:{playerSteamId}:{amount}";
                MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes(message));
                // MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Radiation message sent to server");

                // Step 1 Local Fallback (uncomment for offline/single-player test)
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    var character = MyAPIGateway.Session.Player.Character;
                    if (character != null)
                    {
                        var comp = character.Components.Get<Sandbox.Game.Components.MyCharacterStatComponent>();
                        //character.Components.Get<MyCharacterStatComponent>();
                        if (comp != null)
                        {
                            //comp.Radiation.Value += damage; // Add exposure (ignores decay/immunity) Method 1 test

                            float before = comp.Radiation.Value;
                            comp.Radiation.Value += amount;
                            float after = comp.Radiation.Value;
                            //MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Direct Radiation.Value before {before:F2} + {damage:F2} = after {after:F2}");

                            //This doesn't seem to work need to fix references. Using simpler accumulate Radiation Value method instead.
                            /* var receiver = comp as IMyHazardReceiver;
                             if (receiver != null)
                             {
                                 MyStringHash radiationId = MyStringHash.GetOrCompute("Radiation");
                                 float amount = damage; // Ensure float
                                 MyStringHash nullDamageType = MyStringHash.NullOrEmpty;
                                 receiver.Apply(radiationId, amount, nullDamageType);
                                 MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Apply Radiation {amount:F2}, total: {comp.Radiation.Value:F2}");
                             }
                             */
                            //MyAPIGateway.Utilities.ShowMessage("Lobby", $"Debug: Direct Radiation.Value += {damage:F2}, total: {comp.Radiation.Value:F2}");
                        }
                    }
                }
                amount = 0; //clear the damage amount afterwards ready for next run.
            }
            else if (type == "Damage") { }   //Effect Health
            else if (type == "Hunger") { }   //Effect Remaining Food bar level
            else if (type == "Energy") { }   //Effect Suit Remaining Power level 
            else if (type == "Hydrogen") { } //Effect Suit Fuel level
            else if (type == "Oxygen") { }   //Effect Suit Oxygen Level
            else { } //Something Unexpected.


            //This code almost works, and needs to be run server side once I get it working
            //single player.
            /*
            var player = MyAPIGateway.Session.Player;
            var character = player.Character;
            var comptest = character.Components.Get<MyCharacterStatComponent>();

            //So?: character.DamageEtity(damage, MyDamageType.Radioactivity);                        
            //Definately broken: MyAPIGateway.Session.Player.Character.DamageEntity(damage, MyDamageType.Radioactivity);
            */

            /*
             * Digi Suggests: to add radiation you need a few steps:
             * var comp = character.Components.Get<MyCharacterStatComponent>();
             * and nullcheck. (presumably to check its not unset)
             * then it has `Radiation` property in it, deeper it has `Value` that can be read or set.
             * ^^^^^ This works mostly, and is reduced by anti-rad shots
             * 
             * Digi Further Adds:
             * Setting Radiation.Value directly is not pausing decay, and also ignores rad immunity of course.
             * For decay to be properly affected and for radiation immunity to be considered: cast the
             * MyCharacterStatComponent to IMyHazardReceiver and call Apply() with MyCharacterStatComponent.RADIATION_ID id
             * and MyStringHash.NullOrEmpty damage type (because damage type is not used for radiation).
             *
             * Need to look into this more when brain not broken.
             */


        }

        /// <summary.
        ///     Create or destroy a GPS point
        /// </summary>
        public static void GPS(double x, double y, double z, string name, string description, bool create, string colour = "Red")
        {
            //make a gps point for the objective.  eg GPS(x,y,z,name,description,true)
            //remove an existing GPS point  eg GPS(x,y,z,name,description,false)
            //Helps Automate process for creating/removing investigate/mine here/kill this/destroy/repair/etc missions markers

            //ye not sure how to assign this as the initialised value in a vector need help :) this is my work around
            Vector3D location = MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetPosition();
            location.X = x; location.Y = y; location.Z = z;

            //MyAPIGateway.Utilities.ShowMessage("Lobby", $"I see [GPS] at {x},{y},{z} Called: {name} Details {description} Tint:{colour}");

            //make sure it doesn't already exist
            bool AlreadyExists = false;
            var list = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId);
            foreach (var gps in list)
            {
                if (gps.Description == description && gps.Name == name && gps.Coords == location)
                {
                    //MyAPIGateway.Utilities.ShowMessage("Lobby", "[GPS] Already Exists");
                    AlreadyExists = true;
                }
            }

            if (create && !AlreadyExists)
            {  //make a new GPS
                var gps = MyAPIGateway.Session.GPS.Create(name, description, location, true, false);
                //gps.GPSColor = Color.Red; // Set to red
                gps.GPSColor = GetColorFromString(colour);
                //MyAPIGateway.Utilities.ShowMessage("Lobby", "I am spawning [GPS]");
                MyAPIGateway.Session.GPS.AddGps(MyAPIGateway.Session.Player.IdentityId, gps);
            }
            else if (!create)
            { //remove a GPS if create is not true 
              //reinitialise list after scanning
              //MyAPIGateway.Utilities.ShowMessage("Lobby", "I am removing [GPS]");
                list = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId);
                foreach (var gps in list)
                {
                    if (gps.Description == description || gps.Name == name || gps.Coords == location)
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(MyAPIGateway.Session.Player.IdentityId, gps);
                    }
                }
            }
            //MyAPIGateway.Utilities.ShowMessage("Lobby", "I am returning from GPS()");
        }


        private static Color GetColorFromString(string colour)
        {
            switch (colour.ToLower())
            {
                case "aliceblue":
                    return Color.AliceBlue;
                case "antiquewhite":
                    return Color.AntiqueWhite;
                case "aqua":
                    return Color.Aqua;
                case "aquamarine":
                    return Color.Aquamarine;
                case "azure":
                    return Color.Azure;
                case "beige":
                    return Color.Beige;
                case "bisque":
                    return Color.Bisque;
                case "black":
                    return Color.Black;
                case "blanchedalmond":
                    return Color.BlanchedAlmond;
                case "blue":
                    return Color.Blue;
                case "blueviolet":
                    return Color.BlueViolet;
                case "brown":
                    return Color.Brown;
                case "burlywood":
                    return Color.BurlyWood;
                case "cadetblue":
                    return Color.CadetBlue;
                case "chartreuse":
                    return Color.Chartreuse;
                case "chocolate":
                    return Color.Chocolate;
                case "coral":
                    return Color.Coral;
                case "cornflowerblue":
                    return Color.CornflowerBlue;
                case "cornsilk":
                    return Color.Cornsilk;
                case "crimson":
                    return Color.Crimson;
                case "cyan":
                    return Color.Cyan;
                case "darkblue":
                    return Color.DarkBlue;
                case "darkcyan":
                    return Color.DarkCyan;
                case "darkgoldenrod":
                    return Color.DarkGoldenrod;
                case "darkgray":
                    return Color.DarkGray;
                case "darkgreen":
                    return Color.DarkGreen;
                case "darkkhaki":
                    return Color.DarkKhaki;
                case "darkmagenta":
                    return Color.DarkMagenta;
                case "darkolivegreen":
                    return Color.DarkOliveGreen;
                case "darkorange":
                    return Color.DarkOrange;
                case "darkorchid":
                    return Color.DarkOrchid;
                case "darkred":
                    return Color.DarkRed;
                case "darksalmon":
                    return Color.DarkSalmon;
                case "darkseagreen":
                    return Color.DarkSeaGreen;
                case "darkslateblue":
                    return Color.DarkSlateBlue;
                case "darkslategray":
                    return Color.DarkSlateGray;
                case "darkturquoise":
                    return Color.DarkTurquoise;
                case "darkviolet":
                    return Color.DarkViolet;
                case "deeppink":
                    return Color.DeepPink;
                case "deepskyblue":
                    return Color.DeepSkyBlue;
                case "dimgray":
                    return Color.DimGray;
                case "dodgerblue":
                    return Color.DodgerBlue;
                case "firebrick":
                    return Color.Firebrick;
                case "floralwhite":
                    return Color.FloralWhite;
                case "forestgreen":
                    return Color.ForestGreen;
                case "fuchsia":
                    return Color.Fuchsia;
                case "gainsboro":
                    return Color.Gainsboro;
                case "ghostwhite":
                    return Color.GhostWhite;
                case "gold":
                    return Color.Gold;
                case "goldenrod":
                    return Color.Goldenrod;
                case "gray":
                    return Color.Gray;
                case "green":
                    return Color.Green;
                case "greenyellow":
                    return Color.GreenYellow;
                case "honeydew":
                    return Color.Honeydew;
                case "hotpink":
                    return Color.HotPink;
                case "indianred":
                    return Color.IndianRed;
                case "indigo":
                    return Color.Indigo;
                case "ivory":
                    return Color.Ivory;
                case "khaki":
                    return Color.Khaki;
                case "lavender":
                    return Color.Lavender;
                case "lavenderblush":
                    return Color.LavenderBlush;
                case "lawngreen":
                    return Color.LawnGreen;
                case "lemonchiffon":
                    return Color.LemonChiffon;
                case "lightblue":
                    return Color.LightBlue;
                case "lightcoral":
                    return Color.LightCoral;
                case "lightcyan":
                    return Color.LightCyan;
                case "lightgoldenrodyellow":
                    return Color.LightGoldenrodYellow;
                case "lightgray":
                    return Color.LightGray;
                case "lightgreen":
                    return Color.LightGreen;
                case "lightpink":
                    return Color.LightPink;
                case "lightsalmon":
                    return Color.LightSalmon;
                case "lightseagreen":
                    return Color.LightSeaGreen;
                case "lightskyblue":
                    return Color.LightSkyBlue;
                case "lightslategray":
                    return Color.LightSlateGray;
                case "lightsteelblue":
                    return Color.LightSteelBlue;
                case "lightyellow":
                    return Color.LightYellow;
                case "lime":
                    return Color.Lime;
                case "limegreen":
                    return Color.LimeGreen;
                case "linen":
                    return Color.Linen;
                case "magenta":
                    return Color.Magenta;
                case "maroon":
                    return Color.Maroon;
                case "mediumaquamarine":
                    return Color.MediumAquamarine;
                case "mediumblue":
                    return Color.MediumBlue;
                case "mediumorchid":
                    return Color.MediumOrchid;
                case "mediumpurple":
                    return Color.MediumPurple;
                case "mediumseagreen":
                    return Color.MediumSeaGreen;
                case "mediumslateblue":
                    return Color.MediumSlateBlue;
                case "mediumspringgreen":
                    return Color.MediumSpringGreen;
                case "mediumturquoise":
                    return Color.MediumTurquoise;
                case "mediumvioletred":
                    return Color.MediumVioletRed;
                case "midnightblue":
                    return Color.MidnightBlue;
                case "mintcream":
                    return Color.MintCream;
                case "mistyrose":
                    return Color.MistyRose;
                case "moccasin":
                    return Color.Moccasin;
                case "navajowhite":
                    return Color.NavajoWhite;
                case "navy":
                    return Color.Navy;
                case "oldlace":
                    return Color.OldLace;
                case "olive":
                    return Color.Olive;
                case "olivedrab":
                    return Color.OliveDrab;
                case "orange":
                    return Color.Orange;
                case "orangered":
                    return Color.OrangeRed;
                case "orchid":
                    return Color.Orchid;
                case "palegoldenrod":
                    return Color.PaleGoldenrod;
                case "palegreen":
                    return Color.PaleGreen;
                case "paleturquoise":
                    return Color.PaleTurquoise;
                case "palevioletred":
                    return Color.PaleVioletRed;
                case "papayawhip":
                    return Color.PapayaWhip;
                case "peachpuff":
                    return Color.PeachPuff;
                case "peru":
                    return Color.Peru;
                case "pink":
                    return Color.Pink;
                case "plum":
                    return Color.Plum;
                case "powderblue":
                    return Color.PowderBlue;
                case "purple":
                    return Color.Purple;
                case "red":
                    return Color.Red;
                case "rosybrown":
                    return Color.RosyBrown;
                case "royalblue":
                    return Color.RoyalBlue;
                case "saddlebrown":
                    return Color.SaddleBrown;
                case "salmon":
                    return Color.Salmon;
                case "sandybrown":
                    return Color.SandyBrown;
                case "seagreen":
                    return Color.SeaGreen;
                case "seashell":
                    return Color.SeaShell;
                case "sienna":
                    return Color.Sienna;
                case "silver":
                    return Color.Silver;
                case "skyblue":
                    return Color.SkyBlue;
                case "slateblue":
                    return Color.SlateBlue;
                case "slategray":
                    return Color.SlateGray;
                case "snow":
                    return Color.Snow;
                case "springgreen":
                    return Color.SpringGreen;
                case "steelblue":
                    return Color.SteelBlue;
                case "tan":
                    return Color.Tan;
                case "teal":
                    return Color.Teal;
                case "thistle":
                    return Color.Thistle;
                case "tomato":
                    return Color.Tomato;
                case "turquoise":
                    return Color.Turquoise;
                case "violet":
                    return Color.Violet;
                case "wheat":
                    return Color.Wheat;
                case "whitesmoke":
                    return Color.WhiteSmoke;
                case "yellow":
                    return Color.Yellow;
                case "yellowgreen":
                    return Color.YellowGreen;
                default:
                    return Color.Red; // Fallback
            }
        }

        /// <summary>
        ///     Triggers the specified sound ID this can be from an audio spc or possibly in-game vanilla sounds if id known.
        ///     Original PLaysound() Developed with assistance of Digi
        /// </summary>
        #region Audio
        void PlaySound(MySoundPair soundPair, float volume = 1f)
        {
            var controlled = MyAPIGateway.Session?.ControlledObject?.Entity;

            if (controlled == null)
                return; // don't continue if session is not ready or player does not control anything.

            if (emitter == null)
                emitter = new MyEntity3DSoundEmitter((MyEntity)controlled);
            else
                emitter.Entity = (MyEntity)controlled;

            emitter.CustomVolume = volume;
            //emitter.StopSound(true); // Clear prior (disable if issues occur)
            emitter.PlaySingleSound(soundPair);
            //emitter.Cleanup();

        }

        void StopLastPlayedSound(bool stopTimer = true)
        {
            //emitter?.StopSound(false); false would instead fade out sound
            if (emitter != null)
            {
                emitter.StopSound(true); // Force immediate stop
                emitter = null; // Clear reference
            }
            if (stopTimer && soundTimer != null)
            {
                soundTimer.Stop();
                //soundTimer.Dispose();
                soundTimer = null;
            }
        }
        /// <summary>
        /// This lets us play the radiation sound.
        /// Shorter intervals / quieter makes it seem less urgent
        /// Medium intervals play a medium speed ticking
        /// Longer intervals / louder allow it to play the full sound file and sound urgent
        /// Radiation file itself is 3 seconds long.  
        /// </summary>
        /// <param name="intervalSeconds"></param>   
        /// <param name="volume"></param>

        private void PlayRadiationTicks(double intervalSeconds = 3, float volume = 0.3f)
        {
            // Optional volume (0.0-2.0)
            if (volume < 0) volume = 0f;
            if (volume > 2) volume = 2f;

            // No interval below 0.05 seconds or above 3 seconds
            if (intervalSeconds < 0.05) intervalSeconds = 0.05;
            if (intervalSeconds > 3) intervalSeconds = 3.0;


            if (soundTimer != null)
            {
                //MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Hey we are clearing Last sound");
                StopLastPlayedSound(true); // Clear prior timer
            }
            //MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Entering Radiation sound module");

            //Start the sound playing.
            PlaySound(Radiation, volume);

            soundTimer = new Timer((int)(intervalSeconds * 1000)); // ms converted to seconds

            //Wait interval to let it play before interrupting
            //This in effect works like its own thread.. so we can multitask. Hopefully.
            soundTimer.Elapsed += (s, e) =>
            {
                StopLastPlayedSound(false); //interrupt sound but not timer
                //MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: End of radiation play");
                //We finished our repeat count. Lets stop the timer.
                soundTimer.Stop();
                soundTimer = null;
            };
            soundTimer.AutoReset = false; // One-shot per Start()
            soundTimer.Start(); //Start up the timer loop
        }

        #endregion Audio

        #region visual effects

        private void DrawStaticStreaks2(float intensity = 1.0f)
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null || player.Character == null)
                return;

            var position = player.Character.GetPosition();
            var forward = player.Character.WorldMatrix.Forward;
            var right = player.Character.WorldMatrix.Right;
            var up = player.Character.WorldMatrix.Up;

            int numStreaks = (int)(intensity * 20) + 5; // 5-25 streaks
            var rand = new Random(counter); // Vary per tick

            MyStringId material = MyStringId.GetOrCompute("WeaponLaser");
            Vector4 streakColor = new Vector4(1f, 1f, 1f, (float)(0.3 + intensity * 0.7)); // White, alpha scales (0.3-1.0)
            float thickness = 0.02f + (float)intensity * 0.03f; // Thinner at low intensity

            for (int i = 0; i < numStreaks; i++)
            {
                // Random position in 50m sphere around player (uniform FOV)
                double radiusRand = rand.NextDouble() * 50.0;
                double thetaRand = rand.NextDouble() * 2 * Math.PI;
                double phiRand = Math.Acos(2 * rand.NextDouble() - 1);
                Vector3D offset = new Vector3D(
                    radiusRand * Math.Sin(phiRand) * Math.Cos(thetaRand),
                    radiusRand * Math.Sin(phiRand) * Math.Sin(thetaRand),
                    radiusRand * Math.Cos(phiRand)
                            );
                Vector3D start = position + offset;

                Vector3D dir;
                if (jumping)
                {
                    // Jumping: Point away from boundary (e.g., reverse forward for GE)
                    dir = -forward * rand.NextDouble() * 5; // Away from boundary, short
                }
                else if (spooling)
                {
                    // Spooling: Spread around crosshair (slight cone)
                    double angleH = (rand.NextDouble() - 0.5) * Math.PI / 6; // ±30° horizontal
                    double angleV = (rand.NextDouble() - 0.5) * Math.PI / 12; // ±15° vertical
                    dir = forward * rand.NextDouble() * 5 + right * Math.Sin(angleH) * 2 + up * Math.Sin(angleV) * 2; // Spread
                }
                else
                {
                    // General: Uniform random direction
                    double theta = rand.NextDouble() * 2 * Math.PI;
                    double phi = rand.NextDouble() * Math.PI;
                    dir = new Vector3D(
                        Math.Sin(phi) * Math.Cos(theta),
                        Math.Sin(phi) * Math.Sin(theta),
                        Math.Cos(phi)
                    ) * rand.NextDouble() * 5; // Short streak (0-5m)
                }

                MyTransparentGeometry.AddLineBillboard(material, streakColor, start, dir, (float)dir.Length(), thickness, BlendTypeEnum.Standard);
            }
        }

        private void DrawStaticStreaks(float intensity = 1.0f)
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null || player.Character == null)
                return;

            var position = player.Character.GetPosition();
            var forward = player.Character.WorldMatrix.Forward;
            var right = player.Character.WorldMatrix.Right;
            var up = player.Character.WorldMatrix.Up;

            int numStreaks = (int)(intensity * 20) + 5; // 5-25 streaks (scale by intensity)
            var rand = new Random(counter); // Vary per tick

            MyStringId material = MyStringId.GetOrCompute("WeaponLaser");
            Vector4 streakColor = new Vector4(1f, 1f, 1f, (float)(0.5 + intensity * 0.5)); // White, alpha scales
            float thickness = 0.02f + (float)intensity * 0.03f; // Thicker with intensity

            for (int i = 0; i < numStreaks; i++)
            {
                double dist = rand.NextDouble() * 50 + 10; // 10-60m ahead
                double angleH = (rand.NextDouble() - 0.5) * Math.PI / 6; // ±30° horizontal
                double angleV = (rand.NextDouble() - 0.5) * Math.PI / 6; // ±30° vertical

                Vector3D start = position + forward * dist + right * Math.Sin(angleH) * dist * 0.1 + up * Math.Sin(angleV) * dist * 0.1;
                Vector3D dir = forward * rand.NextDouble() * 5; // Short streak direction (0-5m)

                MyTransparentGeometry.AddLineBillboard(material, streakColor, start, dir, (float)dir.Length(), thickness, BlendTypeEnum.Standard);
            }
        }

        private void DrawBoundaryLines()
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null || player.Character == null)
                return;

            var position = player.Character.GetPosition();
            double range = CubeSize / 2; // Half cube size from center
            double dominantAxis = Math.Max(Math.Max(Math.Abs(position.X), Math.Abs(position.Y)), Math.Abs(position.Z));
            string facingDirection = "";

            if (Math.Abs(position.X) == dominantAxis)
            {
                facingDirection = position.X > 0 ? "GE" : "GW";
            }
            else if (Math.Abs(position.Y) == dominantAxis)
            {
                facingDirection = position.Y > 0 ? "GN" : "GS";
            }
            else if (Math.Abs(position.Z) == dominantAxis)
            {
                facingDirection = position.Z > 0 ? "GU" : "GD";
            }

            // Nearest face center
            Vector3D faceCenter = position;
            if (facingDirection == "GE")
                faceCenter.X = range; // Positive X face
            else if (facingDirection == "GW")
                faceCenter.X = -range;
            else if (facingDirection == "GN")
                faceCenter.Y = range;
            else if (facingDirection == "GS")
                faceCenter.Y = -range;
            else if (facingDirection == "GU")
                faceCenter.Z = range;
            else if (facingDirection == "GD")
                faceCenter.Z = -range;

            // 4 lines for square face (projected to player view, 50m FOV)
            Vector3D right = player.Character.WorldMatrix.Right;
            Vector3D up = player.Character.WorldMatrix.Up;
            Vector3D faceNear = faceCenter - player.Character.WorldMatrix.Forward * 50.0; // 50m ahead
            Vector3D v1 = faceNear - right * 25 - up * 25; // Bottom-left
            Vector3D v2 = faceNear + right * 25 - up * 25; // Bottom-right
            Vector3D v3 = faceNear + right * 25 + up * 25; // Top-right
            Vector3D v4 = faceNear - right * 25 + up * 25; // Top-left

            MyStringId material = MyStringId.GetOrCompute("WeaponLaser");
            Vector4 lineColor = Color.Yellow.ToVector4();
            float thickness = 0.1f;

            MyTransparentGeometry.AddLineBillboard(material, lineColor, v1, v2, 0, thickness, BlendTypeEnum.Standard);
            MyTransparentGeometry.AddLineBillboard(material, lineColor, v2, v3, 0, thickness, BlendTypeEnum.Standard);
            MyTransparentGeometry.AddLineBillboard(material, lineColor, v3, v4, 0, thickness, BlendTypeEnum.Standard);
            MyTransparentGeometry.AddLineBillboard(material, lineColor, v4, v1, 0, thickness, BlendTypeEnum.Standard);
        }



        #endregion visual effects


        #region command list
        /// <summary>
        ///     Checks command line text for commands to process
        /// </summary>
        private bool ProcessMessage(string messageText) //, out string reply)
        {
            string reply = "";
            string[] split = messageText.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            //var split = messageText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // nothing useful was entered.
            if (split.Length == 0)
                return false;

            #region editconfig
            if (split[0].Equals("/lconfig", StringComparison.InvariantCultureIgnoreCase))
            {
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Access denied: Requires Space Master or higher.");
                    return false;
                }
                else
                {
                    // MyAPIGateway.Utilities.ShowMessage("Config", ShowConfigSummary());
                    ShowConfigSummary(out reply);
                    MyAPIGateway.Utilities.ShowMessage("Config", reply);
                    return true;
                }
            }

            if (split[0].Equals("/ledit", StringComparison.InvariantCultureIgnoreCase))
            {
                //MyAPIGateway.Utilities.ShowMessage("Edit", SpawnConfigLCD());
                //bool result = SpawnConfigLCD(out reply);
                //MyAPIGateway.Utilities.ShowMessage("Edit", reply);
                //return result;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    return SpawnConfigLCD(out reply);
                }
                else
                {
                    MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestLed it:" + MyAPIGateway.Session.Player.SteamUserId));
                    reply = "Request sent to server for /ledit.";
                    return true;
                }

            }
            // ~line 680, in ProcessMessage, replace /saveconfig
            if (split[0].Equals("/lsave", StringComparison.InvariantCultureIgnoreCase))
            {

                if (MyAPIGateway.Session.Player == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Failed: No player.");
                    return false;
                }
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Access denied: Requires Space Master or higher.");
                    return false;
                }

                var sphere = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 9);
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                foreach (var block in LCDlist)
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null && textPanel.CustomName.ToString().IndexOf("[config]", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string text = textPanel.GetText() ?? "";
                        MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes($"SaveConfig:{MyAPIGateway.Session.Player.SteamUserId}:{text}:{textPanel.CubeGrid.EntityId}"));
                        var grid = textPanel.CubeGrid as IMyCubeGrid;
                        var blocks = new List<IMySlimBlock>();
                        if (grid != null) { grid.GetBlocks(blocks, b => b != null); }
                        if (grid != null && blocks.Count == 1) { grid.Close(); }
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Config sent to server for saving.");
                        return true;
                    }
                }
                MyAPIGateway.Utilities.ShowMessage("Lobby", "No [config] LCD found nearby.");
                return true;
            }

            #endregion editconfig

            #region depart
            //jump override to force grid jump from voxel
            //only fires if grid has an lcd near player seated position named [override]
            //should also check if grid is ship or station, it is really only meant for jumping stations out of voxel
            if (split[0].Equals("/override", StringComparison.InvariantCultureIgnoreCase))
            //&& OverrideArmed)
            {
                //moved to depart
                //need to trigger a jumping flag type spool up and countdown but stop counting at 17 sec
                //and start outputting gltiches/text while the "spooling" flag is still set and
                //the scary spinning sound is still playing.

                //This command is for debuging purpose, override should be triggered by access hatch button named [override]
                //since we couldnt get the button to work yet use an [override] lcd instead on voxel trapped encounter grids
                //that need to run an override jump
                //TODO:  an override jump is meant to drain all the power too! plus maybe damage jump drive or reactor a little
                //maybe damage batterys enough to set on fire etc.  Probably should be a random consequence for it

                var sphere = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 80);
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                IMyTextPanel overrideLCD = null;
                foreach (var block in LCDlist)
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null && textPanel.CustomName.ToString().IndexOf("[override]", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        overrideLCD = textPanel;
                        break;
                    }
                }

                //non space master users must have a valid voxel trapped ship with an [override] lcd
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                {
                    //   MyAPIGateway.Utilities.ShowMessage("Lobby", "Access denied: Requires Space Master or higher.");
                    //return true;

                    if (overrideLCD == null)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "No [override] LCD found nearby.");
                        return true;
                    }

                    var grid = overrideLCD.CubeGrid as IMyCubeGrid;
                    if (grid == null || !grid.IsStatic)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Override jumps only work on voxel-trapped ships.");
                        return true;
                    }
                }
                else
                { MyAPIGateway.Utilities.ShowMessage("Lobby", "Overriding Override: Found Space Master or higher."); }

                if (!spooling && !jumping)
                {
                    spoolup = true;
                    jumping = true; // set off the jump countdown
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Danger Jump Integrity protocols disabled. /override to abort. Preparing to jump.");
                    StopLastPlayedSound(); PlaySound(BadJump, 2f);

                }
                else
                {
                    spoolup = false; jumping = false; spooling = false; StopLastPlayedSound();
                    chargetime = 20;
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Aborting Jump Attempt.");
                }

                return true;
            }

            if (split[0].Equals("/depart", StringComparison.InvariantCultureIgnoreCase) || split[0].Equals("/jump", StringComparison.InvariantCultureIgnoreCase))
            {
                //are we doing a jump override
                if (spooling)
                {
                    StopLastPlayedSound();
                    spooling = false;

                    //run the jump and consequences here eg set fire to batteries or power, damage jump drive etc
                    var controlled = MyAPIGateway.Session.ControlledObject;
                    IMyCubeGrid grid = null;

                    if (controlled is Sandbox.ModAPI.Ingame.IMyCockpit)
                    {
                        Sandbox.ModAPI.Ingame.IMyCockpit cockpit = (Sandbox.ModAPI.Ingame.IMyCockpit)controlled;
                        grid = (VRage.Game.ModAPI.IMyCubeGrid)cockpit.CubeGrid; // Get grid from cockpit
                    }
                    else if (controlled is Sandbox.ModAPI.Ingame.IMyCryoChamber)
                    {
                        Sandbox.ModAPI.Ingame.IMyCryoChamber cryoChamber = (Sandbox.ModAPI.Ingame.IMyCryoChamber)controlled;
                        grid = (VRage.Game.ModAPI.IMyCubeGrid)cryoChamber.CubeGrid; // Get grid from cryo
                    }
                    else if (controlled is VRage.Game.ModAPI.IMyCubeGrid)
                    {
                        VRage.Game.ModAPI.IMyCubeGrid directGrid = (VRage.Game.ModAPI.IMyCubeGrid)controlled;
                        grid = directGrid; // Direct grid control
                    }


                    if (grid == null)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: No grid controlled—select a grid block or sit in cockpit/seat.");
                        return true;
                    }


                    double distance;
                    if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) >= MyPromoteLevel.SpaceMaster && split.Length > 1)
                    {
                        if (double.TryParse(split[1], out distance))
                        {
                            // Admin: Use specified distance
                        }
                        else
                        {
                            MyAPIGateway.Utilities.ShowMessage("Lobby", "Debug: Invalid distance—use /override [distance]");
                            return true;
                        }
                    }
                    else
                    {
                        // Player: Random 100-301m
                        var rand = new Random(DateTime.Now.Millisecond);
                        distance = rand.Next(100, 301);
                        // MyAPIGateway.Utilities.ShowMessage("Lobby", $"Warning: Random insys jump override: {distance:F0}m forward");
                    }

                    // Calculate forward offset
                    //Vector3D forwardOffset = grid.WorldMatrix.Forward * distance;
                    //Vector3D newPos = grid.GetPosition() + forwardOffset;
                    // Calculate forward offset (player view)
                    Vector3D playerForward = MyAPIGateway.Session.Player.Character.WorldMatrix.Forward;
                    Vector3D playerUp = MyAPIGateway.Session.Player.Character.WorldMatrix.Up;
                    Vector3D forwardOffset = playerForward * distance;
                    Vector3D newPos = grid.GetPosition() + forwardOffset;

                    // Move grid up 50 meters (may need a voxel check here but 50m is placeholder)
                    // assuming the grid is no more than a few degrees nose down in the dirt.
                    // compensate for its angle to throw it up clear of voxel.
                    if (distance <= 101) { newPos += playerUp * 50.0; }
                    else if (distance >= 200) { newPos += playerUp * 85.0; }

                    // Move (uses server/local fallback)
                    long identityId = MyAPIGateway.Session.Player.IdentityId;
                    LobbyTeleport.RequestAbsoluteTeleport(identityId, new Vector3D(newPos.X, newPos.Y, newPos.Z));

                    // Post-jump reeling – side tilt + secondary rotation (combined safely)
                    LobbyPhysics.ShipStagger(identityId);

                    // Convert to ship if station (for override jumps from voxel)
                    if (grid.IsStatic)
                    {
                        grid.IsStatic = false;
                        //MyAPIGateway.Utilities.ShowMessage("Lobby", "Grid converted to ship post-jump.");
                    }
                    //OverrideArmed = false;

                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Travel: Override initiated jump {distance:F0}m forward to {newPos.X:F0},{newPos.Y:F0},{newPos.Z:F0}");
                    PlaySound(Boom, 2f); spoolup = false; jumping = false; spooling = false; chargetime = 20;
                    return true;
                }
                //not a jump override must be hopping servers then
                else if (Zone != "Scanning..." && Target != "none" && Target != "0.0.0.0:27270" && !jumping && !spoolup)
                {
                    //throw a connection to a foreign server from server ie in lobby worlds or we have moved worlds
                    //MyAPIGateway.Multiplayer.JoinServer(Target);
                    //This is a variant rexxar did to overcome the crash:
                    //need a 25 second delay here.
                    MyAPIGateway.Utilities.ShowMessage("Travel ", "Preparing to depart - /depart again to abort");
                    startTime = DateTime.UtcNow;
                    PlaySound(jumpSoundPair, 2f);
                    jumping = true;
                    lockedtarget = Target;
                    //have to flip a flag to depart so we trigger a sim timer and counter then launch joinserver

                    //JoinServer(Target);

                }
                else if (jumping) { MyAPIGateway.Utilities.ShowMessage("Travel ", "Aborting Travel"); jumping = false; chargetime = 20; StopLastPlayedSound(); }
                return true;
            }
            if (split[0].Equals("/hop", StringComparison.InvariantCultureIgnoreCase))
            {
                //this is ment to be used for testing moving players or grids and server side sync but handy tool for admins too
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "SpaceMaster only");
                    return true;
                }

                // Safety: Character must be loaded (prevents lag/delete accidents)
                if (MyAPIGateway.Session.Player.Character == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Character not loaded – wait and try again");
                    return true;
                }

                if (split.Length < 2)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /hop <distance> OR /hop <x> <y> <z>");
                    return true;
                }

                long identityId = MyAPIGateway.Session.Player.IdentityId;

                if (split.Length == 2)
                {
                    // Forward hop
                    double distance;
                    if (double.TryParse(split[1], out distance) && distance > 0)
                    {
                        LobbyTeleport.RequestHop(identityId, distance);
                        return true;
                    }
                }
                else if (split.Length == 4)
                {
                    // Absolute x y z
                    double x, y, z;
                    if (double.TryParse(split[1], out x) && double.TryParse(split[2], out y) && double.TryParse(split[3], out z))
                    {
                        LobbyTeleport.RequestAbsoluteTeleport(identityId, new Vector3D(x, y, z));
                        MyAPIGateway.Utilities.ShowMessage("Lobby", $"Aye Sir, hopping to {x} {y} {z} coords");
                        return true;
                    }
                }

                MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid: /hop <distance> OR /hop <x> <y> <z>");
                return true;
            }
            #endregion depart

            #region physics
            //note should add a way to use the old style grav well pull maybe as a grav bomb feature
            if (split[0].Equals("/phys", StringComparison.InvariantCultureIgnoreCase))
            {
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster || MyAPIGateway.Session.Player.Character == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "SpaceMaster + character required");
                    return true;
                }
                if (split.Length >= 4 && split[1].Equals("rot", StringComparison.InvariantCultureIgnoreCase))
                {
                    var controlledEntity = MyAPIGateway.Session.Player.Controller?.ControlledEntity?.Entity;
                    var gridBlock = controlledEntity as IMyCubeBlock;
                    var grid = gridBlock?.CubeGrid ?? controlledEntity as IMyCubeGrid;
                    if (grid == null)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "No grid controlled");
                        return true;
                    }

                    Base6Directions.Direction axis;
                    float amount;
                    if (!Enum.TryParse<Base6Directions.Direction>(split[2], true, out axis) || !float.TryParse(split[3], out amount))
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys rot <Forward|Left|Up|...> <amount>");
                        return true;
                    }

                    Vector3D worldAxis;
                    switch (axis)
                    {
                        case Base6Directions.Direction.Forward: worldAxis = grid.WorldMatrix.Forward; break;
                        case Base6Directions.Direction.Backward: worldAxis = -grid.WorldMatrix.Forward; break;
                        case Base6Directions.Direction.Left: worldAxis = grid.WorldMatrix.Left; break;
                        case Base6Directions.Direction.Right: worldAxis = -grid.WorldMatrix.Left; break;
                        case Base6Directions.Direction.Up: worldAxis = grid.WorldMatrix.Up; break;
                        case Base6Directions.Direction.Down: worldAxis = -grid.WorldMatrix.Up; break;
                        default: worldAxis = Vector3D.Zero; break;
                    }

                    if (worldAxis == Vector3D.Zero)
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid axis");
                        return true;
                    }

                    long identityId = MyAPIGateway.Session.Player.IdentityId;
                    LobbyPhysics.ApplyRotation(identityId, worldAxis, amount);

                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Reeling {amount:F1}°/s on {axis}");
                    return true;

                }
                else if (split.Length == 2 && split[1].Equals("stagger", StringComparison.InvariantCultureIgnoreCase))
                {

                    long identityId = MyAPIGateway.Session.Player.IdentityId;
                    LobbyPhysics.ShipStagger(identityId);
                    return true;
                }
                /* else if (split.Length == 7 && split[1].Equals("well", StringComparison.InvariantCultureIgnoreCase))                
                  {
                double x=0, y=0, z=0, radius, strength;
                if (double.TryParse(split[2], out x) &&
                    double.TryParse(split[3], out y) &&
                    double.TryParse(split[4], out z) &&
                    double.TryParse(split[5], out radius) &&
                    double.TryParse(split[6], out strength))
                {
                    Vector3D center = new Vector3D(x, y, z);
                    LobbyPhysics.CreateGravityWell(center, (float)radius, (float)strength);
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Gravity well at {x:F0},{y:F0},{z:F0}");
                    return true;
                }
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid radius or strength");
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "Try failed: Usage: /phys well <x> <y> <z> <radius> <strength>");
                } */
                else if (split.Length >= 5 && split[1].Equals("well", StringComparison.InvariantCultureIgnoreCase))
                {
                    double x = 0, y = 0, z = 0, radius, strength;
                    bool usedPosShortcut = false;

                    // NEW: Handle [pos] and [pos]+<dist>
                    if (split[2].Equals("[pos]", StringComparison.OrdinalIgnoreCase) ||
                        split[2].StartsWith("[pos]+", StringComparison.OrdinalIgnoreCase))
                    {
                        var player = MyAPIGateway.Session.Player;
                        if (player?.Character == null)
                        {
                            MyAPIGateway.Utilities.ShowMessage("Lobby", "No character – can't use [pos]");
                            return true;
                        }

                        Vector3D playerPos = player.GetPosition();
                        Vector3D forward = player.Controller.ControlledEntity.Entity.WorldMatrix.Forward;

                        double offset = 50.0; // default

                        if (split[2].StartsWith("[pos]+") && split[2].Length > 6)
                        {
                            string distStr = split[2].Substring(6); // after "[pos]+"
                            double.TryParse(distStr, out offset);
                        }

                        Vector3D target = playerPos + forward * offset;
                        x = target.X;
                        y = target.Y;
                        z = target.Z;
                        usedPosShortcut = true;
                    }
                    else if (split.Length >= 7) // normal x y z
                    {
                        if (!double.TryParse(split[2], out x) ||
                            !double.TryParse(split[3], out y) ||
                            !double.TryParse(split[4], out z))
                        {
                            MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid x y z");
                            return true;
                        }
                    }
                    else
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid coordinates – use x y z or [pos]");
                        return true;
                    }

                    // Parse radius and strength (index shifts if [pos] used)
                    int radiusIndex = usedPosShortcut ? 3 : 5;
                    int strengthIndex = usedPosShortcut ? 4 : 6;

                    if (split.Length <= strengthIndex ||
                        !double.TryParse(split[radiusIndex], out radius) ||
                        !double.TryParse(split[strengthIndex], out strength))
                    {
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Invalid radius or strength");
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "Try failed: Usage: /phys well <x> <y> <z> <radius> <strength>");
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "        or: /phys well [pos] <radius> <strength>");
                        MyAPIGateway.Utilities.ShowMessage("Lobby", "        or: /phys well [pos]+<dist> <radius> <strength>");
                        return true;
                    }

                    Vector3D center = new Vector3D(x, y, z);
                    LobbyPhysics.CreateGravityWell(center, (float)radius, (float)strength);

                    string posType = usedPosShortcut ? "at player pos" : "at coords";
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"Gravity well {posType} – radius {radius:F0}, strength {strength:F1}");

                    return true;
                }
                else
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys rot <Forward|Left|Up|...> <amount>");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys stagger");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys well <x> <y> <z> <radius> <strength>");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys well [pos] <radius> <strength>");
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Usage: /phys well [pos]+<offset> <radius> <strength>");
                    return true;
                }
            }
            #endregion physics

            #region ver
            //ver reply
            if (split[0].Equals("/ver", StringComparison.InvariantCultureIgnoreCase))
            {

                MyAPIGateway.Utilities.ShowMessage("VER", MyVerReply);
                var credits = MyVerReply +
                    "\nA mod for adding more RPG elements and MMORPG like features.\n" +
                    "Station MOTD, travel between servers, space hazards, territory\n" +
                    "Lobby worlds etc.\n" +
                    "Designer: PhoenixX (aka Space Pirate Captain X)\n" +
                    "Consultants:\n" +
                    "+Midspace (Aka Screaming Angels) A modding community pioneer.\n" +
                    "+Gwindalmir (Aka The other Phoenix)\n" +
                    "+Digi (Hero Coder)\n" +
                    "+Tyrsis (Hero SE Pioneer) for adding connect feature to begin with\n" +
                    "+Anonymous (Current and Former Keen staff)\n" +
                    "Testers:\n" +
                    "+Mr Dj Poker (Aka The Space Raider of Ramblers Federal Sector!  /Aargh!/)\n" +
                    "+Harps (Deep space Explorer of Ramblers Frontier Jupitor Sector)\n " +
                    "Special Mention:\n" +
                    "+Malware, Aragath.\n" +
                    "+'The Order of the Phoenix'(Inside Joke, but hey we got a thumping theme song now..)";
                MyAPIGateway.Utilities.ShowMissionScreen("Credits", "", "Galaxies Project", credits, null, "Cool");
                StopLastPlayedSound();
                PlaySound(Theme, 2f);
                return true;
            }
            #endregion ver

            #region test 
            //This tests scan results and displays what the mod see's
            if (split[0].Equals("/ltest", StringComparison.InvariantCultureIgnoreCase) && split.Length > 1)
            {
                if (split[1].Equals("sound", StringComparison.InvariantCultureIgnoreCase) || split[1].Equals("sound1", StringComparison.InvariantCultureIgnoreCase)) //server jump/frameshift sound
                { StopLastPlayedSound(); PlaySound(jumpSoundPair, 2f); }
                else if (split[1].Equals("sound2", StringComparison.InvariantCultureIgnoreCase)) //woop woop alert siren
                { StopLastPlayedSound(); PlaySound(WoopSoundPair, 2f); }
                else if (split[1].Equals("sound0", StringComparison.InvariantCultureIgnoreCase)) //whatever sound i want to test
                {
                    StopLastPlayedSound(); PlaySound(SoundTest, 2f);  //edit sound id in global declarations                  
                }
                else if (split[1].Equals("sound6", StringComparison.InvariantCultureIgnoreCase)) //big boom/bang sound
                { StopLastPlayedSound(); PlaySound(Boom, 2f); }
                else if (split[1].Equals("sound4", StringComparison.InvariantCultureIgnoreCase) || split[1].Equals("spoolup", StringComparison.InvariantCultureIgnoreCase)) //whatever sound i want to test
                {
                    StopLastPlayedSound(); PlaySound(BadJump, 2f);  //edit sound id in global declarations                  
                }
                else if (split[1].Equals("sound5", StringComparison.InvariantCultureIgnoreCase) || split[1].Equals("spool", StringComparison.InvariantCultureIgnoreCase)) //whatever sound i want to test
                {
                    StopLastPlayedSound(); if (spooling) { spooling = false; } else spooling = true;
                }
                else if (split[1].Equals("sound3", StringComparison.InvariantCultureIgnoreCase)) //radiation tick sound
                {
                    float volume = 0.5f; // Default quiet
                    double interval = 3; //Default 3 sec

                    if (split.Length > 2 && float.TryParse(split[2], out volume))
                    {
                        // Optional volume (0.0-2.0)
                        if (volume < 0) volume = 0f;
                        if (volume > 2) volume = 2f;
                    }

                    if (split.Length > 3 && double.TryParse(split[3], out interval))
                    {
                        // No interval below 0.05 seconds or above 3 seconds
                        if (interval < 0.05) interval = 0.05;
                        if (interval > 3) interval = 3.0;
                        //interval = Math.Max(0.05, Math.Min(3.0, interval)); // Clamp 0.05-3.0s same as above but unfriendly to read.
                    }

                    //StopLastPlayedSound();  //PlaySound(Radiation, volume);
                    PlayRadiationTicks(interval, volume);
                    MyAPIGateway.Utilities.ShowMessage("Lobby", $"RadiationTick test started: Volume {volume}, Interval {interval}s");
                    return true;  //suppresses echo back in chat
                }


                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Access denied: Requires Space Master or higher.");
                    return false;
                }

                if (split[1].Equals("debug", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Init(); // Force re-initialization (lets not)
                    UpdateLobby(true); // Debug output
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Show Debug messages");
                }
                else if (split[1].Equals("reset", StringComparison.InvariantCultureIgnoreCase))
                {
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Config reset request sent to server (admin only)");
                    MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("ltest reset:" + MyAPIGateway.Session.Player.SteamUserId));
                }

            }
            else if (split[0].Equals("/ltest", StringComparison.InvariantCultureIgnoreCase) && split.Length < 2)
            {
                StopLastPlayedSound(); //Way to reset sound api if needed
                if (SetExits()) { MyAPIGateway.Utilities.ShowMessage("Note:", "Interstellar Space Boundry Detected."); quiet = false; }
                else { MyAPIGateway.Utilities.ShowMessage("Note:", "No Interstellar Space Detected."); } // Shouldn't need SuppressInterStellar=true; unless bad performance
                //var players = new List<IMyPlayer>();
                //MyAPIGateway.Players.GetPlayers(players, p => p != null);
                var updatelist = new HashSet<IMyTextPanel>();
                string reply2 = "";
                if (seenPopup) { reply2 = "Seen popup: true"; } else { reply2 = "seen popup: false"; }
                if (noZone) { reply2 += " no zone: true"; } else { reply2 += " no zone: false"; }
                MyAPIGateway.Utilities.ShowMessage("Lobby", reply2);

                /*
                if (MyAPIGateway.Session.Player != null)
                {
                    string MySenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
                    string MySenderDisplayName = MyAPIGateway.Session.Player.DisplayName;
                }
                */

                var player = MyAPIGateway.Session.Player;
                if (player != null)
                {
                    string namez = "Detected player: " + player.SteamUserId;
                    var sphere = new BoundingSphereD(player.GetPosition(), 9);
                    var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                    string[] LCDTags = new string[] { "[destination]", "(destination)" };
                    foreach (var block in LCDlist)
                    {
                        var textPanel = block as IMyTextPanel;
                        if (textPanel != null && textPanel.IsFunctional && textPanel.IsWorking && LCDTags.Any(tag => textPanel.CustomName?.ToString().IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                        {
                            updatelist.Add(textPanel);
                        }
                    }
                    MyAPIGateway.Utilities.ShowMissionScreen("Online player names", "", "Warning", namez, null, "Close");
                    foreach (var textPanel in updatelist)
                    {
                        var checkArray = textPanel.CustomName.ToString().Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        //string 
                        reply = "";
                        if (checkArray.Length >= 2)
                        {
                            int title = 0; Zone = "";
                            foreach (var str in checkArray)
                            {
                                if (title != 0 && title <= checkArray.Length) { Zone += " " + checkArray[title]; }
                                title++;
                            }
                            reply = "i got Length " + checkArray.Length + " " + checkArray[0] + " " + Zone;
                        }
                        else { reply = " I got less than 2 - so it is invalid"; }
                        MyAPIGateway.Utilities.ShowMessage("TEST", reply);
                    }
                }
                return true;
            }

            #endregion test

            #region help
            // help command
            if (split[0].Equals("/lhelp", StringComparison.InvariantCultureIgnoreCase))
            {
                //dirty check if they are admin
                bool IamTheBoss = false;
                if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
                { IamTheBoss = false; }
                else
                { IamTheBoss = true; }

                if (split.Length <= 1)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Commands: lhelp, depart, ver, ltest");

                    if (IamTheBoss) MyAPIGateway.Utilities.ShowMessage("Lhelp", "Admin Commands: ledit, lsave"); //these should only show to admins
                    reply = "Features: popup, destination";
                    if (IamTheBoss) { reply = reply + ",interstellar, navigation"; } //also admin only things
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", reply);

                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Try '/Lhelp command/feature' for more informations about specific items.");
                    return true;
                }
                else
                {

                    switch (split[1].ToLowerInvariant())
                    {
                        case "lconfig":
                            reply = "Briefly summarises the map exits and settings.\r\n" +
                                "Also includes any nearby stations or departure LCDs.\r\n" +
                                "Informational only.\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /lconfig");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "lconfig command", reply, null, "Close");
                            return true;
                        case "ledit":
                            reply = "Spawns a [config] LCD to define interstellar departure points.\r\n" +
                                 "Edit LCD text for your desired destination server directions.\r\n(Highlight LCD, pres F)\r\n" +
                                 "Also allows you to set cube size of your map and buffer width of edge.\r\n" +
                                 "Cubesize is how far from middle of map to travel to reach a\r\n " +
                                 "departure direction. Buffer is width of border zone that warns\r\n" +
                                 "you are approached edge of interstellar space.\r\n" +
                                 "Type /lsave to confirm changes and write it to the server.\r\n" +
                                 "The LCD is removed once saved.\r\n" +
                                 "This command is only available to admin or space master users.\r\n\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "(Admin only) Example: /ledit");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "ledit command", reply, null, "Close");
                            return true;
                        case "lsave":
                            reply = "Saves your defined interstellar departure point settings/exits\r\n" +
                                "(after using /ledit) from the spawned LCD text box editor.\r\n" +
                                "Removes the Spawned LCD after saving the changes.\r\n" +
                                "This command is only available to admin or space master users.\r\n\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "(Admin only) Example: /lsave");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "lsave command", reply, null, "Close");
                            return true;
                        case "depart":
                            reply = "Player travels to another server world\r\n" +
                                "The world you connect to depends on your location\r\n" +
                                "and if the server is online or not.\r\nAlternate alias: /jump\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /depart");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "depart command", reply, null, "Close");
                            return true;
                        case "ver":
                            reply = "Simply shows an internal reference version number\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /ver");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "ver command", reply, null, "Close");
                            return true;
                        case "ltest":
                            reply = "Simple debug tool for testing the mod scanner/sound subs\r\n" +
                                "'sound' or 'sound2' test plays the sound effects.\r\n" +
                                "no parameter runs a scan test of nearby objects and halts sounds.\r\n";
                            if (IamTheBoss)
                            {
                                reply += "'debug' shows various verbose debug outputs.\r\n" +
                                    "'reset' overwrites the Lobby server side configuration\r\n" +
                                    "with defaults. Handy if new features added after mod update.\r\n" +
                                    "Be Sure to note any important settings with /lconfig or /ledit\r\n" +
                                    "before reset, so you can add them again.\r\n" +
                                    "Note: some or all of these may be removed or disabled" +
                                    "depending on server settings.\r\n";
                            }
                            reply += "parameters: nothing, sound, sound2";
                            if (IamTheBoss) reply += ", debug, reset";
                            reply += "\r\nLHelp Example: /ltest or /ltest sound or /ltest sound2";
                            if (IamTheBoss) reply += " or /ltest debug or /ltest reset"; //should also be admin only
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "ltest command", reply, null, "Close");
                            return true;
                        case "popup":
                            reply = "Displays a popup message to players when they \r\napproach your ship or station. \r\n" +
                                        "Name a LCD: [station] popup\r\n" +
                                        "Any message written in the LCD Public Text box (highlight screen press f)\r\nwill be shown in the popup.\r\n";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "popup", reply, null, "Close");
                            return true;
                        case "destination":
                            reply = "Allows you to define a location as a departure \r\npoint to another server/sector. \r\n" +
                                        "Name an LCD [destination] then put the ip:port of \r\n the server followed by a description \r\nin the public title field.\r\n" +
                                        "Example: 221.121.149.13:28790 Lawless void Sector\r\nWhen near this point you type /depart to travel there.";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "destination", reply, null, "Close");
                            return true;
                        case "interstellar":
                            reply = "The boundry of interstellar space in your region. \r\n" +
                                        "Crossing it players can travel to a server/sector\r\ndefined in that direction.\r\nThis is usually configured by game admin.\r\n" +
                                        "Depending on settings you will need to type /depart \r\nto travel. Boundries default to \r\naround 150000 Kms from centre of map but may vary by server.\r\nThere can be up to 6 directions defined.\r\n\r\n" +
                                        "If you are a server admin, refer to the workshop page.\r\n" +
                                        "But to summarise imagine the map as a cube, each side\r\n" +
                                        "is a different server you could travel to.";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "interstellar", reply, null, "Close");
                            return true;
                        case "navigation":
                            reply = "A set of x y z locations in the /ledit configuration " +
                                "\r\nunder [navigation warnings] heading that will popup " +
                                "\r\na warning if a player gets near them, generate a gps" +
                                "\n\rand play an alert every 15 seconds until they leave." +
                                "\r\nformat: x,y,z radius(in metres) Alert Message." +
                                "\r\nExample: 1234,10000,30000 20000 Danger black hole over here.";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "interstellar", reply, null, "Close");
                            return true;
                        default:
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Unknown option");
                            return true;
                    }
                }
            }
            #endregion help

            //something unexpected return false and get us out of here;
            return false;
        }
        #endregion command list

        // prescans for station popups to get optional range
        private int StationPrescan()
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null)
                return 50;

            // [station] LCDs (scan 200m radius for potential popups)
            var stationSphere = new BoundingSphereD(player.GetPosition(), 200);
            var stationEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref stationSphere);
            string[] stationTags = new string[] { "[station]", "(station)" };

            foreach (var block in stationEntities)
            {
                var textPanel = block as IMyTextPanel;
                if (textPanel != null &&
                    textPanel.IsFunctional &&
                    textPanel.IsWorking &&
                    stationTags.Any(tag => textPanel.CustomName?.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0) &&
                    textPanel.CustomName.Contains("popup"))
                {
                    // Parse custom radius from CustomName (e.g., "[station] popup 65" → 65)
                    var parts = textPanel.CustomName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && parts[1].Equals("popup", StringComparison.OrdinalIgnoreCase))
                    {
                        double radius;
                        if (double.TryParse(parts[2], out radius))
                        {
                            if (radius >= 6 && radius <= 200 && radius > 0)
                            {
                                return (int)radius; // Return parsed radius (cast to int for consistency)
                            }
                        }
                    }
                }
            }
            return 50; // Default if no valid popup LCD found or invalid radius
        }

        #region viewing editing saving and loading config file
        //Grabs all important mod settings and displays it on screen for enduser reference.
        private void ShowConfigSummary(out string reply)
        {
            reply = "";
            StringBuilder summary = new StringBuilder();

            if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
            {
                SaveConfigText(DefaultConfig);
                //[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    // BroadcastConfig(); // Broadcast new default
                }
                else
                {
                    MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestConfig:" + MyAPIGateway.Session.Player.SteamUserId));
                }
            }

            // Config settings
            summary.AppendLine("Configuration Settings:");
            summary.AppendLine($"[NetworkName] {NetworkName}");
            summary.AppendLine($"[AllowDestinationLCD] {AllowDestinationLCD}");
            summary.AppendLine($"[AllowAdminDestinationLCD] {AllowDestinationLCD}");
            summary.AppendLine($"[AllowStationPopupLCD] {AllowStationPopupLCD}");
            summary.AppendLine($"[AllowAdminStationPopup] {AllowAdminStationPopup}");
            summary.AppendLine($"[AllowStationClaimLCD] {AllowStationClaimLCD} (Placeholder)");
            summary.AppendLine($"[AllowStationFactionLCD] {AllowStationFactionLCD} (Placeholder)");
            summary.AppendLine($"[AllowStationTollLCD] {AllowStationTollLCD} (Placeholder)");

            // Interstellar departure points
            summary.AppendLine("\nInterstellar Departure Points:");
            summary.AppendLine($"[cubesize] {CubeSize:F0}m (diameter, boundaries at ±{CubeSize / 2:F0}m)");
            summary.AppendLine($"[edgebuffer] {EdgeBuffer:F0}m");
            if (serverDestinations.Any())
            {
                foreach (var dest in serverDestinations)
                {
                    summary.AppendLine($"{dest.NetworkName} {dest.Address} - {dest.Description}");
                }
            }
            else
            {
                summary.AppendLine("No destinations configured in LobbyDestinations.cfg.");
            }

            // Interstellar boundaries
            summary.AppendLine("\nInterstellar Boundaries:");
            double range = CubeSize / 2; // Use class-level CubeSize
            summary.AppendLine($"[GW] Galactic West: {(GW == "0.0.0.0:0" ? "Not set" : $"{GW} - {GWD} at X={-range:F0}m")}");
            summary.AppendLine($"[GE] Galactic East: {(GE == "0.0.0.0:0" ? "Not set" : $"{GE} - {GED} at X={range:F0}m")}");
            summary.AppendLine($"[GN] Galactic North: {(GN == "0.0.0.0:0" ? "Not set" : $"{GN} - {GND} at Y={range:F0}m")}");
            summary.AppendLine($"[GS] Galactic South: {(GS == "0.0.0.0:0" ? "Not set" : $"{GS} - {GSD} at Y={-range:F0}m")}");
            summary.AppendLine($"[GU] Galactic Up: {(GU == "0.0.0.0:0" ? "Not set" : $"{GU} - {GUD} at Z={range:F0}m")}");
            summary.AppendLine($"[GD] Galactic Down: {(GD == "0.0.0.0:0" ? "Not set" : $"{GD} - {GDD} at Z={-range:F0}m")}");

            // Nearby [station] and [destination] LCDs
            var player = MyAPIGateway.Session.Player;
            if (player != null)
            {
                // [destination] LCDs (9m radius)
                var destSphere = new BoundingSphereD(player.GetPosition(), 9);
                var destLCDs = new HashSet<IMyTextPanel>();
                var destEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref destSphere);
                string[] destTags = new string[] { "[destination]", "(destination)" };
                foreach (var block in destEntities)
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null && destTags.Any(tag => textPanel.CustomName?.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        destLCDs.Add(textPanel);
                    }
                }

                // [station] LCDs (50m radius)
                var stationSphere = new BoundingSphereD(player.GetPosition(), 50);
                var stationLCDs = new HashSet<IMyTextPanel>();
                var stationEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref stationSphere);
                string[] stationTags = new string[] { "[station]", "(station)" };
                foreach (var block in stationEntities)
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null && textPanel.IsFunctional && textPanel.IsWorking && stationTags.Any(tag => textPanel.CustomName?.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        stationLCDs.Add(textPanel);
                    }
                }

                // Append LCD info
                summary.AppendLine("\nNearby LCDs:");
                if (destLCDs.Any() || stationLCDs.Any())
                {
                    foreach (var lcd in destLCDs)
                    {
                        var nameArray = lcd.CustomName.ToString().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        int nameIdx = nameArray[0].IndexOf("[destination]", StringComparison.InvariantCultureIgnoreCase) >= 0 ? 1 : 0;
                        string address = nameArray.Length > nameIdx ? nameArray[nameIdx] : "Unknown";
                        string desc = lcd.GetText() ?? string.Join(" ", nameArray.Skip(nameIdx + 1));
                        summary.AppendLine($"[destination] {address} - {desc} at {lcd.GetPosition():F0}");
                    }
                    foreach (var lcd in stationLCDs)
                    {
                        string popup = lcd.GetText() ?? "No message";
                        summary.AppendLine($"[station] {lcd.CustomName} - {popup} at {lcd.GetPosition():F0}");
                    }
                }
                else
                {
                    summary.AppendLine("No [station] or [destination] LCDs within range.");
                }
            }
            else
            {
                summary.AppendLine("\nNearby LCDs: Player position unavailable.");
            }

            // Caption
            summary.AppendLine("\nInterstellar exit points can be configured by admin with /ledit and /lsave by editing the text on the LCD that will spawn, /lhelp for more details.");

            // Display in a mission screen
            MyAPIGateway.Utilities.ShowMissionScreen("Lobby Config Summary", "", "", summary.ToString(), null, "Close");
            reply = "Config summary displayed.";
        }

        //Spawn in an LCD for editing exits
        private bool SpawnConfigLCD(out string reply)
        {
            reply = "";
            if (MyAPIGateway.Session.GetUserPromoteLevel(MyAPIGateway.Session.Player.SteamUserId) < MyPromoteLevel.SpaceMaster)
            {
                reply = "Access denied: Requires Space Master or higher.";
                return false;
            }
            var player = MyAPIGateway.Session.Player;
            if (player == null) { reply = "Failed: No player."; return false; }
            var character = player.Character;
            if (character == null) { reply = "Failed: No controlled entity."; return false; }

            // Spawn 2m forward, 1m above eye level            
            Vector3D position = character.GetPosition() + character.WorldMatrix.Forward * 2 + character.WorldMatrix.Up * 0.5;
            Vector3D forward = -character.WorldMatrix.Forward; // Face towards player
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
            if (spawnedGrid == null) { reply = "Failed to spawn LCD: Obstruction detected."; return false; }
            var textPanel = spawnedGrid.GetCubeBlock(new Vector3I(0, 0, 0))?.FatBlock as IMyTextPanel;
            if (textPanel != null)
            {
                textPanel.CustomName = "[config]";
                textPanel.WriteText(LoadConfigText());
                textPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                textPanel.FontSize = 1f;
                textPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
            }
            reply = "Config LCD spawned. Interact (F key) to edit, then use /lsave.";
            return true;
        }


        private string LoadConfigText()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return DefaultConfig;
                //[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]";
            }
            catch (Exception e) { MyAPIGateway.Utilities.ShowMessage("Lobby", $"Config load error: {e.Message}"); return ""; }
        }

        private void SaveConfigText(string text)
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
                {
                    writer.Write(text);
                }
                ParseConfigText(text);
                UpdateLobby(false);
            }
            catch (Exception e) { MyAPIGateway.Utilities.ShowMessage("Lobby", $"Config save error: {e.Message}"); }
        }
        #endregion viewing editing saving and loading config file

        private void HandleServerNavWarnings(byte[] data)
        {
            try
            {
                var received = MyAPIGateway.Utilities.SerializeFromBinary<List<NavigationWarning>>(data);
                navigationWarnings.Clear();
                navigationWarnings.AddRange(received);
                MyAPIGateway.Utilities.ShowMessage("Lobby", $"Received {navigationWarnings.Count} nav warnings from server");
            }
            catch { }
        }
        private void ParseConfigText(string text)
        {
            serverDestinations.Clear();
            navigationWarnings.Clear(); // Clear warnings list
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inNavigationWarnings = false;
            bool inGlobalGPS = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                #region disabled nav haz list creator

                //Nav warning logic - pulled from server side now at LobbyServer.ParseNavigationWarningsServer();
                //Original logic retained in case needed to rework server hazardlist builder
                /*
                if (trimmed.StartsWith("[Navigation Warnings]"))
                {
                    inNavigationWarnings = true;
                    continue;
                }
                else if (inNavigationWarnings && trimmed.StartsWith("[") && !trimmed.StartsWith("[Navigation Warnings]"))
                {
                    inNavigationWarnings = false; // End section
                }
                else if (inNavigationWarnings && trimmed.Length > 0)
                {
                    // Parse "x,y,z radius message"
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string coords = parts[0];
                        string radiusStr = parts[1];
                        string message = string.Join(" ", parts.Skip(2));

                        string type = "General"; // Default
                        //X,Y,Z Radius Long Description
                        if (parts[2].ToLower() == "r" || parts[2].ToLower() == "radiation")
                        {
                            type = "Radiation"; //Radiactive Zone
                            message = string.Join(" ", parts.Skip(3)); // Shift message if type present
                            //X,Y,Z Radius Radiation Long Description
                            //X,Y,Z Radius Radiation Anomaly Long Description
                            //Normal is random radiation/visuals scaled from centre.
                            //Anomaly has reduced radiation in middle
                        }
                        else if (parts[2].ToLower() == "b" || parts[2].ToLower() == "blackhole")
                        {
                            type = "Blackhole"; //deadly gravity well
                            message = string.Join(" ", parts.Skip(4)); // Shift message if type present
                            //X,Y,Z Radius Blackhole pull_power Long Description
                            //X,Y,Z Radius Blackhole Anomaly pull_power Long Description
                            //Anomaly traps you in Event Horizon (30% from centre minimal damage) exiting event horizon one way teleports to in other side
                            //Non Anomaly centre crushes you to death, event horizon inescapable (30% from centre scaling damage) random at-radius teleports

                        }
                        else if (parts[2].ToLower() == "w" || parts[2].ToLower() == "whitehole")
                        {
                            type = "Whitehole"; //wormhole
                            message = string.Join(" ", parts.Skip(3)); // Shift message if type present
                            //X,Y,Z Radius Whitehole pull_power X,Y,Z(Target) Long Description
                            //X,Y,Z Radius Whitehole Anomaly pull_power Radius(random teleport) Long Description
                            //Minor Damage until event horizon, centre teleports you away and adds stagger and velocity
                        }
                        else if (parts[2].ToLower() == "e" || parts[2].ToLower() == "eject")
                        {
                            type = "Ejector";   //repulsor
                            message = string.Join(" ", parts.Skip(4)); // Shift message if type present
                            //x,y,z Radius Eject repulse_power long description
                            //x,y,z radius Eject Anomaly repulse_power long description
                            //no damage just push away
                        }

                        double x; double y; double z; double radius; //location position and size
                        double pullpower = 0; //how much pull/push power if applicable
                        string coords2 = ""; //destination xyz raw string
                        double Ex = 0; double Ey = 0; double Ez = 0; double Eradius = 0; //exit coords and radius for Eject Zone if any

                        //so we havwe parts[0]; split by space,
                        //if it is a black hole then parts[0] is xyz, parts[1] is radius, parts[2] is Blackhole/B, parts[3] is pullpower or anomaly, parts[4] is pullpower or part of description
                        //if it is a white hole then parts[0] is xyz, parts[1] is radius, parts[2] is whitehole/w, parts[3] is pullpower, parts[4] is exit x,y,z, part[5]+ is long description
                        //if it is a white hole then parts[0] is xyz, parts[1] is radius, parts[2] is whitehole/w, parts[3] is anomaly, parts[4] is pullpower, part[5] is teleradius, part[6]+ is long description
                        //if it is a eject repulsor then parts[0] is xyz, parts[1] is radius, parts[2] is Ejector/E, parts[3] is repulse pullpower or anomaly, parts[4] is repulse pullpower or part of description
                        if (type == "Blackhole")
                        {
                            if (parts[3].ToLower() == "anomaly") { double.TryParse(parts[4], out pullpower); message = string.Join(" ", parts.Skip(5)); } //adjust description and power input fields if type anomaly
                            else { double.TryParse(parts[3], out pullpower); } //otherwise the description was already set earlier so just populate power setting 
                        }
                        if (type == "Whitehole")
                        {
                            //if it is a random type populate radius to kick victims to
                            if (parts[3].ToLower() == "anomaly") { double.TryParse(parts[4], out pullpower); double.TryParse(parts[5], out Eradius); message = string.Join(" ", parts.Skip(6)); }
                            else
                            { // if it is a fixed destination type generate an ejector nav hazard at the exit point to add to list based on whitehole details, flip power backwards
                                double.TryParse(parts[3], out pullpower); coords2 = parts[4];
                                var coordExit = coords2.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                if (coordExit.Length == 3 && double.TryParse(coordExit[0], out Ex) &&
                                    double.TryParse(coordExit[1], out Ey) &&
                                    double.TryParse(coordExit[2], out Ez) &&
                                    double.TryParse(coordExit[3], out radius))
                                {
                                    navigationWarnings.Add(new NavigationWarning
                                    {
                                        X = Ex,
                                        Y = Ey,
                                        Z = Ez,
                                        Radius = radius / 2,
                                        Message = "Wormhole Exit",
                                        Type = "Ejector",
                                        Power = -pullpower,
                                        ExitRadius = 0,
                                        ExitX = 0,
                                        ExitY = 0,
                                        ExitZ = 0
                                    }); 
                                }
                            }
                        }

                        //this is suboptimal, should rework in the if style below to sanity check values and fail if bad instead - mostly for reference to organise logic in head
                        if (type == "Ejector")
                        {
                            if (parts[3].ToLower() == "anomaly") { double.TryParse(parts[4], out pullpower); message = string.Join(" ", parts.Skip(5)); } //adjust description and power input fields if type anomaly
                            else { double.TryParse(parts[3], out pullpower); } //otherwise the description was already set earlier so just populate power setting 
                            pullpower = -pullpower; //flip power negative for ejector
                        }

                        //Finally We add hazard to Nav Hazard list as appropriate
                        var coordParts = coords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (coordParts.Length == 3 && double.TryParse(coordParts[0], out x) &&
                            double.TryParse(coordParts[1], out y) &&
                            double.TryParse(coordParts[2], out z) &&
                            double.TryParse(radiusStr, out radius) && radius > 0)
                        {
                            navigationWarnings.Add(new NavigationWarning
                            {
                                X = x,
                                Y = y,
                                Z = z,
                                Radius = radius,
                                Message = message,
                                Type = type,
                                Power = pullpower,
                                ExitRadius = Eradius,
                                ExitX = Ex,
                                ExitY = Ey,
                                ExitZ = Ez
                            });
                        } 
                    }
                } */
                #endregion disabled nav haz list creator

                //global GPS logic
                if (trimmed.StartsWith("[GPS]"))
                {
                    inGlobalGPS = true;
                    // debug message MyAPIGateway.Utilities.ShowMessage("Lobby", "I see [GPS]");
                    continue;
                }
                else if (inGlobalGPS && trimmed.StartsWith("[") && !trimmed.StartsWith("[GPS]"))
                {
                    inGlobalGPS = false; // End section
                    //MyAPIGateway.Utilities.ShowMessage("Lobby", "I DONT see [GPS]");
                }
                else if (inGlobalGPS && trimmed.Length > 0)
                {
                    // Parse "x,y,z color "name" description"
                    var quoteParts = trimmed.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries);
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string quotedName = ""; //declare here so we can use it in if/else later without scope/undef error
                    string description = "Server GPS Point"; //Default Description for lazy admins.

                    if (parts.Length >= 3)
                    {
                        //MyAPIGateway.Utilities.ShowMessage("Lobby", "I see [GPS] settings");
                        string coords = parts[0];
                        string colorName = parts[1];
                        //first try to assign the space seperated text as description in case we have no quoted gps name                     
                        if (parts.Length >= 3) description = string.Join(" ", parts.Skip(3));
                        //if we have at least a quoted gps name use that for name, regardless of if they included a long description
                        //and try to get description from past the second quote if anything exists there.
                        if (quoteParts.Length >= 2) { quotedName = quoteParts[1]; if (quoteParts.Length > 2) description = quoteParts[2].Trim(); } else { quotedName = parts[2]; }


                        //never actually runs now since any " prevent " making it into quotedName to remove
                        //if (quotedName.StartsWith("\"") && quotedName.EndsWith("\""))
                        //{
                        //   quotedName = quotedName.Substring(1, quotedName.Length - 2); // Remove quotes
                        // }

                        var coordParts = coords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double x, y, z;
                        if (coordParts.Length == 3 && double.TryParse(coordParts[0], out x) &&
                            double.TryParse(coordParts[1], out y) &&
                            double.TryParse(coordParts[2], out z))
                        {
                            //MyAPIGateway.Utilities.ShowMessage("Lobby", "I am adding [GPS] to list");
                            globalGPS.Add(new GlobalGPS
                            {
                                X = x,
                                Y = y,
                                Z = z,
                                ColorName = colorName,
                                Name = quotedName,
                                Description = description
                            });
                        }
                    }
                }

                //all other settings logic
                if (trimmed.StartsWith("[GE]") || trimmed.StartsWith("[GW]") || trimmed.StartsWith("[GN]") ||
                    trimmed.StartsWith("[GS]") || trimmed.StartsWith("[GU]") || trimmed.StartsWith("[GD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 2 && parts[1] != "0.0.0.0:0") // Skip if no valid address
                    {
                        string address = parts[1];
                        string networkName = parts[0].ToUpper();
                        string description = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : GetDefaultDescription(networkName);
                        serverDestinations.Add(new Destination
                        {
                            Address = address,
                            NetworkName = networkName,
                            Description = description
                        });
                        // debug MyAPIGateway.Utilities.ShowMessage("Lobby", $"Added destination: {networkName} {address} - {description}");
                    }
                }
                else if (trimmed.StartsWith("[cubesize]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    double parsedSize = CubeSize; // Initialize with class-level default
                    //if a value has been specified try to get a number out of it
                    if (parts.Length > 1 && double.TryParse(parts[1], out parsedSize))
                    {
                        //if the value is invalid (zero or negative number) set it to zero.
                        if (parsedSize <= 0)
                        {
                            //Set this 0 when invalid so supress remains on, otherwise next run may accidentally turn it on
                            //when the value is invalid. In effect 0 boundry disables interstellar exits.
                            CubeSize = 0; //150000000.0; // Default
                            quiet = true; // Disable potential boundaries nag message off invalid boundries
                            SuppressInterStellar = true;  // Assume we should disable with wrongly configured interstellar settings.
                        }
                        else
                        {
                            //If the value is valid or above zero use that value.
                            CubeSize = parsedSize;
                            quiet = false; // Enable boundaries nag message if applicable to current player position
                            SuppressInterStellar = false; // let interstellar exits be enabled.
                        }
                    }
                    //if [cubesize] was specified, but has NO value keep class default.
                    //just in case our file was missing a value somehow (class defined as 150000000 for CubeSize)
                    //This is probably not necessary but makes sure Suppress is turned off at this point in case 
                    //a config reset was recently ran.
                    else { SuppressInterStellar = false; }
                }
                else if (trimmed.StartsWith("[edgebuffer]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    double parsedBuffer = EdgeBuffer; // Initialize with class-level default
                    if (parts.Length > 1 && double.TryParse(parts[1], out parsedBuffer))
                    {
                        //if interstellar space is 0 or invalid make edgebuffer 0 so we dont end up with a calculated 0 boundry minus edge buffer value.
                        //to avoid for example an edgebuffer value of -2000
                        if (parsedBuffer < 0 || !SuppressInterStellar)
                        {
                            EdgeBuffer = 1001; // 2000.0; // Default, or set to 0 to disable buffers
                            //using 1001 as debug value
                        }
                        else
                        {
                            EdgeBuffer = parsedBuffer;
                        }
                    }
                }
                else if (trimmed.StartsWith("[NetworkName]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        NetworkName = string.Join(" ", parts.Skip(1)); // Allow spaces in NetworkName
                    }
                    else
                    {
                        NetworkName = "";
                    }
                }
                else if (trimmed.StartsWith("[ServerPasscode]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        ServerPasscode = parts[1];
                    }
                    else
                    {
                        ServerPasscode = "";
                    }
                }
                else if (trimmed.StartsWith("[AllowDestinationLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowDestinationLCD = true; // Initialize default
                    if (parts.Length > 1)
                    {
                        bool.TryParse(parts[1], out AllowDestinationLCD);
                    }
                }
                else if (trimmed.StartsWith("[AllowAdminDestinationLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowAdminDestinationLCD = true;
                    if (parts.Length > 1)
                        bool.TryParse(parts[1], out AllowAdminDestinationLCD);
                }
                else if (trimmed.StartsWith("[AllowStationPopupLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowStationPopupLCD = true; // Initialize default
                    if (parts.Length > 1)
                    {
                        bool.TryParse(parts[1], out AllowStationPopupLCD);
                    }
                }
                else if (trimmed.StartsWith("[AllowAdminStationPopup]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowAdminStationPopup = true;
                    if (parts.Length > 1)
                        bool.TryParse(parts[1], out AllowAdminStationPopup);
                }
                else if (trimmed.StartsWith("[AllowStationClaimLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowStationClaimLCD = true; // Initialize default
                    if (parts.Length > 1)
                    {
                        bool.TryParse(parts[1], out AllowStationClaimLCD);
                    }
                }
                else if (trimmed.StartsWith("[AllowStationFactionLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowStationFactionLCD = true; // Initialize default
                    if (parts.Length > 1)
                    {
                        bool.TryParse(parts[1], out AllowStationFactionLCD);
                    }
                }
                else if (trimmed.StartsWith("[AllowStationTollLCD]"))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    AllowStationTollLCD = true; // Initialize default
                    if (parts.Length > 1)
                    {
                        bool.TryParse(parts[1], out AllowStationTollLCD);
                    }
                }
            }

            // === Navigation Warnings – now synced from server (or copied from LobbyServer) ===
            // Old client parsing removed – single source from server
            if (LobbyServer.ServerNavigationWarnings != null && LobbyServer.ServerNavigationWarnings.Count > 0)
            {
                navigationWarnings.Clear();
                navigationWarnings.AddRange(LobbyServer.ServerNavigationWarnings);

                //MyAPIGateway.Utilities.ShowMessage("Lobby", $"Loaded {navigationWarnings.Count} navigation warnings from server");
            }
            else
            {
                // MyAPIGateway.Utilities.ShowMessage("Lobby", "No nav warnings from server – using defaults");
            }

            // Update globals with CubeSize
            SetExits();
        }



        private string GetDefaultDescription(string networkName)
        {
            switch (networkName.ToUpper())
            {
                case "[GE]": return "Galactic East";
                case "[GW]": return "Galactic West";
                case "[GN]": return "Galactic North";
                case "[GS]": return "Galactic South";
                case "[GU]": return "Galactic Up";
                case "[GD]": return "Galactic Down";
                default: return "";
            }
        }
    }
}
