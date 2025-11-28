============================================
Space Engineeers Workshop;
Lobby World Gateway MOD
(Aka Travel Module of Galaxies Project)
============================================
Developed by PhoenixX (Aka Pirate Captain X)

-

==========================
Contents in this document:
==========================
'Why Use this ?'

'How to use'
	'Keyboard Commands'
		'Admin Only'
	'Dynamic LCD Destinations'
	'Station Pop up Messages (Fun!)'
	'Interstellar Space Boundry'
		'How to use this feature'
	'Navigation HAZARDS OR Warnings'
	'Global Server Wide GPS Points'
	
'Other Configuration options'
	'AllowDestinationLCD'
		'AllowAdminDestinationLCD'
	'AllowStationPopupLCD'
		'AllowAdminStationPopupLCD'
	'NetworkName'
	'ServerPasscode'

'Claim System'
	'AllowStationClaimLCD'
	'AllowStationFactionLCD'
	'AllowStationTollLCD'
	
'Notes'

-

==================
Why Use this ?
==================
Author Note:
In addition to the below 6 reasons.. because this mod adds Navigation Hazards like Blackholes, 
Wormholes(whiteholes), Radiation Zones and Repulsor zones.  Can also allow you to have GPS
points that are immediately available to all users on your server.  No more leaving LCDs, motd
or vague notes the players never read anyway, just ping, and they know every point of interest
you need them to know about.  You can also have per-station popup greeting messages, sort of
like a MOTD for every persons space station.

Other reasons:
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
to be managed by different admins, or just have a ragtag federation of server admins working
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

*********************
* Keyboard commands *
*********************
There are not many keyboard commands. This mod mostly makes use of LCDs or configured
events like navigation hazards.

Keyboard commands:
Enter a command into the in game chat box, and it will perform an action.

Enter "/Lhelp" for a list of current commands.
Enter "/Lhelp #" for more information on that particular command.

Version check:
Enter: /ver
This displays the current version of mod (and any other mods supporting /ver)
and some details about the people who helped make it.


There is also an /override jump/bump command.      
This is for freeing (bumping) a ship (locked in voxel as station) out a voxel.   
For instance if an admin manually places an encounter crashed ship,  or you find a random
one trapped in a voxel, include an LCD near the cockpit named [override]
This will allow users to spool up the interstellar jump engine, using /override.  Once
fully spooled, the user should take a seat and type /depart as prompted.  This will attempt
a random jump override to free it from voxel. 

Danger/Warning: Forcing your interstellar engine to override its safety protocols can 
result in severe damage to the ship or its systems.  Trying to jump a ship trapped in voxel
will overload it, as in theory it is trying to jump the entire asteroid/planet.   Naturally 
this will fail, but the ship itself will pop out like a cork released under water with 
unpredicable results!   (Yes this is deliberate it is meant as an encounter activity!)

==========================================================================================
For admins, at the /depart  stage they can specify a distance in metres to force the jump.
Normal players can only do a single random jump, and it will probably make things explode.
Admins can also force the interstellar engine to spool up on any ship or station grid, not 
just station blocks stuck in voxels, and can do so without an [override] lcd.
Note: Likely the [override] lcd will need to be owned by an admin as a security check.
Admins should be the ones adding the required LCD.
==========================================================================================

************
*Admin Only*
************
Note: There are some additional keyboard commands available for admins. Refer to the 
"Interstellar Space Boundry" and "Other Configuration Options" for details.
==========================================================================================
Admins - if you find yourself with an invalid out out of date configuration, 
enter "/ltest reset"   to revert your server side config file to default settings without
needing to remotely log into your server to delete it by hand.
Be sure to use /lconfig (or /ledit and copy/paste the old settings from LCD) beforehand 
if you have not noted your current network name or existing Interstellar exit server 
addresses or navigation hazards if you need to add them again.
==========================================================================================

Admins have several test commands for debugging various mod featues.  
They can also teleport themselves or the currently controlled grid using the /hop
command.   It has two operating modes "look" and "Absolute"
Hop 'Look' Mode:
1) Face the direction you wish to move
2) Type "/hop  <DISTANCE>"  
Example, to hop 10 kms type this: /hop 10000

