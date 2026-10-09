# Sir Holomap

An in-game map for Space Engineers, as a client-side plugin loaded by Pulsar.
Press **M** to open and close it. With a block in hand, M keeps the game's own
symmetry key.

Nothing goes through the server: a player without the plugin plays as usual,
and everything the map remembers, the player saw with their own eyes.

## The three views

The buttons **Planet**, **Local space** and **System** at the top switch
between them; the mouse wheel also glides from one to the next. Every view has the same menu on the
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

- **Planet.** The globe of the planet you are on, drawn by the game's own
  renderer from orbit: its real relief and colours, for any planet, modded
  ones included. You, your grids and the bases are in place, ships move as
  they move. Wheel in: the ground comes closer and the game sharpens the
  relief down to a few dozen metres. Wheel out: the neighbourhood. A double
  click on a grid in range fastens the camera to it: the left drag then
  turns around the grid, the wheel still zooms over the globe. A double click
  on another body in sight (a moon, a neighbour planet) glides the globe over
  to it, still in the planet view, and the menu shows that body.
- **Local space.** Your neighbourhood seen from above a virtual plane, over
  the sky of the world. Each grid hangs over the plane by a line telling its
  height. The planets and moons hide the plane: whatever part of it passes
  inside a body, or behind it, is not drawn. The menu lists the grids in
  view; a click highlights one. A double
  click on a grid in range fastens the camera to it, and the game draws it
  filling the screen; on a remembered grid, the view centres on its marker
  and its info says it is out of range. While the camera follows a grid,
  the plane stays where it was before the double click, and the grid's
  height line grows and shrinks as it climbs or sinks. A simple click on
  empty space outside the grid's frame, a right click or the wheel lets go,
  back over you; a left drag only turns around the grid. The Planet button
  keeps a grid near a planet in the middle of its globe.
- **System.** Three tabs: **System 2D**, the planets and their moons laid
  flat by distance to the centre of the world; **System 3D**, the system with
  true distances and enlarged bodies, the sun marked on the edge of the screen
  in its true direction; and **Galaxy**, where every server has a fixed place computed
  from its address, lying on a black, starry sky that fills the view however
  far you zoom out. A dive from the system view (double click on a body or a
  grid) comes straight back to it when you zoom out. The globes of the bodies are painted
  from the real planets as soon as you join a world, a coarse picture within
  a few frames, then the full one, so they are ready when the map opens. Each
  one shows its own relief and the terrain the game puts at each place, in
  the colour the game paints it with from far away, or else the mean colour
  of the terrain's own texture: green and blue for EarthLike, red for Mars,
  and the same for the planets of a world mod.

The switches between the three views depend on the scale only, with a gap
between the way out and the way back, so the views never take turns when
the wheel stops on a limit.

## Memory

Whatever enters your synchronisation range is seen: its position and the time
are recorded. Beyond that range the game sends nothing more, so a marker stays
at the last place it was seen, greyed, with "seen ... ago". Coming back near a
place updates it: what is there is seen again, what is gone is forgotten.

The memory is kept on your machine, one per server, in
`%AppData%\SpaceEngineers\Storage\sir-holomap\Memory`. The settings (block
thresholds of the planet view and of space, the galaxy filters, what to show) are kept in
`settings.xml` next to it, the same for every server.

## Joining another server

A double click on a server of the galaxy first asks the server whether it
answers, has room and runs this version of the game; on any of these failures
you stay where you are, with a message telling why. Then a confirmation names
the server; when the server asks for a password, it holds a box to type it. Its
text is cut to the width of the box and the box grows to hold it, in every
language and for the longest server names.
The password is handed to the game's own join when it asks for it, and is not
kept. Only then does the game leave the current server. Should the other
server still refuse once the current one is left (a wrong password, for
instance), the game is back at its main menu and the map offers to return to
the server you just left.

## Languages

The map speaks the language set in the game's options, for every language
the game offers; a text missing in a language shows in English. The texts are
in `Source/Logic/Texts.cs` (English) and `Source/Logic/Translations.*.cs`.

## Living with other plugins

The map borrows two steps of the game while it is open: the render camera (so
that the game draws the real planet from the map's point of view) and the
sun's direction (the "Daylight globe" box). If another plugin already holds
one of them, the map leaves it alone and says so in a notification. Without
the camera, the planet view shows a globe the map paints itself from the real planet's
relief and terrain colours, turning and zooming with the map's camera, at a
coarser level of detail than the game's own. Without the light, the globe
keeps the game's light.

## Building

Pulsar compiles the `Source` folder itself. For a local build, give the path
of the game's `Bin64` folder (see `Directory.Build.props.example`):

    dotnet build sir-holomap.csproj

The tests cover the plugin's pure logic and run without the game:

    dotnet test tests/tests.csproj
