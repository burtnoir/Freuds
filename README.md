# Freuds

A 2D side-view rescue game in the style of Oids and Thrust, written in C# on MonoGame.
Current state: **milestone 3, TileMap levels**. Fly a ship under gravity over a level made in TileMap, with its art drawn and its spawn points shown as coloured markers.

## Requirements

- .NET 8 SDK
- Windows, macOS or Linux (the game uses MonoGame DesktopGL)

## Run

```
dotnet run --project src/Freuds.Game
```

Controls: Left / Right (or A / D) rotate, Up (or W) fires the engine, Space fires the cannon, R restarts after a crash, Esc quits.

What to try:

- Thrust uses fuel (bar at top left). Out of fuel means no engine.
- Touch the terrain and the ship is destroyed, unless you touch down upright, slowly, on flat ground. A landed ship is green; thrust to take off again.
- Holding Space fires at most 20 bolts in flight (the pips under the fuel bar show what is left). Bolts launch in the direction the ship faces, add the ship's own velocity to their launch speed, and ignore gravity after launch.
- Fly off the left or right edge: the world loops horizontally.
- Fly to the top of the level: the ship cannot leave it. It sticks there with only a small upward speed for about a second, then drops back (unless you keep the engine on).
- Fly above the highest terrain (the floating island) and a magenta bar appears at the top. That is the placeholder for the mother ship, and vertical scrolling stops.

## Levels

The game loads `Levels/rescue_example.json` (next to the executable) or the file named as the first command line argument:

```
dotnet run --project src/Freuds.Game -- path/to/level.json
```

If the file is missing or unusable, the error is printed and the old demo level is used. The tileset image named in the map must sit beside the JSON file. For the build to copy them, add this to `src/Freuds.Game/Freuds.Game.csproj`:

```xml
<ItemGroup>
  <Content Include="Levels\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

How a TileMap export is read (`TileMapImporter`):

- Tile layer `Collision`: any non-zero cell is solid.
- Object layer `Spawn Points`: point objects named `<Unit> Spawn`. `Player Spawn` is the ship start. Units: Graviton, Repulsor, Gun Turret, Launcher, Volcano, Prison, Fuel, Flying Turret, Flying Launcher, Mother Ship, Tree, Building. Typed properties (Strength, TriggerRange, Agression/Aggression, Captives, Fuel) are kept. The old name `Flyer Spawn` still works but gives a warning.
- Every other object layer: tile objects are the background art (flip flags are honoured).
- Optional map properties `Gravity` and `ThrustAccel` set the level's physics. Without them, level 1's tuned values apply.

Units are only drawn as markers so far (green prison, cyan fuel, magenta graviton or repulsor, orange volcano, red anything else).

## Test

```
dotnet run --project tests/Freuds.Tests
```

A plain console runner with no NuGet packages. The exit code is the number of failed checks.

## Layout

| Project | Contents |
| --- | --- |
| `src/Freuds.Core` | Model, events, physics, terrain, demo level. No graphics dependency. |
| `src/Freuds.Game` | MonoGame front end: view, sprite pool, input controller, game class. |
| `tests/Freuds.Tests` | Checks for the event manager, terrain, flight, landing and crashing, the cannon, and the TileMap importer (including the real example map). |

How it follows the design doc:

- The controller (`InputController`), model (`World`, `Ship`, `PlasmaBolt`) and view (`GameView`) talk only through `EventManager`.
- The model never sends messages to the view. On each `TickEvent` the view reads the model's collections of moving objects and rebuilds its sprites from a reuse pool (`SpritePool`).
- Physics runs at a fixed 60 updates per second.

## Tuning

All physics numbers (gravity, thrust, fuel burn, landing limits, bolt speed and so on) are in `src/Freuds.Core/Model/GameConfig.cs`. They are first guesses. Gravity and engine thrust are also per-level: see `LevelSettings` and `DemoLevel.Settings` (level 1 uses half the default gravity and 20 percent more thrust). The landing area rule (`Terrain.IsFlatLandingSpot`) is also a placeholder until levels mark pads.

## Not done yet

Hostages, prisons and the other units, bombs, shield, the mother ship itself, menus and campaigns. See the design doc milestones.