Hop 'Absolute' Mode:
1) Work out the X Y Z coordinates you need (You can get these from a saved GPS point)
2) Type "/hop <X coord> <Y Coord> <Z Coord>"
Example, to hop to the xyz coordinates x10000,y50000,z100000 type this: 
/hop 10000 50000 100000

You can also test Physics modifiers such as the rotation effect or gravitational pull
effect. For example to tilt the current grid sideways 45 degrees: 
/phys rot right 45

To check the ship stumble/stagger effect:
/phys stagger

To test a gravity pulse you can use well or bomb:
Usage: 	/phys well <x> <y> <z> <radius> <+/- strength>");
 	/phys well [pos] <radius> <+/- strength>");
 	/phys well [pos]+<offset> <radius> <+/- strength>");

	/phys bomb <x> <y> <z> <radius> <+/- strength>");
 	/phys bomb [pos] <radius> <+/- strength>");
	/phys bomb [pos]+<offset> <radius> <+/- strength>");

Examples:
Or to test a 0.25 Gravity pulse from the location 1000,1000,1000 with a 5000 metre 
radius type:  /phys well 1000 1000 1000 5000 25

Or to test a really aggressive Gravity wave from the location 1000,1000,1000 with a 
5000 metre radius type: /phys bomb 1000 1000 1000 5000 100

Debugging commands (/ltest /lconfig etc):
You can do a sound check for all the audio effects.
If you just want to dry run test the jumping sound, Enter: /Ltest sound
Note: This will not trigger the 20 second countdown, it just plays the sound.
Other Examples. 
/ltest sound0, /ltest sound1, /ltest sound2, /ltest sound3, 
/ltest sound4, /ltest sound5, /ltest sound6
Note: sound3 is the radiation ticks, you can also specify volume and interval

Other tests:
Stop Sound, and general diagnostic check:
If entered on its own, it will attempt to stop any playing sound effects from the Mod and
display some basic diagnostic info about popup message status flags range checks etc.
Example: /ltest

Force a server tick and display more verbose info about that server tick.
/ltest debug

To give you a rough summary of some of the server side config file settings and exits:
/lconfig

-

***************
*Dynamic LCD  * 
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

You can also limit destination LCDs to only ones created by a server admin. Or disable them entirely.
Refer Other Configuration options at end of readme.

-

********************************
*Station Pop up messages: (fun!)*
********************************
Create another LCD on a large station/ship block.
Using "NAME" field key in: [station] popup 
Then edit the public text (which is usually shown on the LCD screen) with whatever message
you would like to pop up on a players screen if they come within 50 metres of the LCD.

Basic Usage:
Just make the LCD, name it [station] popup then highlight the screen and press "f" to edit
the message you want to pop up to players.

Whenever a player comes within 50 or so blocks (about the interior size of a small station or the 
wing of a large station) the pop up message will appear on the players screen and output the text
you specified, and not be shown again unless they travel outside of 50 metres from the LCD then
return again.

Advanced Usage:
If 50 metres is too big or small you can override the distance by adding a range to the NAME. 
Example:  [station] popup 100
Overrides are limited to between 6 and 200 metres in game.

If you want to limit popup messages to only server admins, or disable them entirely this is
a configurable option. Refer Other Configuration options at end of readme.

Note:
You do NOT requre any destination LCD, you can use these popups simply as an automatic welcome 
message to visitors of your ship or space station!

- 

******************************
* Interstellar Space Boundry *
******************************
Note: This feature is a little buggy at the moment, but should work.

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


************************
How to use this feature:
************************
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


******************************
Navigation HAZARDS OR Warnings
******************************
Navigation warnings are admin created popup warning messages tied to a particular position
in space, and a defined radius around it.  If a Player enters one of these hazard zones
it displays a warning, generates a GPS point, and plays an alert tone until they leave the
area.  Some may have special rules changing game mechanics or physics in them.

There are several TYPES of Hazards you can use when defining a Navigation Warning. Many
have different behaviours, a primary and an anomaly. Anomaly behaviours are losely 
inspired by anomalies from the STALKER game series, generally aimed at being explored.

Current Types:
If you don't specify a type; default is "General" type.  It gives a GPS point and warns
with your configured message while within this zone.  It has no Anomaly behaviour.
Example Parameters:   x,y,z radius detailed description

