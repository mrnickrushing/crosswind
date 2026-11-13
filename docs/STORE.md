# Store, purchases and App Store Connect

App Store Connect app for bundle id `com.rushingtech.crosswind`, team PH4AKDQ4Q7. Nothing here submits the app for
review: that is done on the website when the game is ready.

## In-app purchases

Products are `Sim.Products` (`Assets/Scripts/Core/Sim.Store.cs`); `Tools/appstore/iap.py` has the same list.

| Product id | Type | Price | Contents |
|---|---|---|---|
| crosswind.gems.small | Consumable | $1.99 | 80 gems |
| crosswind.gems.medium | Consumable | $9.99 | 500 gems |
| crosswind.gems.large | Consumable | $19.99 | 1,400 gems |
| crosswind.starter | Non-consumable | $4.99 | Founder's Pack: 300 gems (paid with the purchase only; a restore brings back the crew, not the gems) and a third building crew |
| crosswind.double | Non-consumable | $9.99 | Golden Runway: all income doubled |

Purchases go through `Assets/Scripts/Game/App/Store.cs`: RevenueCat on device (`RevenueCatStore`, compiled when the
package defines `CROSSWIND_REVENUECAT`), a test store with instant pretend purchases in the Editor only
(device builds, TestFlight included, use the App Store sandbox), and nothing at all on a device whose store isn't
working (never free). `Sim.BeginPurchase` notes a
purchase before it starts, `Sim.Grant` pays out once per App Store transaction, and `Sim.ReconcilePurchases` delivers
one that was charged but never reported (a crash, Ask to Buy) at the next launch. `Sim.RestoreOwned` brings the two
one-time products back (EMPIRE > Restore purchases, and automatically on Sign in with Apple).

RevenueCat: project **Crosswind** (`756e2394`), App Store app `app1870f45582`, using the team's in-app purchase key
(8V5W657NH4) and App Store Connect API key (CBXMPCMX4L). Its public Apple SDK key is `RevenueCatStore.ApiKey`
(made to ship in the app). The game buys and restores by product id, so no entitlements or offerings are needed;
import the products in RevenueCat (Product catalog > Products > Import) once they exist in App Store Connect.

## Game Center

`Assets/Scripts/Game/Platform/GameCenter.cs` reports three leaderboards (`crosswind.board.flights`, `.wings`,
`.topdays`) and every in-game achievement (`crosswind.ach.<id>`, points = its gems). They are created from
`docs/appstore/gamecenter.json` (pictures in `docs/appstore/gamecenter/`, painted by `Tools/art/gc_badges.py`);
`GameCenterTests` fails if the file and `Sim.Achievements` drift apart. After adding an achievement: add it to the
JSON, run `gc_badges.py`, run the setup job again.

## Setting App Store Connect up

The App Store Connect API key lives only in the Codemagic app **train** (variable group `ios_credentials`), so the
jobs run there from the train repo's branch `codemagic/crosswind`, reading this (public) repo:

- `crosswind-setup` runs `Tools/appstore/` in order: `capabilities.py` (iCloud, Sign in with Apple, Game Center on
  the app ID), `iap.py` (the five purchases with names, prices, every territory and the review picture
  `docs/appstore/iap_review.png`), `gamecenter.py`, `listing.py` (`docs/appstore/listing.json`: categories, age
  rating, subtitle, description, keywords, URLs, free price, availability, and `docs/appstore/screens/`). Every
  script leaves what is already there alone, so the job is safe to run again. `CROSSWIND_STEPS` picks steps,
  `CROSSWIND_REF` a branch of this repo.
- `crosswind-testflight` downloads the Xcode project exported by `Tools/ios-export.sh` (release asset `ios-latest`),
  numbers the build after the latest one, signs and uploads it for internal testers only. Ask before running it.

Screenshots: Play mode, then `Tools/store-shot.sh <iphone|ipad> out.png` (2868x1320 and 2752x2064), saved as JPG in
`docs/appstore/screens/`.

App Privacy (published 2026-11-12 on the website): linked to the player and used for app functionality: Name (the
manager's name friends see), Gameplay Content (airport name, stars, flights shown to friends), User ID (RevenueCat app
user id, the friends account), Purchase History (RevenueCat); not linked, for analytics: Device ID (the hashed
statistics id), Product Interaction and Performance Data (frame rate). No tracking. Keep it in step with
`docs/privacy.html` and with anything new the game sends.

The age rating answers `userGeneratedContent` yes (friends see each other's chosen names, screened by
`FriendRules.Rude` / the Worker's `rude`, with REMOVE / BLOCK / REPORT on every friend). The app is offered everywhere
but China mainland (a game with in-app purchases needs an approval number there).

Left for the website before submitting: picking the build, adding the in-app purchases and Game Center items to the
version, review contact and notes.

Site (GitHub Pages, branch `gh-pages`, copied from `docs/`): https://mrnickrushing.github.io/crosswind/ with
`privacy.html` and `support.html`.
