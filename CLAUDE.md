# Crosswind: working notes for coding agents

Airport tycoon for iPhone and iPad (portrait), Unity **6000.0.85f1**, URP. Rushing Technologies. Working title.
Design: `docs/DESIGN.md` (pitch, systems, monetisation, milestones). Original names, airlines and aircraft only: no
real brands, nothing recreated from an existing game. Free models only (CC0 first; CC BY credited in `CREDITS.md`
and in the game); never paid AI art without asking.

- `Assets/Scripts/Core` (no Unity dependency, asmdef `Crosswind.Core` with `noEngineReferences`):
  - `Content` (aircraft classes A-E, facilities and their costs, airlines, routes, tuning constants),
  - `GameState` (the save: plain fields and lists so JsonUtility can write it; `Repair` fixes older saves),
  - `Sim` (the tick: arrivals per signed route, runway and stand allocation, the turnaround phases, service jobs
    dispatched by a tap or by a hired manager), `Sim.Economy` (what a flight pays, satisfaction, rating and stars,
    managers, `IdleRate` and offline earnings), `Goals` (the guided chain), `Format` (money and time for the screen).
  - Randomness is an xorshift seed kept in the save, so a run replays exactly.
- `Assets/Tests/EditMode`: `SimTests` (rules), `PacingTests` (a greedy player; prints the first hour's timeline:
  run it after any tuning change).
- `Assets/Scripts/Game` (everything is built from code at runtime; `GameRoot` boots in any scene):
  - `App/GameRoot` (creates the `Sim` from the save, light, camera, airport, HUD; ticks and saves), `App/SaveSystem`
    (JSON in persistentDataPath), `App/CameraRig` (three-quarter view; `Pan`, `Zoom`).
  - `World/Layout` (where the runway, taxiway, stands and terminal are, and the taxi paths), `World/AirportView`
    (rebuilds the static airport when a level changes; one `PlaneView` per flight), `World/PlaneView` (turns a
    flight's phase and timer into a position: holding pattern, landing, taxi, parked with service trucks, take-off),
    `World/Models` (aircraft by class in airline colours, trucks, blocks: placeholders until free models replace
    them), `World/MeshBuilder` + `Resources/Shaders/VertexLit` (vertex-coloured meshes, from Last Tide).
  - `UI/Ui` (canvas 1080x1920 expanding, rounded panels, labels, buttons; `DragPad` pans and pinches the camera
    through UI events: the project uses the new Input System only), `UI/Hud` (money, income, stars, guide card,
    service buttons over waiting aircraft, pay pop-ups, toasts), `UI/Hud.Sheets` (BUILD, AIRLINES, STAFF, the
    welcome-back card).
- In the Editor: `Crosswind.Game.GameRoot.Speed` fast-forwards the simulation; delete
  `~/.config/unity3d/Rushing Technologies/Crosswind/crosswind_save.json` for a new game.

## Verify every change

```bash
Tools/core-test.sh                           # Core tests without Unity (finds a .NET 8 SDK)
Tools/core-test.sh --logger "console;verbosity=detailed" --filter "Name~FirstHour"   # the pacing timeline
unity pipeline install --project-path .     # once per clone (local dev tool, never commit it)
unity open . --until-ready
Tools/unity-recompile.sh
Tools/unity-test.sh                          # EditMode tests in the Editor, or batch mode if none is open
```

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`; don't commit `Packages/packages-lock.json` or
  `ProjectSettings/Packages/com.unity.pipeline/`.
- Core tests passing; once there is a Game layer: recompile with 0 errors and Play mode with 0 Console errors.
- Commit `.meta` files.
