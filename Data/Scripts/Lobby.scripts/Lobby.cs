/*
 *  Lobby Mod 
 *  by PhoenixX (JPC Dev)
 *  For use with Space Engineers Game
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
        int counter = 0;
        bool initDone = false;
        bool instant = false;  //should it auto-depart or wait of the depart command (only works with command causes crash otherwise)
        bool seenPopup = false; //have we already displayed a popup in this zone?
        bool  noZone = true; //no zone in sight?
        public string Zone = "Scanning...";
        public string Target = "none";

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
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "This sector is equipped with a gateway station!");
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "Type '/Lhelp' for more informations about available commands.");
                 //now user configured - MyAPIGateway.Utilities.ShowMissionScreen("Lobby", "", "Warning", "Welcome to gateway Station.\r\n\r\nPlease enter a shuttle and when indicated on hud..\r\nType /depart to travel to its sector.", null, "Close");
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= gotMessage;
            base.UnloadData();

        }

	/* public override void Simulate()
	{
    	if (instant && Target != "none")
    		{
        	string join = Target;
        	Target = "none";
        	
        	MyAPIGateway.Multiplayer.JoinServer(join);
    		}
	}
    */
        public override void UpdateAfterSimulation()
        {
            //heres our processing loop

            if (!initDone && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                init();

            //once again, lets not run this bit on a server.. cause that would be dumb
            if (!AmIaDedicated() )
            {
                //my dirty little timer loop - fires roughly each 2 seconds
                if (counter >= 900)
                {
                    counter = 0;
                    if (UpdateLobby())
                    {
                        //MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = 
                        //if the option for insta teleport is enabled do so on entering a teleport zone.
                        if (instant && Target != "none")
                        { 
                            MyAPIGateway.Utilities.ShowMessage("Departure point", Target); 
                            //MyAPIGateway.Multiplayer.JoinServer(Target); //  <-- This crashes ??
                            //if (ProcessMessage("/depart")) {  }  //<-- so does this !?
                        }
                        //else
                        //{
                            string reply = Zone + " [Type /depart to travel]";
                            MyAPIGateway.Utilities.ShowMessage("Departure point", reply);
                        //}

                    }
                    else { Zone = "Scanning..."; Target = "none"; /* MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = Zone; */ }
                }
                counter++;
            }
            base.UpdateAfterSimulation();
        }

        private void gotMessage(string messageText, ref bool sendToOthers)
        {
                // here is where we nail the echo back on commands "return" also exits us from processMessage
                if (ProcessMessage(messageText)) { sendToOthers = false; }
        }

        public bool UpdateLobby()
        {
            //check our position - are we in a hot spot?
            //if (MyAPIGateway.Session.Player?.Controller?.ControlledEntity != null) {
             if ( MyAPIGateway.Session.Player.Controller.ControlledEntity != null)
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
                var updatelist = new HashSet<IMyTextPanel>();
                var updatelist2 = new HashSet<IMyTextPanel>();

                var sphere = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 9); //destination lcds
                var LCDlist = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
                string[] LCDTags = new string[] { "[destination]", "(destination)" };

                var sphere2 = new BoundingSphereD(MyAPIGateway.Session.Player.GetPosition(), 50); //popup notification lcds
                var LCDlist2 = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere2);
                string[] LCDTags2 = new string[] { "[station]", "(station)" };


                foreach (var block in LCDlist)
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

                foreach (var block in LCDlist2)
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

              //special option lcds
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
  
                 foreach (var textPanel in updatelist)
                 { 
                    //var checkArray = (textPanel.GetPublicTitle() + " " + textPanel.GetPrivateTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var checkArray = (textPanel.GetPublicTitle()).Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); //fix for removal of private text by keen
                    if (checkArray.Length >= 2) //if its Not at least 2 its invalid.
                     {
                        int title = 0; Zone = "";
                        foreach (var str in checkArray) {
                                //string reply = "i got " + checkArray.Length + " " + checkArray[0] + " " + checkArray[1];
                                //MyAPIGateway.Utilities.ShowMessage("TEST", reply);
                                if (title != 0 && title <= checkArray.Length) { Zone += " " + checkArray[title]; } 
                                title++;
                        } Target = checkArray[0]; return true; //this should probably check if ip is valid format but other than exiting current map nothing bad seems to occur if its invalid
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
        private bool ProcessMessage(string messageText)
        {
            string[] split = messageText.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // nothing useful was entered.
            if (split.Length == 0)
                return false;

            #region depart
            if (split[0].Equals("/depart", StringComparison.InvariantCultureIgnoreCase) || split[0].Equals("/jump", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Zone != "Scanning..." && Target != "none")
                {                
                    //test throwing a connection to a foreign server from server ie in lobby worlds or we have moved worlds
                    MyAPIGateway.Multiplayer.JoinServer(Target);
                }
                return true;
            }
            #endregion jump

            #region ver
            //ver reply
            if (split[0].Equals("/ver", StringComparison.InvariantCultureIgnoreCase))
            {
                string versionreply = "3 " ;
                MyAPIGateway.Utilities.ShowMessage("VER", versionreply);
                return true;
            }
            #endregion ver

            #region test 
            //This tests scan results and displays what the mod see's
            if (split[0].Equals("/test", StringComparison.InvariantCultureIgnoreCase))
            {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p != null);
            var updatelist = new HashSet<IMyTextPanel>();
            string reply2 = "";
            if (seenPopup) { reply2 = "Seen popup: true"; } else { reply2 = "seen popup: false"; }
            if (noZone) { reply2 += " no zone: true"; } else { reply2 += " no zone: false"; }
            if (instant) { reply2 += " intant travel: true"; } else { reply2 += " instant travel: false"; }
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
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Commands: Lhelp, depart, ver, test");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Try '/Lhelp command' for more informations about specific command");
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
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /test");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "test command", helpreply, null, "Close");
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
