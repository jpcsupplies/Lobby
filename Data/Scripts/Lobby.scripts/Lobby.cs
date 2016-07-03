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
    //using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
   // using IMyOxygenTank = Sandbox.ModAPI.Ingame.IMyOxygenTank;
    //using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LobbyScript : MySessionComponentBase
    {
        int counter = 0;
        bool initDone = false;
        public string Zone = "Scanning...";
        public string Target = "none";

        bool AmIaDedicated()
        { //apparently keen didn't invent one of these already?
            //lets see if i can adapt midspaces version (hope you dont mind!)

            //are we offline in which case running server side IS client
            if (MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
            {
                //DebugOn = MyAPIGateway.Session.Player.IsExperimentalCreator();
                if (MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE)) // pretend single player instance is also server.
                    //i am offline!
                    return false;
                if (!MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE) && MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
                    //i am some half server state! probably local hosted
                    return false;
                    //apparently im something else client related?
                return false; //MyAPIGateway.Multiplayer.MultiplayerActive
            }

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
           
            //ok lets warm up the hud.. unless of course we are a server.. which would be stupid
            if (!AmIaDedicated())
            {
                MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Clear();
                MyAPIGateway.Utilities.GetObjectiveLine().Title = "Initialising";
                MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Add("Scanning..");
                MyAPIGateway.Utilities.GetObjectiveLine().Show();
            
                //Lets let the user know whats up.
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "loaded!");
                 MyAPIGateway.Utilities.ShowMessage("Lobby", "Type '/Lhelp' for more informations about available commands\r\ntype /jump to go to current server");
                 MyAPIGateway.Utilities.ShowMissionScreen("Lobby", "", "Warning", "Welcome to gateway Station.\r\n\r\nThis server has been decomissioned.\r\nEnter a shuttle and type /jump to travel to its sector.\r\nWatch hub for sector indicator.", null, "Close");
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= gotMessage;
            base.UnloadData();

        }

        public override void UpdateAfterSimulation()
        {
            //heres our processing loop

            if (!initDone && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                init();

            //once again, lets not run this bit on a server.. cause that would be dumb
            if (!AmIaDedicated() )
            {
                //my dirty little timer loop - fires roughly each 1.5 seconds
                if (counter >= 100)
                {
                    counter = 0;
                    if (UpdateLobby())
                    {
                        MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = Zone + " Type /jump to depart on this shuttle";
                        // uncomment this line if you want players to travel immediately on entering zone
                        //MyAPIGateway.Multiplayer.JoinServer(Target);
                    }
                    else { MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = "Scanning..."; }
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
            //MyAPIGateway.Utilities.ShowMessage("Lobby", "Scanning..");
            //check our position - are we in a hot spot?
            //if (MyAPIGateway.Session.Player?.Controller?.ControlledEntity != null) {
             if ( MyAPIGateway.Session.Player.Controller.ControlledEntity != null){
                Vector3D position = MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetPosition();
                double X = position.X; double Y = position.Y; double Z = position.Z;
                string whereami = string.Format("[ X: {0:F0} Y: {1:F0} Z: {2:F0} ]", X, Y, Z);
                //MyAPIGateway.Utilities.ShowMessage("debug", whereami);
                MyAPIGateway.Utilities.GetObjectiveLine().Title = whereami;

                //hard coded target list cause I suck at server side datafiles..
                if (X >= -100 && X<=100 && Y >= -100 && Y<=100 && Z >= 15 && Z <=25) { Zone = "Lawless void"; Target = "221.121.159.238:27270"; return true; }
                if (X >= -100 && X <= -10 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Black Talon Sector"; Target = "59.167.215.81:27016"; return true; }
                if (X >= -100 && X <= -10 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Spokane Survivalist Sector"; Target = "162.248.94.205:27065"; return true; }
                if (X >= 10 && X <= 100 && Y >= -100 && Y <= -10 && Z >= -20 && Z <= 11) { Zone = "Pandora Sector"; Target = "91.121.145.20:27016"; return true; }
                if (X >= 10 && X <= 100 && Y >= 10 && Y <= 100 && Z >= -20 && Z <= 11) { Zone = "Ah The Final Frontier"; Target = "192.99.150.136:27039"; return true; }
                else { Zone = "Scanning..."; Target = "none"; return false;  } 

            } 
            return false;  //we fell through a hole           
        }

        #region command list
        private bool ProcessMessage(string messageText)
        {
            MyAPIGateway.Utilities.ShowMessage("debug", "hey we typed something");
            string[] split = messageText.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // nothing useful was entered.
            if (split.Length == 0)
                return false;

            #region jump
            if (split[0].Equals("/jump", StringComparison.InvariantCultureIgnoreCase))
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
                string versionreply = "2 " ;
                MyAPIGateway.Utilities.ShowMessage("VER", versionreply);
                return true;
            }
            #endregion ver

            #region help
            // help command
            if (split[0].Equals("/lhelp", StringComparison.InvariantCultureIgnoreCase))
            {
                if (split.Length <= 1)
                {
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Commands: Lhelp, jump, ver");
                    MyAPIGateway.Utilities.ShowMessage("Lhelp", "Try '/Lhelp command' for more informations about specific command");
                    return true;
                }
                else
                {
                    string helpreply = "."; //this reply is too big need to move it to pop up \r\n
                    switch (split[1].ToLowerInvariant())
                    {
                        case "jump":
                            helpreply = "transfers player to another server world\r\n" +
                                "The world you connect to depends on your location\r\n" +
                                "by default it connects to the default world\r\n";
                            MyAPIGateway.Utilities.ShowMessage("LHelp", "Example: /jump");
                            MyAPIGateway.Utilities.ShowMissionScreen("lobby Help", "", "jump command", helpreply, null, "Close");
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
