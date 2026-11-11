# Crosswind: Airport Tycoon — game design

Working title. iPhone and iPad, portrait, Unity 6000.0.85f1 (URP). Rushing Technologies.
Original setting, names, airlines and aircraft: nothing recreated from an existing game, and no real airline or
manufacturer brands.

## The pitch

You inherit a grass airstrip with one shed and one debt. Flight by flight you turn it into an international hub:
lay runways and taxiways, build the terminal room by room, sign airlines, keep turnarounds on time through storms and
snow, hire a crew of characters, and when the airport is running itself, open the next one somewhere harder.

It is a management tycoon with an idle heart: everything you build keeps earning while the app is closed, but the
airport is a real simulation with bottlenecks you can see and fix, so there is always a reason to come back and a
smarter move to make.

## Why this, commercially

- Tycoon/idle management is one of the steadiest-earning genres on iOS: long retention, a wide audience, and it
  monetises with rewarded ads plus IAP without needing PvP or servers.
- Airports are a strong theme for it: everyone understands the fantasy, the progression ladder is natural (prop
  planes to jumbos, airstrip to hub), and the systems interlock (airside, terminal, airlines, staff).
- It reuses what Last Tide already proved in this studio: a Unity-free simulation core with tests, a world built from
  code out of free CC0/CC BY models, RevenueCat purchases, iCloud saves, a play-testing bot, own play statistics.

## Pillars

1. **You can see the problem.** Queues at security, planes holding short of a busy runway, a stand blocked by a late
   fuel truck. Every number in a panel has something moving in the world behind it.
2. **Always one more thing.** At any moment there are three clocks running: seconds (a flight to turn around),
   minutes (a building finishing, a contract to fulfil), days (the season, the next airport).
3. **Depth you grow into.** The first ten minutes need one finger and no reading. Hour ten has schedules, pricing,
   staffing and research. Nothing is hidden behind a wiki.
4. **Generous, then worth paying for.** No energy wall on the core loop. Money buys speed, convenience and
   cosmetics, never the only way past a wall.

## The core loop (seconds to minutes)

1. A flight is on approach. It needs a free **runway**, a **stand** of its size, and the **services** its airline
   contracted (fuel, baggage, catering, cleaning, de-icing).
2. It lands, taxis, parks. Passengers deplane into the **terminal**; departing passengers have been flowing the other
   way: check-in, security, lounge and shops, gate.
3. The turnaround runs. Each service is a crew and a vehicle that has to get there. On time = full fee and a
   reputation tick with the airline. Late = a smaller fee and an annoyed airline.
4. It pushes back and takes off. You are paid: landing fee, passenger fee, fuel margin, retail spend.
5. You spend: another stand, a second security lane, a faster fuel truck, a new airline contract. The airport's
   **rating** rises, which unlocks bigger aircraft and better airlines.

Early on the player taps to dispatch services (a satisfying, teachable loop). Hiring a **manager** for a department
automates it, which is the genre's classic idle hand-off: you stop doing the job and start designing the system.

## Systems

### Airside
- **Runways**: grass, gravel, asphalt, concrete; length sets the largest aircraft class; lighting and ILS allow
  night and low-visibility operations. One runway handles one movement at a time: the first real bottleneck.
- **Taxiways and stands**: stands come in size classes A-E; remote stands need buses, contact stands need a jet
  bridge and earn more. Layout matters: a long taxi route is slow money.
- **Services**: fuel farm and trucks, baggage tugs and belts, catering kitchen, cleaning crews, pushback tugs,
  de-icing pads, fire station, maintenance hangar (aircraft stay overnight and pay rent). Each has vehicles, speed,
  capacity and a level.
- **Tower**: controls how many aircraft can be on approach and on the ground at once; upgrades add radar and a
  second controller.

### Terminal
- Built room by room on a grid: check-in desks, security lanes, immigration (international flights), baggage
  reclaim, gates, seating, toilets, lounges, shops and restaurants, information desks.
- **Passengers** are simulated as flows with a visible sample walking: each has patience and a wallet. Short queues
  and good amenities raise **satisfaction**, which multiplies retail spend and the airport rating.
- **Retail** is its own tycoon layer: choose which tenant goes in each unit (coffee, books, duty free, noodle bar),
  each with a rent, a sales share and a passenger type it suits.

### Airlines and flights
- A cast of original airlines (a bush-plane mail outfit, a holiday charter, a budget carrier, a flag carrier, a
  cargo line), each with a personality: what they fly, what they pay, what they will not tolerate.
- **Contracts**: an airline offers a route (aircraft class, flights a day, required services, fee). You accept the
  ones your airport can actually handle. Reputation with an airline unlocks its better routes.
- **Aircraft classes** A (9-seat prop) to E (jumbo), plus cargo and VIP jets; each new class is a visible milestone
  and needs infrastructure the last one did not.
- **Schedule**: later, a day planner to slot flights against runway and stand capacity (depth for the engaged
  player; an auto-planner for everyone else).

### Economy
- Income: landing and stand fees, passenger service charge, fuel margin, retail rent and share, hangar rent,
  cargo handling. Costs: staff wages, maintenance, loan interest.
