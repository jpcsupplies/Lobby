Space Engineeers Workshop;
Lobby World Gateway MOD
(Aka Travel Module of Galaxies Project)
======================

Developed by PhoenixX (Aka Pirate Captain X)

======================

There are not many keyboard commands. This mod mostly makes use of LCDs.

Keyboard commands:
Enter a command into the in game chat box, and it will perform an action.

Enter "/Lhelp" for a list of current commands.
Enter "/Lhelp #" for more information on that particular command.

If you just want to dry run test the jumping sound, Enter: /Ltest sound
Note: This will not trigger the 20 second countdown, it just plays the sound.
-


==================
Why Use this ?
==================
1: You could create an offline "game lobby" map containing a station with LCD exits to all your 
favourite dedicated servers, as a more fun way than using the server browser if you only
use a curated set of servers.  It could also be an online world for larger server hosts where
admins running servers can post and advertise their world to attract players from a central
location.  Doing things this way lets you leave notes on an LCD about each server, 
and can help build communities; eg everyone meets up to socialise at a space bar on the 
"Lobby" server, then flies off to their various servers to play.

2: "We have moved" servers.   Server hosts can frequently move and upgrade their dedicated
servers as their community grows.  Normally this means you lose most of your existing players
if they don't know the new server address.   With this mod you can make a friendly server
that still works with their existing server favourite, and it will direct them to the new
server in a Friendly Role Play/Game Play manner which is less jarring.  This was the original
reason I created this mod at the beginning.  
(I was moving servers, but everyone kept trying to connect on the old address)

3: "World/Universe/Community building" servers.  RPG adds to the fun of some servers, add
a complex backstory, spread out between several linked server worlds to explore as sectors in a
Galaxy of your chosen "Universe". 
If you are a big community, this can minimise lag/server slow downs, allow different "sectors"
to be managed by different admins, or just have a lose federation of server admins working
in a common goal within the same galaxy and/or sci-fi franchise that can travel between each
others server maps with ease.

4: A "special event" server, this mod allows you to specialise particular worlds, run a one time 
event on an extra server (space battle? Racing? PvP? PvE? Mining? Industry? Building? Trade?)?
or develop a larger game "Universe" within your chosen sci-fi setting where you can slowly add 
new parts to your explorable galaxy.  That way one bookmarked server can lead to many. 

5: Highly "specialised" servers. Have a server just big enough for a single planet and moon, 
perhaps with an ocean. Giving high focus planetary start gameplay without performance concerns 
(Minimising NPC encounter types and other space behaviours that may otherwise lag server)
Travelling from this server might take you to another server which is dedicated to Mars, keep 
going, the next sector maybe you are exploring the moons of Saturn etc.  
This level of specialisation has the advantage that you can run different mods 
(water mod on one, weapon core on another for PvP, or enhanced mining blocks for large scale
mining, avoiding disrupting players on another server.) With a smaller more focused map size 
the performance of the dedicated server may be much higher and suit given players style of play
while still allowing interactions with other players in other "sectors" of your server Galaxy.

6: Simply to divide a large player base across several physical dedicated servers while all 
being part of the same Universe that can interact somewhat.

-

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
Note: This feature is a little buggy at the moment, and might only work on in-game hosted map
coop and offline mode. We are currently working towards making it stable on dedicated servers too.
It is suggested you do not use this features until we update the code here to reliably work on
server side config files.

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

This allows each side of the map to exit to another "sector" of a larger universe with 6 possible 
exits depending on your chosen direction.  Each Sector is a physically different Space Engineers
Dedicated Server. 

This is really easy to visualise on a role playing website map by represending each individual 
server as a cube.  Line them all up together to form a larger cube.   
Eg if you had 9 servers representing 9 locations in a "Galaxy"  the map would look like this -
[Server 1] [Server 2] [Server 3] 

[Server 4] [Server 5] [Server 6]

[Server 7] [Server 8] [Server 9]

On 'server 5', Players flying out the right [GE] of your map would be sent to 'server 6'.  etc.


********************
To use this feature:
********************
First check if there are already exits defined (if you have multiple admin etc)
with /LCONFIG

As an admin/space master face toward an emply space in the world, then type /LEDIT in chat.
This will spawn a static grid LCD.  Highlight the screen and press "F" to edit the text. 
(Once you are done making changes exit the LCD text editor by hitting "ok" and type 
/LSAVE in chat which should record the changes in the server config, and despawn the LCD
again. More on that later.) The LCD will not be powered but that doesn't matter as it is
only used to view/edit the "Interstellar Space" departure point settings.

Once you press "F" on the LCD screen highlight you should find it already populated with
some default settings the first time you load it:

[cubesize]  150000000         
This is how big (diameter wide in metres) your map "cube" is. The default is 150000KMs 
(75000km radius) This size allows for the default 30000KM outer radius (60000KM diameter) 
of Keen Economy trade station spawn locations to fit inside your space map dimensions. 
We may make the default smaller later. But you can change it to any appropriate size for your
needs.
(Eg.60500000 (60500KM) allows about a 250km gap (500km/2) from the outer most trade station.)

[edgeBuffer] 2000
This is the "edge border" width in metres approaching each "Interstellary Space Exit Facing" 
Default is 2km. (2000 metres)
Within this zone it is planned that the game will give visual warnings and hopefully enough 
time to stop before crossing the edge of the playable game area into "Interstellar Space" 
and a few minor things. Not Fully implemented yet.

[GW]
[GE]
[GN]
[GS]
[GU]
[GD]
These are the actual exit destinations of your imaginary "Cube" that your map exists within.

The directions represent the following map Axis:
GW "Galactic West"  (-X) (the left side of cube)
GE "Galactic East"  (+X) (the right side of cube)
GS "Galactic South" (-Y) (the front side of cube)
GN "Galactic North" (+Y) (the back side of cube)
GU "Galactic Up"    (+Z) (the roof of the cube)
GD "Galactic Down"  (-Z) (the floor of the cube)

To assign a given exit pick one of the directions and insert the destination address and a 
brief server name to identify the server zone it is for.  Example:
[GE] 202.123.41.11:25565 Orion
This example would allow players who cross the "Galactic East" Interstellar border of your 
map boundry to travel to a "Orion" server if it was hosted at that IP address/port.

Once you have keyed in one or more servers in an appropriate exit direction, to recap -
Press ok to exit the text editor, then type /LSAVE to save them and despawn the LCD editor.

Other options may be added later, such as passcodes or options related to ship to transfer 
too but the above are the most important options.

-

*******
*Notes*
*******
To increase compatability with non-english keyboards you can use [ ]  or ( ) in your LCD 
[station] and [destination] Tags, and they are case insensitive.  

For Interstellar Space definitions, each exit prefix is already populated, so () does not
work since you just type your address after it.



