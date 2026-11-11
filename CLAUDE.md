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
- Depth (Core): `Sim.Progress` (research effects via `Fx`, manager training, boosts, selling up for Wings and the six
  maps), `Sim.Live` (weather, opportunities, daily tasks, sign-in calendar, achievements), `Sim.Schedule` (the day planner from 3 stars: six-hour slots that pay more and fly 3x the pace, `HourlyLoad` against `HourlyCapacity`, `AutoPlan`; UI in `Hud.Plan`), `Sim.Crew` (16 collectible team members in four rarities from crates with printed odds and a sure Epic by the tenth; copies level them, only those on duty count through `Fx`; UI in `Hud.Crew`), `Sim.Airlines` (airline
  mood, contract terms, an airline walking), timed builds with two crews (`Sim.Upgrade`, `BuildSeconds`; tests set
  `BuildTimeScale = 0`), passenger `Needs` behind `Satisfaction`. Real time comes from `Sim.RealNow` (tests replace it).
- Art: free models only. `Tools/art/kenney.py <kit>` downloads Kenney kits (CC0) to `art-src/free`;
  `Tools/art/models.py` converts the ones listed in it through `Tools/blender/model_import.py` into
  `Resources/Models/<id>.fbx`: vertex-coloured, one unit on the longest side (aircraft: one unit long, nose +Z via
  `--nose`; people: one unit tall, frozen in a clip frame via `--pose walk:4`). Kit cars and people face -Z. Draw
  them with `Models.Kit(id, parent, at, yaw, size)`. Keep `CREDITS.md` and the in-game credits (EMPIRE) current with what is actually shipped.
  `App/Look` is the post-processing and weather grading; `World/Life` the passengers and traffic; `World/WeatherFx`
  rain and snow; `App/Sfx` sound (interface clips + synthesised aircraft).
- Look: isometric `CameraRig` (pitch 40, yaw -42, FOV 22); `Ground` lays textured quads with world-space UVs
  (`Materials.Surface(name, tint)`: Asphalt, Tarmac, Concrete, Grass from `Tools/art/surfaces.py`; tints are separate
  materials because URP Lit ignores vertex colours); HDRI sky in `Resources/Sky`; SSAO added to the renderers by
  `Crosswind.EditorTools.UrpSetup.Run()`. `World/Aircraft` builds every aircraft in code with a livery per airline
  (the imported Sketchfab planes and the Kenney works buildings were removed). `World/Interior` is the terminal cutaway at
  `Interior.Origin` (INSIDE button, `CameraRig.SetInside`): furniture by level, a crowd drawn with
  `Graphics.DrawMeshInstanced`. `UI/Hud.Guide` is the first-minute pointer and the tap-an-aircraft card.
  `App/Store` stubs purchases in the Editor and development builds (`Sim.Grant` is the real grant, once per
  transaction); the App Store products in `Sim.Products` still have to be created and wired to RevenueCat.