"Radiation" Random Radiation will be applied to the player while anywhere in this zone. 
"Anomaly" behaviour is reduced radiation in the centre to allow ruins, wrecks, derelicts 
or other encounters to be placed there by the admin with the intention of being explored.
Example Parameters: Note: "Radiation" can be abbreviated to "R" when defining zones.
		x,y,z radius Radiation detailed description
		x,y,z radius Radiation anomaly detailed description

"Blackhole" A steadily increasing gradient of gravity will occur the closer you get to 
centre.   Once you reach the centre you will become frame locked in place and unable to
escape short of death or a jump drive.  Before that is the event horizon zone where gravity
is too strong to escape,  at best you may be able to hold position, but you could find your
self phasing in and out of place.  
<Not implemented yet> You will steadily take damage while within the effects of a Blackhole.
"Anomaly" <Not implemented yet> behaviour is there is a relatively stable ring around the 
centre.  Although it is not possible to escape the ring under power, except with a jump 
drive you will not take much damage, and can move around within the ring region, although 
attempting to escape could also cause phasing issues.. also a good place to hide secrets.
Example Parameters: Note: "Blackhole" can be abbreviated as "B" when defining zones.
		x,y,z radius Blackhole Power Long Description
		x,y,z radius Blackhole Anomaly Power Long Description

"Whitehole" A similar gravity gradent to Blackhole, but instead of trapping you it sends
you somewhere to a fixed location when pulled in centre.  Effectively a one way Wormhole. 
"Anomaly" behaviour is the location is instead entirely random at a fixed distance.
Any object in the game, including artillery shells can be pulled into these and thrown out
elsewhere. Whiteholes with a fixed exit generate an Ejector zone automatically on the 
other side. 
Example Parameters:  Note: "Whitehole" can be abbreviated as "W" when defining zones.
		x,y,z radius Whitehole Power x,y,z Long Description Fixed Exit
		x,y,z radius Whitehole Power Eject_Radius Long Description Random Exit

"Eject" This is an Ejector (Or Repulsor) this will push you away if you attempt to approach
it.  These can be their own Navigation Hazard, or will be automatically created at the exit
point of a Whiteholes's wormhole.
<Not implemented>"Anomoly" behaviour - if you managed to get past the repulsion zone by raw
speed, brute force thrust, or using a jump drive there is a dead zone inside you can move 
around within.   Ideal for admins to hide a secret in.

******
Adding Navigation Hazards to your game:
******
To add these Navigation hazards to your world, open your configuration editor with /Ledit 
and look for the [Navigation Warnings] heading.

Below this heading you can enter different values and a warning message. 

x,y,z radius <type> <subtype> <modifier eg range, power or x,y,z> Detailed warning message.

"Radius/Range" is a figure (in metres) so 1000 is equal to 1km.  
"Type" can be:		Nothing,
		 	Radiation (abbreviated R), 
			Blackhole (abbreviated B), 
			Whitehole (abbreviated W), 	
			Eject (abbreviated E)
"Subtype" is either Anomaly or nothing.
"Power" is usually a force value related to Attract or Repell Between 0 and 100.
"x,y,z" is the coordinate location on the map you want to use.
"Detailed warning message" is whatever description you want for the navigation warning 
chat pager.

===============================================================================
Note: "G" (Gravity) Class navigation hazards add 100 metres to their warn
radius to give players at least some chance of stopping in time to avoid them.
Keep this in mind in deciding on the size of your Hazard zone.

"R" (Radiation) or "Z" (Generic/General) Class navigation hazards have no such 
margin for error, and can be any size, (even less than 10 metres)

The "Class" of a Navigation Hazard is the letter code in the GPS point created
for the hazard. Yes it is meant to have a weird looking GPS name text.

This is how to read it:
#Nav Hazard#Z3 R:0.5KM
	#Nav Hazard# - It is a {Nav}igation {Hazard} to watch out for, clearly. 
	Z3 - The hazard class is "Z" (generic) and this it hazard list item "3"
	R:0.5KM - The hazard has a radius "R:" of 0.5km (500 metres)

If the class code isn't G, R or Z, then it might be something new i forgot to
mention anywhere, and added later... 
Nanites? Minefield? Warzone? Broken spacetime? Private Property? Who knows!
===============================================================================

Formating Examples:
[Navigation Warnings]
-341000,24422,11111 80000 Danger! space pirates? (general zone with 80 km radius)

