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
 *  
*/

namespace Economy.scripts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Timers;
    using Sandbox.Common;
    //using Sandbox.Common.ObjectBuilders;
    using Sandbox.Definitions;
    //using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using VRage;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRage.Game.ObjectBuilders.Definitions;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using VRageMath;

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LobbyScript : MySessionComponentBase
    {
        int counter = 0;  //Keeps track of how long since the last full run of main processing loop
        bool initDone = false; //Has the script finished loading its business
        bool instant = false;  //should it auto-depart or wait of the depart command (disabled at the moment)
        bool seenPopup = false; //have we already displayed a popup in this zone?
        bool  noZone = true; //no zone in sight?
        public bool quiet = true; // shall we nag the player about intersteller space?
        public string Zone = "Scanning...";  //placeholder for description of target server
        public string Target = "none"; //placeholder for server address of target server

        public string GW = "0.0.0.0:27270"; public double GWP = -10000000; //X
        public string GE = "0.0.0.0:27270"; public double GEP = 10000000; //X
        public string GS = "0.0.0.0:27270"; public double GSP = -10000000; //Y
        public string GN = "0.0.0.0:27270"; public double GNP = 10000000; //Y
        public string GD = "0.0.0.0:27270"; public double GDP = -10000000; //Z
        public string GU = "0.0.0.0:27270"; public double GUP = 10000000; //Z

        public string GWD = "Galactic West";  // -X
        public string GED = "Galactic East";  // +X
        public string GSD = "Galactic South"; // -Y
        public string GND = "Galactic North"; // +Y
        public string GDD = "Galactic Down";  // -Z
        public string GUD = "Galactic Up";  // +Z

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
        public void init()
        {
            MyAPIGateway.Utilities.MessageEntered += gotMessage;
            initDone = true;
           

            if (!AmIaDedicated())
            {
                //ok lets warm up the hud.. unless of course we are a server.. which would be stupid
                //no longer using hud to prevent conflicts with other mods
               /* MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Clear();
                MyAPIGateway.Utilities.GetObjectiveLine().Title = "Initialising";
                MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Add("Scanning..");
                MyAPIGateway.Utilities.GetObjectiveLine().Show(); */
            
                //Lets let the user know whats up. 
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "This sector supports gateway stations! Use /Lhelp for details.");
                //MyAPIGateway.Utilities.ShowMessage("Lobby", "Type '/Lhelp' for more informations about available commands.");
                //Triggers the 1 off scan for Interstellar Space boundry definitions to populate the destination list.
                if (setexits()) { MyAPIGateway.Utilities.ShowMessage("Note:", "Interstellar Space Boundry Detected."); quiet = false; } else { MyAPIGateway.Utilities.ShowMessage("Note:", "No Interstellar Space Detected."); }
                 //now user configured - MyAPIGateway.Utilities.ShowMissionScreen("Lobby", "", "Warning", "Welcome to gateway Station.\r\n\r\nPlease enter a shuttle and when indicated on hud..\r\nType /depart to travel to its sector.", null, "Close");
            }
        }

        /// <summary>
        ///     Attempts to shut things down neatly when game exits.
        /// </summary>
        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= gotMessage;
            base.UnloadData();

        }

        /// <summary>
        ///     Main client side processing loop. Runs the whole show.
        /// </summary>
        public override void UpdateAfterSimulation()
        {
            if (!initDone && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                init();

            //once again, lets not run this bit on a server.. cause that would be dumb
            if (!AmIaDedicated() )
            {
                //my dirty little timer loop - fires roughly each 2 seconds
                if (counter >= 900)
                {
                    counter = 0;
                    if (setexits()) { quiet = false; }  //rechecks in case the lcds didnt load in yet or got added
                    if (UpdateLobby())
                    {
                         //if the option for insta teleport is enabled do so on entering a teleport zone.
                        if (instant && Target != "none")
                        { 
                            //MyAPIGateway.Utilities.ShowMessage("Departure point", Target); 
                            //MyAPIGateway.Multiplayer.JoinServer(Target); //  <-- This crashes ??
                            //if (ProcessMessage("/depart")) {  }  //<-- so does this !?
                            //rexxar made a special joinserver use local JoinServer(Target); instead
                        }
                        //else
                        //{ 
                        string reply = "";
                        if (Target == "0.0.0.0:27270" || Target=="none") { reply = "Warning: You have reached the edge of " + Zone + " Interstellar Space"; }
                        else { reply = Zone + " [Type /depart to travel]"; }

                        MyAPIGateway.Utilities.ShowMessage("Departure point", reply); 
                        //}

                    }
                    else { Zone = "Scanning..."; Target = "none"; /* MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = Zone; */ }
                }
                counter++;
            }
            base.UpdateAfterSimulation();
        }

        /// <summary>
        ///     Processes chat text and decides if it should show in game chat or not.
        /// </summary>
        private void gotMessage(string messageText, ref bool sendToOthers)
        {
                // here is where we nail the echo back on commands "return" also exits us from processMessage
                // return true supresses echo back, false allows it.
                if (ProcessMessage(messageText)) { sendToOthers = false; }
        }


        /// <summary>
        ///     Rexxars: IMyMultiplayer.JoinServer called this way to prevent crashes.
        /// </summary>
        /// <param name="ip"></param>
        public static void JoinServer(string ip)
        {
            //Little change to allow instant to work at a later date hopefully.
            MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Multiplayer.JoinServer(ip));
        }

        /// <summary>
        ///     Populates Interstellar Space Boundry Destinations. Returns true if it found any, false if it found none.
        /// </summary>
        public bool setexits() {           
            //this probably should only run once to populate the exit facings in a global variable
            //this file having been populated in or out of the game by a server admin
           //This should really load from a file, which should be sent from server to client. but using sphere+lcd for testing purposes
            Vector3D MapCore = new Vector3D(0);
            var sphere3 = new BoundingSphereD(MapCore, 10000000); //area to scan for LCDs defining interstellar space.  10000 km diameter 

            var LCDlist3 = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere3);
            string ip = "0.0.0.0:27270"; //target server
            string description = ""; //target description
            string[] LCDTags3 = new string[] { "[GW]", "(GW)" , "[GE]", "(GE)", "[GN]", "(GN)", "[GS]", "(GS)", "[GU]", "(GU)", "[GD]", "(GD)" };
            var updatelist3 = new HashSet<IMyTextPanel>(); //list of exit boundries (wall facing checks for interstellar space)


            foreach (var block in LCDlist3) //area block list filtered by LCD type with any of the above tags
            {
                var textPanel = block as IMyTextPanel;
                if (textPanel != null
                    && textPanel.IsFunctional
                    && textPanel.IsWorking
                    && LCDTags3.Any(tag => textPanel.CustomName.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    updatelist3.Add((IMyTextPanel)block); //be more efficient to populate our LCD list here methinks.. instead of scan the list below
                }

            }
            if (!updatelist3.Any()) { return false; } //nothing to do?  Lets stop the madness here.

            //Our filtered LCD list containing only interstellar space definition LCDs
            //for each lcd check its ID tags, and assign the destination server for that exit direction
            foreach (var textPanel in updatelist3)
            {
                //is the data valid
                //bool LCDValid=false;

                // text from lcd title in an array eg [GE] [GS]
                var titleArray = (textPanel.CustomName).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); 
                
                //  text from lcd name in an array eg 0.0.0.0:12345 Nowhere Server 
                var nameArray = (textPanel.GetPublicTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); 

                //Whats the target server and its description
                description = "Interstellar Sector: "; //clear out any old data for later checking in the switch for validity
                if (nameArray.Length >= 1 ) //if its Not at least 1 its invalid. We need at least a server address. 
                {
                    int title = 0; 
                    foreach (var str in nameArray)
                    {
                        if (title != 0 && title <= nameArray.Length) { description += " " + nameArray[title]; } //build the description
                        title++;
                    }
                    ip = nameArray[0]; //this should probably check if ip is valid format but other than exiting current map nothing bad seems to occur if its invalid
                    //description as above

                    //LCDValid = true;
                } else { ip = "0.0.0.0:27270"; } //ok this lcd is invalid - check for default values later in the depart command as a final check 

                //what exit are we setting 
                if (titleArray.Length >= 1) //if its Not at least 1 its invalid.
                {
                    int exitcount = 0; string ExitName = "";
                    foreach (var str in titleArray) //if we have more than 1 facing listed in an lcd we go back and check eg more than one facing goes to a single server
                    {                          
                       //check how many/which facing we are working with
                        if (exitcount+1 <= titleArray.Length)
                        { 
                           ExitName = titleArray[exitcount]; 
                           //ideally the facings eg GWP GEP etc should match the appropriate X or Y or Z of the related LCD but since I
                           //plan to replace LCDs in this part with file operations eventually its a moot point - i may set it in the lcd itself 
                           //for now I am defaulting to 1000 kms
                           //if the description is blank we default to the galactic title from init
                            switch (ExitName.ToUpper())
                            {
                                case "[GW]":
                                    GW = ip; if (description !="") GWD = description; //GWP = -1000000;
                                    break;
                                case "[GE]":
                                    GE = ip; if (description != "") GED = description;  //GEP = 1000000;
                                    break;
                                case "[GN]":
                                    GN = ip; if (description != "") GND = description; //GNP = 1000000;
                                    break;
                                case "[GS]":
                                    GS = ip; if (description != "") GSD = description;  //GSP = -1000000;
                                    break;
                                case "[GU]":
                                    GU = ip; if (description != "") GUD = description; //GUP = 1000000;
                                    break;
                                case "[GD]":
                                    GD = ip; if (description != "") GDD = description; //GDP = -1000000;
                                    break;
                                case "(GW)":
                                    GW = ip; if (description != "") GWD = description; //GWP = -1000000;
                                    break;
                                case "(GE)":
                                    GE = ip; if (description != "") GED = description; //GEP = 1000000;
                                    break;
                                case "(GN)":
                                    GN = ip; if (description != "") GND = description; //GNP = 1000000;
                                    break;
                                case "(GS)":
                                    GS = ip; if (description != "") GSD = description; //GSP = -1000000;
                                    break;
                                case "(GU)":
                                    GU = ip; if (description != "") GUD = description; //GUP = 1000000;
                                    break;
                                case "(GD)":
                                    GD = ip; if (description != "") GDD = description; //GDP = -1000000;
                                    break;
                                default:
                                    break;
                            }
                            
                        } exitcount++;
                    }
                }
            }
            return true;
        }

        /// <summary>
        ///     Checks players surroundings for proximity to departure points and special LCDs
        ///     Returns true if it found any, false if not or invalid.
        /// </summary>
        public bool UpdateLobby()
        {
            //check our position - are we in a hot spot?
            //if (MyAPIGateway.Session.Player?.Controller?.ControlledEntity != null) {
            if (MyAPIGateway.Session.Player.Controller.ControlledEntity != null)
            {
                //hud code, disabled to prevent conflicts with other mods kept for reference
                /* Vector3D position = MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetPosition();
                double X = position.X; double Y = position.Y; double Z = position.Z;
                string whereami = string.Format("[ X: {0:F0} Y: {1:F0} Z: {2:F0} ]", X, Y, Z);
                MyAPIGateway.Utilities.GetObjectiveLine().Title = whereami; */

                //hard coded target list cause I suck at server side datafiles..
                /* if (X >= -100 && X<=100 && Y >= -100 && Y<=100 && Z >= 15 && Z <=25) { Zone = "Lawless void"; Target = "221.121.159.238:27270"; return true; }
                 if (X >= -100 && X <= -10 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Black Talon Sector"; Target = "59.167.215.81:27016"; return true; }
                 if (X >= -100 && X <= -10 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Spokane Survivalist Sector"; Target = "162.248.94.205:27065"; return true; }
                 if (X >= 10 && X <= 100 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Pandora Sector"; Target = "91.121.145.20:27016"; return true; }
                 if (X >= 10 && X <= 100 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Ah The Final Frontier"; Target = "192.99.150.136:27039"; return true; }
                 else { Zone = "Scanning..."; Target = "none"; return false;  }  */


                //rip of lcd server code off economy mod..  all it needs to do is test for a block name and any strings
                //look for an lcd named [destination] then grab the public title and extract a server address and caption
                //display the caption in hud, and set the server address to connect to

                //var players = new List<IMyPlayer>();
                // MyAPIGateway.Players.GetPlayers(players, p => p != null); //dont need list of players unless we are doing seat/cryo allocations when we transfer ships too.
                var updatelist = new HashSet<IMyTextPanel>(); //list of destination lcds
                var updatelist2 = new HashSet<IMyTextPanel>(); //list of popup notification lcds


                var sphere = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 9); //destination lcds
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                string[] LCDTags = new string[] { "[destination]", "(destination)" };

                var sphere2 = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 50); //popup notification lcds
                var LCDlist2 = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere2);
                string[] LCDTags2 = new string[] { "[station]", "(station)" };

                if (!quiet)
                {
                    //check the player location in relation to Intersteller space boundries.
                    Vector3D position = MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetPosition();
                    double X = position.X; double Y = position.Y; double Z = position.Z;

                    //Here we check if a player crossed into intersteller space - and confirm which direction travelled the deepest to work out where to send them
                    //eg say a player is at X= -1000001    Y= 1000000   Z= -1000000   They will be offered travel to Galactic West as that is the highest applicable direction
                    //if in the unlikely event they are at exactly the apex of 3 travel points, eg X=1000000 Y=1000000 Z=1000000  then they are offered the first matching point ie Galactic East
                    if (X <= GWP && X < Y && X < Z) { Zone = GWD; Target = GW; return true; } //if we crossed -X, and -X is the highest depth travelled in that direction
                    if (X >= GEP && X > Y && X > Z) { Zone = GED; Target = GE; return true; } //if we crossed +X, and +X is the highest depth travelled in that direction
                    if (Y <= GSP && Y < X && Y < Z) { Zone = GSD; Target = GS; return true; } //if we crossed -Y, and -Y is the highest depth travelled in that direction
                    if (Y >= GNP && Y > X && Y > Z) { Zone = GND; Target = GN; return true; } //if we crossed +Y, and +Y is the highest depth travelled in that direction
                    if (Z <= GDP && Z < X && Z < Y) { Zone = GDD; Target = GD; return true; } //if we crossed -Z, and -Z is the highest depth travelled in that direction
                    if (Z >= GUP && Z > X && Z > Y) { Zone = GUD; Target = GU; return true; } //if we crossed +Z, and +Z is the highest depth travelled in that direction

                    // Scan for X Y or Z LCDs located parallel to the players X  Y or Z position?
                    // or scan for X Y or Z LCDs with a number we can use as the X Y or Z parallel check with player 
                    //if a player overlaps two facings eg both xy xz or yz  ignore them, or send a warning or use the highest/lowest?
                    //Should add a 1000 metre warning "approaching interstellar space"
                }
                foreach (var block in LCDlist) //destination LCDs
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null
                        && textPanel.IsFunctional
                        && textPanel.IsWorking
                        && LCDTags.Any(tag => textPanel.CustomName.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        //noZone = false;
                        updatelist.Add((IMyTextPanel)block);
                    }

                }

                foreach (var block in LCDlist2) //popup station notification lcds
                {
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null
                    && textPanel.IsFunctional
                    && textPanel.IsWorking
                    && LCDTags2.Any(tag => textPanel.CustomName.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        noZone = false;
                        updatelist2.Add((IMyTextPanel)block);
                    }
                }



                //special option lcds - popup station messages etc?
                foreach (var textPanel in updatelist2)
                {
                    string popup = "";
                    //   var checkArray = (textPanel.GetPublicTitle() + " " + textPanel.GetPrivateTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); //private title removed by keen
                    var checkArray = (textPanel.GetPublicTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    popup = textPanel.GetPublicText();
                    if (checkArray.Length >= 1) //if its Not at least 1 its invalid.
                    {
                        int title = 0;
                        foreach (var str in checkArray)
                        {

                            if (title <= checkArray.Length)
                            {
                                if (!seenPopup && checkArray[title].Equals("popup", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    MyAPIGateway.Utilities.ShowMissionScreen("Station", "", "Warning", (popup + " "), null, "Close");
                                    seenPopup = true;
                                }
                                if (checkArray[title].Equals("instant", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    instant = true;

                                }
                                //any other lcd keyword checks go here?

                            }
                            title++;
                        }
                    }
                    else { break; }
                }

                //destination lcd
                foreach (var textPanel in updatelist)
                {
                    //var checkArray = (textPanel.GetPublicTitle() + " " + textPanel.GetPrivateTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var checkArray = (textPanel.GetPublicTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); //fix for removal of private text by keen
                    if (checkArray.Length >= 2) //if its Not at least 2 its invalid.
                    {
                        int title = 0; Zone = "";
                        foreach (var str in checkArray)
                        {
                            //string reply = "i got " + checkArray.Length + " " + checkArray[0] + " " + checkArray[1];
                            //MyAPIGateway.Utilities.ShowMessage("TEST", reply);
                            if (title != 0 && title <= checkArray.Length) { Zone += " " + checkArray[title]; }
                            title++;
                        }
                        Target = checkArray[0]; return true; //this should probably check if ip is valid format but other than exiting current map nothing bad seems to occur if its invalid
                    }
                    else { return false; }
                }


            }
            //no zone is used to detect if we have left the range of any useful lcds
            //if so reset the option flags to reduce processing and to allow more than one gateway station using 
            //different options
            if (noZone) { seenPopup = false; instant = false; }
            else { noZone = true; }
            return false;
            //we fell through a hole nothing to see here
        }

        #region command list
        /// <summary>
        ///     Checks command line text for commands to process
        /// </summary>
        private bool ProcessMessage(string messageText)
        {
            string[] split = messageText.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // nothing useful was entered.
            if (split.Length == 0)
                return false;

            #region depart
            if (split[0].Equals("/depart", StringComparison.InvariantCultureIgnoreCase) || split[0].Equals("/jump", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Zone != "Scanning..." && Target != "none" && Target != "0.0.0.0:27270")
                {
                    //throw a connection to a foreign server from server ie in lobby worlds or we have moved worlds
                    //MyAPIGateway.Multiplayer.JoinServer(Target);
                    //This is a variant rexxar did to overcome the crash:
                    JoinServer(Target);

                }
                return true;
            }
            #endregion jump

            #region ver
            //ver reply
            if (split[0].Equals("/ver", StringComparison.InvariantCultureIgnoreCase))
            {
                string versionreply = "3.5 " ;
                MyAPIGateway.Utilities.ShowMessage("VER", versionreply);
                return true;
            }
            #endregion ver

            #region test 
            //This tests scan results and displays what the mod see's
            if (split[0].Equals("/ltest", StringComparison.InvariantCultureIgnoreCase))
            {
                if (setexits()) { MyAPIGateway.Utilities.ShowMessage("Note:", "Interstellar Space Boundry Detected."); quiet = false; } else { MyAPIGateway.Utilities.ShowMessage("Note:", "No Interstellar Space Detected."); }
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players, p => p != null);
                var updatelist = new HashSet<IMyTextPanel>();
                string reply2 = "";
                if (seenPopup) { reply2 = "Seen popup: true"; } else { reply2 = "seen popup: false"; }
                if (noZone) { reply2 += " no zone: true"; } else { reply2 += " no zone: false"; }
                if (instant) { reply2 += " instant travel: true"; } else { reply2 += " instant travel: false"; }
                MyAPIGateway.Utilities.ShowMessage("Lobby", reply2);

                /*
                 if (MyAPIGateway.Session.Player != null)
                {
                    string MySenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
                    string MySenderDisplayName = MyAPIGateway.Session.Player.DisplayName;
                }
                */

                int playerno=0;
                string namez = "";
                foreach (var player in players)
                {
                    namez += players[playerno].SteamUserId+" - "+ MyAPIGateway.Session.Player.SteamUserId+"\r\n";
                    playerno++;
                var sphere = new BoundingSphereD(player.GetPosition(), 9);
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                
                string[] LCDTags = new string[] { "[destination]", "(destination)" };
                foreach (var block in LCDlist)
                {
                    
                    var textPanel = block as IMyTextPanel;
                    if (textPanel != null
                        && textPanel.IsFunctional
                        && textPanel.IsWorking
                        && LCDTags.Any(tag => textPanel.CustomName.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        updatelist.Add((IMyTextPanel)block);
                    }
                }
                }

                MyAPIGateway.Utilities.ShowMissionScreen("names", "", "Warning", namez, null, "Close");

                foreach (var textPanel in updatelist)
            { 
              //   var checkArray = (textPanel.GetPublicTitle() + " " + textPanel.GetPrivateTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var checkArray = (textPanel.GetPublicTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string reply="";
                     if (checkArray.Length >= 2) {
                         int title = 0; Zone = "";
                         foreach (var str in checkArray)
                         {
                             if (title != 0 && title <= checkArray.Length) { Zone += " " + checkArray[title]; }
                             title++;
                         } reply = "i got Length " + checkArray.Length + " " + checkArray[0] + " " + Zone;
                     }  else { reply = " I got less than 2 - so it is invalid";  }
                     MyAPIGateway.Utilities.ShowMessage("TEST", reply);

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
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Commands: Lhelp, depart, ver, ltest");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Features: popup, destination, interstellar");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Try '/Lhelp command/feature' for more informations about specific items.");
                    return true;
                }
                else
                {
                    string helpreply = ""; 
                    switch (split[1].ToLowerInvariant())
                    {
                        case "depart":
                            helpreply = "Player travels to another server world\r\n" +
                                "The world you connect to depends on your location\r\n" +
                                "and if the server is online or not.\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /depart");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "depart command", helpreply, null, "Close");
                            return true;
                        case "ver":
                            helpreply = "Simply shows an internal reference version number\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /ver");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "ver command", helpreply, null, "Close");
                            return true;
                        case "test":
                            helpreply = "Simply shows what the mod scanner is picking up\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /ltest");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "test command", helpreply, null, "Close");
                            return true;
                        case "popup":
                            helpreply = "Displays a popup message to players when they \r\napproach your ship or station. \r\n" +
                                        "Name an LCD [station] then put the keyword popup in the public title field\r\n" +
                                        "Any message written in the Public Text box will be shown in the popup.\r\n";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "popup", helpreply, null, "Close");
                            return true;
                        case "destination":
                            helpreply = "Allows you to define a location as a departure \r\npoint to another server/sector. \r\n" +
                                        "Name an LCD [destination] then put the ip:port of \r\n the server followed by a description \r\nin the public title field.\r\n" +
                                        "Example: 221.121.149.13:28790 Lawless void Sector\r\nWhen near this point you type /depart to travel there.";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "destination", helpreply, null, "Close");
                            return true;
                        case "interstellar":
                            helpreply = "The boundry of interstellar space in your region. \r\n" +
                                        "Crossing it players can travel to a server/sector\r\ndefined in that direction.\r\nThis is usually configured by game admin.\r\n" +
                                        "Depending on settings you will need to type /depart \r\nto travel. Boundries are usually \r\naround 1000 Kms from centre of map.\r\nThere can be up to 6 directions defined.\r\n\r\n" + 
                                        "If you are a server admin, refer to the workshop page.";
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "interstellar", helpreply, null, "Close");
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

   
    }
}
