Space Engineeers Workshop;
Lobby World MOD
======================

Developed by PhoenixX (Aka Pirate Captain X)

======================


There are not many keyboard commands. This mod mostly makes use of LCDs.

Keyboard commands:
Enter a command into the in game chat box, and it will perform an action.

Enter "/Lhelp" for a list of current commands.
Enter "/Lhelp #" for more information on that particular command.

======================


==================
How to use:
==================

***************
*Dynamic      * 
*Destinations:*
***************
Create an LCD on a large station or ship you wish to use as a "travel departure point"
Using "NAME" field on the LCD block key in: [destination] followed by your desired destination server
address and port and a description - for example 
[destination] 221.121.159.238:27270 Orion Pirates Zone

This will allow players within 9 metres of that LCD to /depart and travel to that server. The chat 
displays a notification that this is a departure point every few seconds or so until they /depart
or move more than 9 metres from the LCD.

If you want to have an easier way to enter a description you can enable the "text" content option
and use the LCD screen text editor instead to write in your destination server description.
Example:   The Orion Pirates PvP Server ARGh!
This might be useful if you have a lobby station, so players can read the description on the LCD
without waiting for the chat notification to appear.

The "Title" field of the LCD is not used and can be anything you like, example: "Orion Departure LCD" 

-

********************************
*Station Pop up message: (fun!)*
********************************
Create another LCD on a large station/ship block.
Using "NAME" field key in: [station] popup 
Then edit the public text (which is usually shown on the LCD screen) with whatever message
you would like to pop up on a players screen if they come within 50 metres of the LCD.

Whenever a player comes within 50 or so blocks (about the interior size of a small station or the 
wing of a large station) the pop up message will appear on the players screen and output the text
you specified, and not be shown again unless they travel outside of 50 metres from the LCD then
return again.

You do NOT requre any destination LCD, you can use these popups simply as an automatic welcome 
message to visitors of your ship or space station!

- 

******************************
* Interstellar Space Boundry *
******************************
This feature is a little buggy at the moment, and might not work at all as the newest game code 
only exposes grids within 5km of the player, which somewhat limits this option, unless you are 
within a ship that contains the boundry definition, or you just have really small boundries!

It is suggested you do not use this features until we update the code here to use server side
config files.

You can Set World "boundries" so that if a player flies off the edge of the allowed world - they 
enter "interstellar space" (the empty void between "sectors" (servers)) and can travel to whatever 
"sector"  (server) is assigned that direction.

If you imagine your entire game world as a large cube - facing from behind -   
The left wall of the "cube" represents "Galactic West"   
The Right wall is "Galactic East" 
The far wall is "Galactic North" 
The near wall is "Galactic South"
The top wall is "Galactic up" 
The bottom "Galactic Down"

This allows each server to be a "sector" of a larger universe with 6 possible exits depending on
your chosen direction.  

This is really easy to visualise on a role playing website map by represending each individual 
server as a cube.  Line them all up together to form a larger cube.   
Eg if you had 9 servers representing 9 locations in a "Galaxy"  the map would look like this -
[Server 1] [Server 2] [Server 3] 

[Server 4] [Server 5] [Server 6]

[Server 7] [Server 8] [Server 9]

On server 5, Players flying out the right size of your map would be sent to server 6.  etc.

To use this feature:
Create an LCD, eg - on a large station/ship block.  It shouldn't need to be powered so you 
could simply place a single block underground on a planet and place an LCD on each side and 
set that represents each exit direction. Just keep in mind cleanup tools may delete unpowered
grids.

"NAME" the LCD the desired direction of interstellar space.
Eg.  [GW] or [GE] or [GN] or [GS] or [GU] or [GD] 

In the LCD Text Field insert the steam address of the server you wish people to travel to,
eg. 221.121.149.13:28790 
After the server address put in a brief description eg.  Lawless Void Sector

What you should now see in your LCD text field is: 221.121.149.13:28790 Lawless Void Sector

So for example if you wish players who enter the "Galactic West" Interstellar space of your map
to be able to travel to the Lawless Void Sector server your LCD should look like this:

LCD NAME: [GW]
LCD Public Title: 221.121.149.13:28790 Lawless Void Sector    

You can have more than one direction go to the same sector by simply adding more directions 
with a space between.  
Eg:  [GW] [GN] [GS]

In theory you can override how far the boundry is (at the moment only 5km) but that part of 
the mod is currently not working as expected.

-

*******
*Notes*
*******
To increase compatability with non-english keyboards you can use [ ]  or ( ) in your LCD Tags, 
and they are case insensitive.