-341000,24422,11111 2000 Radiation (Radioactive zone 2km radius)
-341000,24422,11111 2000 R Anomaly (Rad zone 2km radius less radiation in middle)

-341000,24422,11111 8000 Blackhole 10 Danger ! (Weak blackhole with 8 km radius)
-341000,24422,11111 2000 B 100 Danger ! (Strong small blackhole with 2 km radius)
-341000,24422,11111 8000 B Anomaly 20 Danger ! (8km Blackhole with survivable ring)

-341000,24422,11111 2000 Whitehole 10 34100,2000,-1111 Whee!(Fixed wormhole exit)
-341000,24422,11111 2000 Whitehole 100 1000000000 Yikes!(Random 1 million km exit)

-341000,24422,11111 4000 Eject 40 Bye Bye! (Insistent Repulsor Ejection zone 4km)
-341000,2422,1111 5000 Eject Anomaly 10 Bye? (5km Weak Repulse zone with deadzone)

Once you have created (/ledit) your navigation hazard locations then use /lsave to 
record it. Copy and paste the x y and z fields from a GPS point at your desired 
position to get the correct location.

*****************************************
Notes on Special Navigation Warning Types
*****************************************
(Currently a work in progress, these specifications may change without notice.)

The radiation zone aims to have two types: 
Normal Rad Zone (All space within radius is radioactive, weaker at the edge)
Anomaly Rad Zone(All space within radius is radioacive, weaker at the edge and middle)

Anomaly deadzones:
The size of edge or deadzones (middle or ring zone) may use the [edgebuffer] setting 
from /ledit, or 10% of the radius if it is smaller than the defined edgebuffer size.
This may change as the feature evolves.

Anomaly zones might be handy for hiding space hulks or relics or other interesting
ruins where you want the player to take less radiation contamination or damage to 
allow them to explore it. Or for hiding secret bases there!

Normal Rad Zones might be handy for putting around Admin inserted areas with Uranium 
available to mine or areas you just want players not to spend too much time near.

It can be used as follows:
Designate the type using the Keyword "Radiation" or simply "R"
Designate an Anomaly zone by the Keyword "Anomaly" (after "Radiation" or "R")

Further examples:
[Navigation Warnings]
-341000,24422,11111 8000 Radiation Area High in Uranium ore.
1234,34322,111 10000 R Nuked area of space.
22323,234244, 888 20000 R Anomaly Mysterious Derelict Space Hulk
122323,2334244, 5000 Eject Anomaly 12 My Secret base



*****************************
Global Server Wide GPS Points
*****************************
Global Server Wide GPS Points are definable GPS entries that are automatically given to all 
players.  Examples might include where all the planets are, or where important locations 
like trade or travel hubs/stations are located or special locations like exit points or
mining facilities, ore locations, asteroids; whatever the server admin wants or needs all 
the players to know.

You could also use this as a sort of 'Admin Curated Unknown/Mystery Signal" for all players
pointing at an abandoned ship or station, or some server event happening that day.

To use this feature, open your configuration editor (/ledit) 
(or manually edit the config file in the map storage folder)  and add your Global GPS 
points under the [GPS] header in the format:
x,y,z colour "GPS Name" Detailed GPS Description.

An easy way to insert GPS points: On an empty line, Paste in a gps from your own gps list 
then replace the : with , key in a colour then add a "name" and optionally a description.

Examples to add all the default planet locations:
[GPS]
0,0,0 blue "Earthlike" Earth Centre of the map
16384,136384,-113615 gray "Moon" The Moon
1031072,131072,1631072 yellow "Mars" The red planet
916384,16384,1616384 cyan "Europa"  The Ice Planet
131072,131072,5731072 green "Alien Ganymede" The alien xeno planet
36384:226384:5796384 gray "Titan" moonlet Titan.
-284463,-2434463,365536 lime "Triton" moonlet Triton
-3967232,-32232,-767232 yellow "Pertam" The sandy planet of Pertram

-

============================
Other Configuration options
============================
The /Ledit command has additional options a server admin can change although many
are still placeholders and will be ignored, and may or may not be particularly 
reliable:

[AllowDestinationLCD] true / false
Should Destination LCDs be allowed to work.  Default true.
Configures if players on servers can create their own destination LCDs
If this is set to false all [destination] LCDs will be ignored.

