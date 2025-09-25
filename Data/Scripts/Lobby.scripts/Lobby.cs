/*
 *  Gateway Lobby Mod 
 *  by PhoenixX (JPC Dev)
 *  For use with Space Engineers Game
 *  Todo: 
 *  instant transfers,  (not urgent)
 *  navigation warnings, (could be fun)
 *  warning approaching interstellar space warning @ 1000 metres (could be fun)
 *  ability to totally disable departure or interstellar prompts etc.
 *  Move depart notifications optionally to use Draygo text hud API mod (767740490) instead of chat. (could be fun)
 *  ship server transfers (help!), 
 *  save/loading settings/destinations server side to prevent player abuse, and only allow admins to link servers - replaces all but navigation/dock/territory and popup LCDs (help!)
 *  configurable interstellar boundry points (they are static at 1000kms currently) (needs save/loading first)
 *  a way to notify owners of a visitor at their station popup (might be fun)
 *  Economy(504209260) API: permission to dock - configurable public/private connectors, guns off/on etc for a fee (need Economy api, may be interesting)
 *  Economy(504209260) API: faction territory, entry taxes, GPS indicators etc (Need Economy API)
 *  Economy(504209260) API: charge for travel
 *  Note to self use Ctrl + K, Ctrl + D to for re-tabbing globally
*/

