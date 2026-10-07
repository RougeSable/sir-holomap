# Sir Holomap

An in-game map for Space Engineers, as a client-side plugin loaded by Pulsar.
Press **M** to open and close it. With a block in hand, M keeps the game's own
symmetry key.

Nothing goes through the server: a player without the plugin plays as usual,
and everything the map remembers, the player saw with their own eyes.

## The three views

The buttons **A**, **B** and **C** at the top switch between them; the mouse
wheel also glides from one to the next. Every view has the same menu on the
right (info, what to show, the list of what is in view) and answers the same
gestures:

| Gesture | Everywhere |
| --- | --- |
| Left drag | turn |
| Middle drag, or WASD | move |
| Wheel | zoom; every notch multiplies the distance (Ctrl + wheel flies forward in the 3D system) |
| Click | select, the info shows on the right |
| Double click | dive into what is under the cursor |
| Space | back to you |
| M or Esc | close |

- **A, planet.** The globe of the planet you are on, drawn by the game's own
  renderer from orbit: its real relief and colours, for any planet, modded
  ones included. You, your grids and the bases are in place, ships move as
  they move. Wheel in: the ground comes closer and the game sharpens the
  relief down to a few dozen metres. Wheel out: the neighbourhood.
- **B, local space.** Your neighbourhood seen from above a virtual plane, over
  the sky of the world. Each grid hangs over the plane by a line telling its
  height. The menu lists the grids in view; a click highlights one. A double
  click fastens the camera to a grid, which then fills the screen; the wheel
  out comes back over you.
- **C, system.** Three tabs: the planets and their moons laid flat by
  distance to the centre of the world; the system in 3D with true distances
  and enlarged bodies, the sun marked on the edge of the screen in its true
  direction; and the galaxy, where every server has a fixed place computed
  from its address. A dive from C (double click on a body or a grid) comes
  straight back to C when you zoom out.

## Memory

Whatever enters your synchronisation range is seen: its position and the time
are recorded. Beyond that range the game sends nothing more, so a marker stays
at the last place it was seen, greyed, with "seen ... ago". Coming back near a
place updates it: what is there is seen again, what is gone is forgotten.

The memory is kept on your machine, one per server, in
`%AppData%\SpaceEngineers\Storage\sir-holomap\Memory`. The settings (block
thresholds of A and of B and C, the galaxy filters, what to show) are kept in
`settings.xml` next to it, the same for every server.

## Joining another server

A double click on a server of the galaxy asks first, naming the server. The
server is then asked whether it answers, has room, needs no password and runs
this version of the game; only then does the game leave the current server.
On any of these failures you stay where you are. Should the other server still
refuse once the current one is left, the game is back at its main menu and the
map offers to return to the server you just left.

## Living with other plugins

The map borrows two steps of the game while it is open: the render camera (so
that the game draws the real planet from the map's point of view) and the
sun's direction (the "Daylight globe" box). If another plugin already holds
one of them, the map leaves it alone and says so in a notification. Without
the camera, view A shows a globe the map paints itself from the real planet's
relief and terrain colours, turning and zooming with the map's camera, at a
coarser level of detail than the game's own. Without the light, the globe
keeps the game's light.

## Building

Pulsar compiles the `Source` folder itself. For a local build, give the path
of the game's `Bin64` folder (see `Directory.Build.props.example`):

    dotnet build sir-holomap.csproj

The tests cover the plugin's pure logic and run without the game:

    dotnet test tests/tests.csproj