- **Pricing levers** with trade-offs (raise landing fees: more per flight, fewer contracts offered).
- **Offline earnings**: the simulation is summarised analytically while away (capped at 2 h, extendable by
  upgrades), shown on a welcome-back card.
- Numbers grow far (idle-genre big numbers), with a clean suffix formatter.

### People
- **Managers** (collectible characters, four rarities): each automates a department and adds a bonus; duplicates
  level them up. Earned from play first, from crates second.
- **Staff levels** per department, trained over time.

### Progression
- **Airport rating** (1-5 stars with sub-levels) gates aircraft classes, airlines and buildings.
- **Research tree**: operations, passenger experience, commerce, sustainability. Long timers, real choices.
- **Goals**: a guided chain of missions that always says what to do next, daily tasks, achievements.
- **New airports (prestige)**: sell the stake in a finished airport for **Wings**, a permanent multiplier and
  currency, and start a harder map: island, desert, alpine valley, arctic, megacity. Each map has its own twist
  (short runway, sandstorms, altitude, snow, no land to expand).
- **Your own airline** (late game): buy aircraft, open routes from your airports, a second economy on top.

### Live layer (no server needed)
- **Weather and time of day**: fog, storms, snow, heat; each stresses a different system.
- **Incidents**: a medical diversion, a VIP arrival, a strike, a bird-strike inspection; short decisions with
  rewards.
- **Events** on a calendar cycle (holiday rush, air show, cargo week) with milestone rewards; a **season pass**
  with free and premium tracks.

## Monetisation

- **Rewarded ads** (opt-in only): double offline earnings, a 2x income boost for a few hours, skip a short timer,
  a free crate a day. No forced interstitials.
- **IAP**: gems (hard currency) in packs; a one-time starter pack; a permanent 2x income; remove-ads (grants the ad
  rewards without watching); season pass premium; manager crates with **published odds**.
- **Rules**: everything in a crate is also earnable by play; no pay-only walls; timers are skippable with earned
  gems too; a spending confirmation on gem purchases over a threshold.
- RevenueCat for purchases, Sign in with Apple and iCloud saves as in Last Tide.

## Retention design

- First session: three flights turned around by hand inside two minutes, the first manager inside ten, a clear
  next unlock always on screen.
- Return reasons at 1 h (offline cap), 8 h (research, big builds), 24 h (daily tasks, free crate), 7 d (event
  milestone), 28 d (season).
- Local notifications for: offline cap reached, research done, event ending. Asked for after the first real
  build, never on launch.
- Own play statistics (the Last Tide Worker pattern) to see where players stop.

## Art

Free models only (CC0 first, CC BY credited in CREDITS.md and in the game): Kenney kits (city, vehicles, people),
Quaternius, Poly Haven textures and props, Sketchfab CC0/CC BY aircraft and airport vehicles. The world is built
from code at runtime on a grid, as in Last Tide. Camera: a three-quarter view over the airport, pinch to zoom, drag
to pan, tap to select.

On size: depth comes from systems and content count, not megabytes. A lean download (well under 300 MB) installs
far more often than a large one, so the target is as much content as the design above in as small a build as
possible; heavy optional content (extra maps) can ship as on-demand resources later.

## Technical architecture

- `Assets/Scripts/Core` (no Unity dependency): content tables, the simulation (`Sim`: tick, flights, turnarounds,
  passenger flows, economy), offline catch-up, goals, save state. Deterministic given a seed; covered by tests that
  also run under plain .NET.
- `Assets/Scripts/Game`: world building from code, vehicles and aircraft movement, camera, HUD.
- Tools copied from Last Tide: Editor scripts, test runners, capture, asset import pipelines.

## Milestones

1. **Core sim + tests**: flights, runway, stands, services, money, rating, offline earnings. (this session)
2. **First playable**: the airstrip in 3D, planes landing and leaving, tap-to-service, build menu, HUD.
3. **Terminal and passengers**, managers and automation, goals chain.
4. **Airlines and contracts**, research, weather, incidents.
5. **Second map and prestige**, events, season, store, saves, statistics.
6. **Polish, balance bot, TestFlight.**

## Depth target (added 2026-11-10)

The owner pointed at the mobile "Airport Simulator" tycoons as the level of depth wanted. What that genre's deeper
entries have, as general ideas (nothing of theirs is copied: names, airlines, art, layouts and numbers here are ours):

- airlines as partners with a satisfaction that depends on punctuality and service quality, contracts that are
  negotiated, and the risk of losing a partner by over-committing: **in** (`Sim.Airlines`: relations, terms, walking);
- passengers with visible needs, served by check-in, security, gates and amenities (cafes, plants, seating): the
  terminal is levels today; **next**: a terminal interior you lay out, passengers that walk it, and needs to answer;
- a flight schedule the player plans against runway and stand capacity: **next** (a day planner over the routes);
- construction and deliveries that take time and continue while away: research does; **next**: build times;
- missions and a daily sign-in reward: tasks and achievements are in; **next**: a sign-in calendar.

Reviews of the genre complain about upgrades priced to push purchases and about unclear mechanics, so this game
keeps: every block says why it is blocked, no pay-only walls, and prices a player can reach by playing.