namespace Lobby.scripts
{
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
    using VRage.Game.Components;
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
    //using Lobby.scripts.ServerThingy??; //Not sure how to reference this to my LobbyServer namespace..
    using ProtoBuf;

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
    }

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LobbyScript : MySessionComponentBase
    {
        int counter = 0;  //Keeps track of how long since the last full run of main processing loop
        bool initDone = false; //Has the script finished loading its business
        bool seenPopup = false; //have we already displayed a popup in this zone?
        public long lastStationId = 0; // Tracks the last station LCD that triggered a popup
        bool noZone = true; //no zone in sight?
        //private bool handlerRegistered = false;
        private Timer initTimer; //timer for pausing init long enough for grids to load in
        public bool quiet = true; // shall we nag the player about intersteller space?
        public bool jumping = false; public int chargetime = 20; public DateTime startTime = DateTime.UtcNow; public string lockedtarget = "";
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


        public string GW = "0.0.0.0:0"; public double GWP = -10000000; //X
        public string GE = "0.0.0.0:0"; public double GEP = 10000000; //X
        public string GS = "0.0.0.0:0"; public double GSP = -10000000; //Y
        public string GN = "0.0.0.0:0"; public double GNP = 10000000; //Y
        public string GD = "0.0.0.0:0"; public double GDP = -10000000; //Z
        public string GU = "0.0.0.0:0"; public double GUP = 10000000; //Z

        public string GWD = "Galactic West";  // -X
        public string GED = "Galactic East";  // +X
        public string GSD = "Galactic South"; // -Y
        public string GND = "Galactic North"; // +Y
        public string GDD = "Galactic Down";  // -Z
        public string GUD = "Galactic Up";  // +Z

        private List<NavigationWarning> navigationWarnings = new List<NavigationWarning>(); // New list for warnings
        private const string MyVerReply = "Gateway Lobby 3.52 (+Navigations Warnings)";  //mod version
        private Dictionary<long, bool> adminCache = new Dictionary<long, bool>(); // Cache for admin status
        private const string CONFIG_FILE = "LobbyDestinations.cfg";
        private const ushort MESSAGE_ID = 12345; // Same ID as server
        public const string DefaultConfig = "[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]\n[Navigation Warnings]\n";
        private List<Destination> serverDestinations = new List<Destination>();

        MyEntity3DSoundEmitter emitter;
        readonly MySoundPair jumpSoundPair = new MySoundPair("IJump");

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
            //if (!handlerRegistered)
            //{
            MyAPIGateway.Utilities.MessageEntered += GotMessage;
            //handlerRegistered = true;
            //initDone = true;
            //}

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

                // Request config from server
                MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("RequestConfig:" + MyAPIGateway.Session.Player.SteamUserId));

                ParseConfigText(LoadConfigText()); // Fallback to local if no server response

                // Check and create default config
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(LobbyScript)))
                {
                    SaveConfigText(DefaultConfig);
                    //"[cubesize] 150000000\n[edgebuffer] 2000\n[NetworkName]\n[ServerPasscode]\n[AllowDestinationLCD] true\n[AllowAdminDestinationLCD] true\n[AllowStationPopupLCD] true\n[AllowAdminStationPopup] true\n[AllowStationClaimLCD] true\n[AllowStationFactionLCD] true\n[AllowStationTollLCD] true\n[GE]\n[GW]\n[GN]\n[GS]\n[GU]\n[GD]");
                }

                //Lets let the user know whats up. 
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
            }
            initDone = true;
        }

        /// <summary>
        ///     Attempts to shut things down neatly when game exits.
        /// </summary>
        protected override void UnloadData()
        {
            //if (handlerRegistered)
            //{
            MyAPIGateway.Utilities.MessageEntered -= GotMessage;
            //   handlerRegistered = false;
            //}
            //initTimer?.Stop();
            //initTimer?.Dispose();
            StopLastPlayedSound(); // Ensure sound cleanup to avoid memory leaks/sound bugs
            //initDone = false;
            //MyAPIGateway.Entities.OnEntityAdd -= entity => { if (entity is IMyCubeGrid) UpdateLobby(false); }; // Cleanup
            base.UnloadData();
            if (!AmIaDedicated())
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MESSAGE_ID, HandleMessage);
            }

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

        }

        /// <summary>
        ///     Main client side processing loop. Runs the whole show.
        /// </summary>
        public override void UpdateAfterSimulation()
        {

            if (!initDone && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                Init();

            //once again, lets not run this bit on a server.. cause that would be dumb
            if (!AmIaDedicated())
            {
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
                        if (Target == "0.0.0.0:0" || Target == "none")
                        { reply = "Warning: You have reached the edge of " + Zone + " Interstellar Space"; }
                        else { reply = Zone + " [Type /depart to travel]"; }
                        if (!jumping) MyAPIGateway.Utilities.ShowMessage("Departure point", reply);
                    }
                    else
                    {
                        Zone = "Scanning...";
                        Target = "none";
                    }
                    /*
                    string reply = "";
                    if (Target == "0.0.0.0:27270" || Target == "none") { reply = "Warning: You have reached the edge of " + Zone + " Interstellar Space"; }
                    else { reply = Zone + " [Type /depart to travel]"; }

                    if (!jumping) MyAPIGateway.Utilities.ShowMessage("Departure point", reply);
                     */
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
                        chargetime--;
                        reply = $"Charging {chargetime}";
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
            }
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
            GWD = "Galactic West"; GED = "Galactic East"; GND = "Galactic North";
            GSD = "Galactic South"; GUD = "Galactic Up"; GDD = "Galactic Down";

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
                string[] stationTags = new string[] { "[station]", "(station)" };
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
                                    MyAPIGateway.Utilities.ShowMissionScreen("Station", "", "Warning", popup, null, "Close");
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
                            }
                        }
                    }
                    // this was here for a reason but debug code not using it atm. else { break; }
                }

                //[Navigation Warnings] logic
                //If it is first time pop up warning; set seen flag
                //If it is second time only show console message
                //If no warnings are in current location clear the seen flag.
                //This will also suppress popups if you just saw a station or claim popup, but still show a warning in chat.
                //Potentially may be worth letting multiple trigger in the case of nested warnings eg 100km caution, 50km warning, final 10km goodbye message
                
                //Need to add a sound effect for this, maybe also check for Draygo HUD API and use that instead/too
                bool ClearSeenState = true;   //Default that we didn't just see a warning, so safe to clear seenPopup
                foreach (var warning in navigationWarnings)
                {
                    if (!seenPopup && Vector3D.Distance(position, new Vector3D(warning.X, warning.Y, warning.Z)) <= warning.Radius)
                    {
                        MyAPIGateway.Utilities.ShowMissionScreen("Navigation", "", "Warning", warning.Message, null, "Close");
                        seenPopup = true; //no other popups recently shown except this one 
                        ClearSeenState = false;
                        //MyAPIGateway.Utilities.ShowMessage("Lobby", $"Navigation warning triggered: {warning.Message}");
                        break; // Only show one at a time (Disable if need to show multiple)
                    }
                    else if (seenPopup && Vector3D.Distance(position, new Vector3D(warning.X, warning.Y, warning.Z)) <= warning.Radius)
                    {
                        //popups recently seen somewhere so just use a less annoying chat warning this time.                        
                        MyAPIGateway.Utilities.ShowMessage("Navigation", $"Warning: {warning.Message}");
                        ClearSeenState = false;
                        break;  // Only show one at a time (Disable if need to show multiple)
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
                // SupressInterStellar supresses interstellar logic if the boundry is invalid.
                if (!quiet || !SuppressInterStellar)
                {
                    double X = position.X; double Y = position.Y; double Z = position.Z;
                    double range = CubeSize / 2; // Half cube size from center
                    double buffer = EdgeBuffer; // Use class-level EdgeBuffer

                    // For now, check if beyond range (future: subtract buffer for warnings)
                    if (X <= -range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GWD; Target = GW; return true; }
                    if (X >= range && Math.Abs(X) > Math.Abs(Y) && Math.Abs(X) > Math.Abs(Z)) { Zone = GED; Target = GE; return true; }
                    if (Y <= -range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GSD; Target = GS; return true; }
                    if (Y >= range && Math.Abs(Y) > Math.Abs(X) && Math.Abs(Y) > Math.Abs(Z)) { Zone = GND; Target = GN; return true; }
                    if (Z <= -range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GDD; Target = GD; return true; }
                    if (Z >= range && Math.Abs(Z) > Math.Abs(X) && Math.Abs(Z) > Math.Abs(Y)) { Zone = GUD; Target = GU; return true; }


                    /* old logic remove later once testing passes
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
            if (noZone) { seenPopup = false; }
            else { noZone = true; }

            //fell through a hole
            return false;
        }

        /// <summary>
        ///     Triggers the specified sound ID this can be from an audio spc or possibly in-game vanilla sounds if id known.
        ///     Developed with assistance of Digi
        /// </summary>
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
            emitter.PlaySingleSound(soundPair);
            //emitter.Cleanup();
        }

        void StopLastPlayedSound()
        {
            //emitter?.StopSound(false); false would instead fade out sound
            if (emitter != null)
            {
                emitter.StopSound(true); // Force immediate stop
                emitter = null; // Clear reference
            }
        }




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
                // MyAPIGateway.Utilities.ShowMessage("Config", ShowConfigSummary());
                ShowConfigSummary(out reply);
                MyAPIGateway.Utilities.ShowMessage("Config", reply);
                return true;
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
            if (split[0].Equals("/depart", StringComparison.InvariantCultureIgnoreCase) || split[0].Equals("/jump", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Zone != "Scanning..." && Target != "none" && Target != "0.0.0.0:27270" && !jumping)
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
            #endregion depart

            #region ver
            //ver reply
            if (split[0].Equals("/ver", StringComparison.InvariantCultureIgnoreCase))
            {
                //MyVerReply = "Gateway Lobby 3.4 (server side settings)";
                MyAPIGateway.Utilities.ShowMessage("VER", MyVerReply);
                return true;
            }
            #endregion ver

            #region test 
            //This tests scan results and displays what the mod see's
            if (split[0].Equals("/ltest", StringComparison.InvariantCultureIgnoreCase) && split.Length > 1)
            {
                if (split[1].Equals("sound", StringComparison.InvariantCultureIgnoreCase))
                { StopLastPlayedSound(); PlaySound(jumpSoundPair, 2f); }
                else if (split[1].Equals("debug", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Init(); // Force re-initialization (lets not)
                    UpdateLobby(true); // Debug output
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Show Debug messages");
                }
                else if (split[1].Equals("reset", StringComparison.InvariantCultureIgnoreCase))
                {
                    MyAPIGateway.Multiplayer.SendMessageToServer(MESSAGE_ID, Encoding.UTF8.GetBytes("ltest reset:" + MyAPIGateway.Session.Player.SteamUserId));
                    MyAPIGateway.Utilities.ShowMessage("Lobby", "Config reset request sent to server (admin only)");
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
                if (split.Length <= 1)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Commands: lhelp, depart, ver, ltest");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Admin Commands: ledit, lsave"); //these should only show to admins later
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Features: popup, destination, interstellar");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Try '/Lhelp command/feature' for more informations about specific items.");
                    return true;
                }
                else
                {
                    //string helpreply = "";
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
                                "'sound' test plays the jump sound without jumping.\r\n" +
                                "no parameter runs a scan test of nearby objects and halts sounds.\r\n" +
                                "'init' reruns initialisation to debug sync issues.\r\n" +
                                "Note: some or all of these may be removed or disabled.\r\n" +
                                "parameters: nothing, sound, init";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /ltest or /ltest sound or /ltest init"); //should also be admin only
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "test command", reply, null, "Close");
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
                                        "Depending on settings you will need to type /depart \r\nto travel. Boundries are usually \r\naround 1000 Kms from centre of map but may vary by server.\r\nThere can be up to 6 directions defined.\r\n\r\n" +
                                        "If you are a server admin, refer to the workshop page.\r\n" +
                                        "But to summarise imagine the map as a cube, each side\r\n" +
                                        "is a different server you could travel to.";
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
        private void ParseConfigText(string text)
        {
            serverDestinations.Clear();
            navigationWarnings.Clear(); // Clear warnings list
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inNavigationWarnings = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                //Nav warning logic
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
                    if (parts.Length >= 5)
                    {
                        string coords = parts[0];
                        string radiusStr = parts[1];
                        string message = string.Join(" ", parts.Skip(2));

                        double x; double y; double z; double radius;

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
                                Message = message
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
                        if (parsedBuffer <= 0 || !SuppressInterStellar)
                        {
                            EdgeBuffer = 0; // 2000.0; // Default, or set to 0 to disable buffers
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