- `Game/Debug/PlayBot` (Editor): `PlayBot.Begin(30)` plays the real game greedily at 30x; `PlayBot.Report()`.
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
- Accounts (`Game/Platform`): the save also lives in the player's iCloud key-value store (`CloudSave` + `Plugins/iOS/CrosswindCloud.mm`, rules in `Core/CloudRules`: a new phone adopts it, a newer save from another device is offered by `Hud.OfferCloud`, never overwritten); local reminders (`Reminders`, `Sim.ReminderPlan`; permission asked after the first build over a minute; Settings switch). `Editor/IosCapabilities.cs` writes `Crosswind.entitlements`; the bundle id needs iCloud enabled before a signed build. Committing `Packages/manifest.json`: stage it from HEAD plus your line so `com.unity.pipeline` stays out.
- Never add a URP shader to Always Included Shaders (`BuildScript.Shaders`): every variant gets built (URP/Lit: 590,000, about a day). Materials made at runtime from URP shaders copy an asset in `Resources/Materials` (`BuildScript.EnsureMaterials`: `Surface.mat`, `Weather.mat`). A healthy iOS export takes about 80 seconds; the build rewrites `Assets/Settings/*` and a few ProjectSettings files, `git checkout` those afterwards.
- Interface art is our own, painted by `Tools/art/ui_art.py` into `Resources/UI/Fx` (glossy bubble, button, plate, rim, coin, gem, star, crate, service icons, rays): `Ui.Art`/`Ui.ArtImg` nine-slice it; every solid `Ui.Button` uses the glossy button. Feedback lives in `UI/Hud.Feedback.cs` (chips for pay, rating and goal rewards, toast, the star banner). `UI/LoadingScreen.cs` covers launch (`GameRoot.Launch` builds the airport a step a frame behind it; `_ready` holds `Update`): key picture `Resources/UI/Loading/hero.jpg` (a golden-hour capture of the game), the name, a bar with an aircraft, a tip and the Rushing Technologies logo (`Resources/UI/Brand/rushingtech.png`, on every game's loading screen).
- Since 2026-11-10 the game is LANDSCAPE (iPhone and iPad; canvas 1920x1080, `Ui.SafeArea`). HUD (`UI/Hud.cs`): a navigation rail on the left (`TabNames`, plus TERMINAL/AIRFIELD), a compact status block and the guide card top-left, clock top-centre, the flight board top-right (`Hud.Board.cs`), drawers down the right side. Scratch captures use 1920x1080.
- The camera looks from the airfield toward the terminal (`CameraRig.Yaw = 128`): stands are one row of at most six, nose in, with jet bridges (`Sim.MaxStands`, `Layout.PerColumn`); stand levels past six speed every turnaround instead (`Sim.StandBoost`).
- The terminal is furnished by the player (`Core/Sim.Terminal.cs`): 23 `Amenities` placed on the concourse grid (`FloorCols` x `FloorRows`, 2 m cells; `Place`, `MoveAmenity`, `UpgradeAmenity` to level 3, `SellAmenity`, `PlaceAnywhere` for bots), eight `Needs` (queues + `Want`s) behind `Satisfaction`; `RetailPull`/`LoungeLevel` replace the old shops/lounge facility levels (the shops, seating, info and lounge facilities are gone). `World/Interior.cs` draws them (`Amenity`, `FloorPoint`, `CellAt`, `ItemAt`, `AgentNear`); `UI/Hud.Terminal.cs` is the SHOP, placing (tap floor, ROTATE, PLACE), the item card (upgrade/move/sell), the ratings strip and the passenger card (a tap on a passenger: their own star ratings and a remark).
- Accounts and friends: `Platform/AppleAccount.cs` (Sign in with Apple via `com.lupidan.apple-signin-unity`; a stand-in account in the Editor), `Platform/FriendsClient.cs` -> the Worker `crosswind-friends` (`Tools/friends-worker`, D1 `crosswind-friends`, https://crosswind-friends.mrnickrushing.workers.dev), rules in `Core/Friends.cs`, `Core/Sim.Social.cs` (name, a gift of gems from each friend every 20 h), UI `Hud.Friends.cs` (code, add/remove, ranking, sign out, delete account; `Prompt` is the text-entry box). Not done: revoking the Apple token on account deletion (needs a server-side Apple key).
- Finance (`Core/Sim.Finance.cs`, EMPIRE): fee policy (`SetFees`, five levels from 3 stars), one bank loan at a time repaid from a fifth of every payment (`TakeLoan`, `RepayLoan`), income by source (`State.incomeBy`).
- People are Quaternius characters (CC0; `Tools/art/people.py` downloads, `Tools/art/models.py` poses and slims them to `person_N_*`, `worker_*`); `Portraits` renders the team's faces from them.
- If the Editor stops answering the pipeline after a restart, look for a modal dialog (`wmctrl -l`), e.g. "URP Material upgrade"; keep `ProjectSettings/URPProjectSettings.asset` committed as the Editor writes it.
- Ground operations (`Core/Sim.cs`, `GroundTests`): after boarding (`FlightState.boarded`) a flight still needs, in
  order, de-icing (only in snow, only with a `deice` pad; that time is not counted against it) and a pushback tug
  (`Service.Pushback` is in the `Needs` of every class C+ route, so the `pushback` department must exist to sign
  one). Then it keeps its gate up to `HoldLimit` seconds (`FlightState.held`, also not counted as late) while a
  stand is spare, so the apron stays full without costing throughput. `Content.Departments` lists the six crewed
  departments (managers, depots, `IdleRate` crew loads). View: `PlaneView.Trucks` (tug on the nose, also through
  the push back; `Spray` for de-icing), bubbles and board statuses in `Hud.cs` / `Hud.Board.cs`.
- Ground support vehicles are built from code in `World/Gse.cs` (bowser, baggage train, catering high-loader, cabin
  van, pushback tug, de-icer; all face -Z, about 4 m long). `PlaneView.Trucks` parks the first crew on the service
  side (-Z, away from the bridges), the next under the other wing aft of the bridges.
- Airside buildings are code too: `World/Works.cs` (`Depot` per department with its yard, `Hangar`, `Cargo`) and
  `AirportView.Bridge` (boarding bridges; `HasBridge(stand)` stops `Life` walking passengers over the apron). The
  camera sees north (+Z) and west (-X) faces, so fronts point that way (hangar and cargo are turned with RotateY(90)).
- Fuel market (`Core/Sim.Fuel.cs`, `FuelTests`): `FuelPriceOn(day)` (0.65..1.4, par 1, one price per 12-minute
  day), tanks measured in money at par (`FuelCapacity` grows with the fuel depot and the biggest aircraft class),
  `BuyFuel(to)` pays stock x price, each fuelled departure draws `FuelDraw` and is paid it back (`DrawFuel` in `Pay`);
  dry tanks leave `DryMargin` of the fuel margin. A fuel manager reorders to 60% under 25% when the price is <= 1.08.
  `FlightValue` and the idle rate are unchanged (neutral at par). UI: FUEL MARKET at the top of BUILD.
- Wide-body stands (facility `heavy`, 5 stars, max 6): class D and E (`Sim.Heavy`) park only on the first
  `HeavyStands` stands of the row; smaller aircraft fill the ordinary ones first and leave a wide-body stand at once
  when one is circling (`WideBodyWaiting`). `Sim.Missing(route)` lists the facilities a route still waits for (the
  bot and the pacing test buy from it). Old saves with a D/E route get two. `AirportView` draws the pad, the second
  stop bar and the second jet bridge.
- The country round each airfield is `World/Scenery.cs` (farmland, alpine forest and peaks, a city grid, palms and
  islets, dunes and mesas, drifts and ice), filled into the rectangles `AirportView` passes it; town buildings
  are `Scenery.Building` / `House` (window ribbons, some lit at night via `mb.Glow`). Keep ground layers at least
  5 cm apart (they flicker at distance otherwise) and check `UnityEditor.UnityStats.triangles` per map (about
  1.0-1.5M including shadow passes). `MeshBuilder.Sphere` gives squashed balls correct normals.
- Menu rows (`Hud.Sheets.cs`): `Ico("name", tint).Bar(0..1).Entry(...)` adds an icon tile and a progress bar.
  Glyphs are cut from Font Awesome Free (CC BY 4.0, credited) by `Tools/art/icons.py` into `Resources/UI/Ic`.
  In-game credits are rows under EMPIRE; anything CC BY must be listed there and in `CREDITS.md`.
- Music is synthesized by `Tools/art/music.py` (numpy; nothing licensed) into `Resources/Audio/Music/day|night.ogg`
  (streamed, see `ArtImport`); `Sfx.TickMusic` cross-fades them with `Sim.Night`; Settings has a Music switch.
- Road cars are Quaternius (CC0, `Tools/art/cars.py`); the bus, lorries and cones are code (`Gse.Bus`, `Lorry`, `Cone`). No Kenney models are left in the build.