[AllowAdminDestinationLCD] true / false
Default true.
If you set AllowDestinationLCD to false but set this option to true then if a server
admin creates a [destination] LCD the destination will still be offered to players
as long as the LCD is owned/created by a server Admin user.

[AllowStationPopupLCD] true /false
Should station/ship popup greeting LCDs be allowed? Default true.
Configures if "[station] popup" will work or not. false means they are ignored.

[AllowAdminStationPopupLCD] true /false
Default true.
If you set AllowStationPopupLCD to false but set this option to true, then if a server
admin creates a [station] popup #  LCD, then the popup will still appear as long as the
LCD is owned/created by a server Admin.

[NetworkName]    
Currently a placeholder for future features. Not Implemented.
This is the optional name of your shared universe.
Example: You have three different servers which are connected using interstellar boundries
or departure LCD's. To share things all three servers would need to be the same Network.  
Eg. Orion
If omitted, it is assumed your Network is "Anarchy"  which means you can carry things
between any uncontrolled network of servers running this mod. (Exciting.. or scary?)
If set the mod may warn before attempting to travel to a different network name that 
things may not carry over as expected, or will remain on the server you are departing.
(Note to self: May require server map settings to NOT be creative, or only allow ships 
from creative servers to travel TO creative servers.   Survival <-> Creative travel
should probably not be allowed even on Anarchy servers.)

[ServerPasscode]
Currently a placeholder for future features. Default is no passcode. Not Implemented.
This is the optional passcode to Authenticate information carried between same network 
servers by a player travelling between them.   All servers would need to share this
code and it is not available to normal players only admins as it is only recorded server
side.   /Ledit may only allow "sending" this code, one way.  It might not show in
/Lconfig.    
(Note to self: if a passcode is specified but no network, ships may be kept in storage
and not spawned unless joining an Anarchy server using the same passcode.
Information will only be allowed between servers sharing the same, or sharing no
passcode.  Additional logic allowing players to opt to allow their ship to travel but
one way only to no passcode servers, or departing a server and specifying a passcode 
when they leave to allow it to travel too (ie the entry 


************
Claim System
************
[AllowStationClaimLCD] true / false
Currently a placeholder for future features. Default true.
This is an option that configures if players can use "[station] claim" LCDs
This configures if a player regardless of if they are in a faction or not can claim a 
region of space as their own personal territory.
Positive actions here may increase inter-faction/player reputation.
Negative actions here may decrease inter-faction/player reputation.

[AllowStationFactionLCD] true / false
Currently a placeholder for future features. Default true.
This is an option that configures if factions can use "[station] faction" LCDs
This configures if a player can claim territory as belonging to their faction.
Positive actions here may increase inter-faction/player reputation.
Negative actions here may decrease inter-faction/player reputation.

[AllowStationTollLCD] true / false
Currently a placeholder for future features. Default true.
This is an option that configures if "[station] toll" LCD's are allowed.
This configures if tolls can be charged to allow players to enter claimed space.
toll size may vary depending on reputation of current player/faction to the 
player/faction claiming the territory.     Tolls will likely be placed in a
[vault] cargo crate; or failing that the factions economy bank balance.
Directly paying a player the toll will likely not be allowed except in
very specific scenarios.  Vaults are more immersive to gameplay since potentially
it allows heists.. and a condition (damage/grinding to [vault] container) that
can be checked for triggering severe negative reputation changes.
Direct Faction payments (or at least a 50:50 split between vault and faction)
may be allowed if the faction has more than 3 members?



-

*******
*Notes*
*******
To increase compatability with non-english keyboards you can use [ ]  or ( ) in your LCD 
[station] and [destination] Tags, and they are case insensitive.  

For Interstellar Space definitions, each exit prefix is already populated, so () does not
work since you just type your address after it.

Navigation Hazards may be expanded later to have a "type" which might actively generate 
hazards (eg minefield, warzone, radiation, nanites, hostileAI, blackhole etc) but 
currently are informational only, requiring an admin to actively add a hazard manually.

Other options (related to what you carry between servers) may be added later such as:
Travel options: Allow Ship, Allow Inventory, Allow Faction, Allow Buffs, 
Allow bank balance.
General options: Enable Visuals, Enable lottery etc. depending what features we 
can get working.
(Note to self: lottery is just a random money giveaway from ticket sales and travel/claim/toll 
creation fees?)



