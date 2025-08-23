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
Create an LCD on a large station or ship you wish to use as a "travel point"
NAME the LCD block: [destination]
In the Public Title field insert the steam address of a space engineers server you wish players 
to be able to travel to - for example 221.121.159.238:27270
After the server address in the public title field put in a description of one or more words. 
Eg Ramblers Frontier 
What you should now see in your title field is: 221.121.159.238:27270 Ramblers Frontier


Whenever players come within 9 or so blocks (about the interior size of the default starter lemon ship)
their screen will indicate they can travel to the "description" (as above) sector, and to type /depart 
to leave.

-

******************************
* Interstellar Space Boundry *
******************************
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

NAME the LCD the desired direction of interstellar space.
Eg.  [GW] or [GE] or [GN] or [GS] or [GU] or [GD] 

In the Public Title Field insert the steam address of the server you wish people to travel to,
eg. 221.121.149.13:28790 
After the server address put in a brief description eg.  Lawless Void Sector

What you should now see in your title field is: 221.121.149.13:28790 Lawless Void Sector

So for example if you wish players who enter the "Galactic West" Interstellar space of your map
to be able to travel to the Lawless Void Sector server your LCD should look like this:

LCD NAME: [GW]
LCD Public Title: 221.121.149.13:28790 Lawless Void Sector    

You can have more than one direction go to the same sector by simply adding more directions 
with a space between.  
Eg:  [GW] [GN] [GS]

-

********************************
*Station Pop up message: (fun!)*
********************************
Create another LCD on a large station/ship block.
NAME the LCD block [station]
use the keyword "popup" in the public title field
then edit the public text with the pop up message you want to show when a player is near this LCD

Whenever a player comes within 50 or so blocks (about the interior size of a small station or the 
wing of a large station) the pop up message will appear on the players screen and output the text
you specified.


You do NOT requre a destination LCD, you can use these popups simply as an automatic welcome 
message to visitors of your ship or space station!

-

*******
*Notes*
*******
To increase compatability with non-english keyboards you can use [ ]  or ( ) in your LCD Tags, 
and they are case insensitive.



