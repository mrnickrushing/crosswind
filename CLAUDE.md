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
- `Assets/Scripts/Game`: not written yet (milestone 2: the world built from code, camera, HUD).

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
