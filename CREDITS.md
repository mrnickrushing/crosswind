# Credits

Crosswind uses free 3D models, textures, icons and fonts. Thank you to their authors. Aircraft, airport vehicles, the terminal building and the airfield buildings are modelled in code and are our own.

## Fonts

- Barlow Condensed and Barlow Semi Condensed by Jeremy Tribby, SIL Open Font License 1.1 (`Assets/Resources/Fonts/Barlow-OFL.txt`).

## People

Passengers and ground crew: characters by **Quaternius** (CC0, via poly.pizza): Woman in Dress, Man in Long Sleeves, Man (two), Man in Suit, Woman in Tank Top, Animated Woman, Woman Casual, Woman, Casual Character, Business Man, Hoodie Character, Worker. Posed and slimmed by `Tools/art/models.py`.

The staff's portraits (the sixteen team members and the six department managers) are our own: painted for Crosswind as two sheets with Higgsfield (GPT Image 2.5) and cut into tiles by `Tools/art/portraits.py`. They show invented people.

## Cars

Road cars (saloon, hatchback, SUV, taxi, pickup, coupe): **Quaternius** (CC0 / public domain, via poly.pizza), fetched by `Tools/art/cars.py` and slimmed by `Tools/art/models.py`.

## Terminal furniture

Everything that stands in the terminal (counters, chairs and sofas, shelves and tills, arcade machines, plants and palms, the piano, the fountain, the fish in the aquarium, the bags on the belts) is a free 3D model. `Tools/art/props.py` downloads them and records each licence, and `Tools/blender/model_import.py` converts them for the game: colours baked into the mesh, some slimmed to fewer triangles, the kits' mint foliage turned leaf green. The game sizes, tints and arranges them (`Assets/Scripts/Game/World/Furnish.cs`).

- **Kenney** (CC0, kenney.nl): Furniture Kit, Mini Market, Mini Arcade, Food Kit, Nature Kit, City Kit (Roads) for the site fencing and cones.
- **Poly by Google** (CC BY 3.0, via poly.pizza): Airplane, Analog clock, Bench, Bottle of wine, Broom, Bucket, Cell phone, Coral, Desk, Fiddle-leaf Plant, Fish, Fountain, Garbage, Goldfish, Jet, Koala, Luggage, Mirror, Panda, Roomba, Rubber Duck, Rubik's cube, Sea anemone, Seahorse, Slide, Swing set, Vacuum.
- **J-Toastie** (CC BY 3.0, via poly.pizza): ATM.
- **jeremy** (CC BY 3.0, via poly.pizza): Piano.
- **Josh** (CC BY 3.0, via poly.pizza): Conveyor (the baggage belts).
- **dook** (CC BY 3.0, via poly.pizza): Conveyor Thing (the security scanners).
- **Quaternius** (CC0): Bookcase with Books, Chukaman Steamer, Flag, Houseplant, Light Ceiling, Ramen, Udon.

## Icons

- Flat glyphs (where there is no picture icon) cut from **Font Awesome Free** 6 Solid by Fonticons, Inc. (icons CC BY 4.0, https://fontawesome.com/license/free), rendered to white glyphs by `Tools/art/icons.py`.

## Picture icons

The menu and rail icons are renders of free 3D models (`Tools/art/icon_models.py` downloads them and records each licence; `Tools/blender/icon_render.py` renders them into `Assets/Resources/UI/Icons`). The models themselves are not shipped, only their pictures.

- **Poly by Google** (CC BY 3.0, via poly.pizza): Airplane, Alarm clock, Bakery, Bank, Bell, Bench, Blue ribbon, Boombox, Broom, Car, Cheeseburger, Cloud, Coffee cup, Coin, Coins, Computer, Couch, Crown, Curved road, Door, Earth, Erlenmeyer flask, Gas can, Gears, Gem, Hard hat, Heart, Highway, Jet, Lightning bolt, Luggage, Paint roller, Pencil, Satellite dish, Scroll, Shopping cart, Star, Toilet, Toolbox, Tractor, Trashcan, Videogame Controller.
- **Casey Tumbers** (CC BY 3.0, via poly.pizza): Trophy.
- **Tomáš Bayer** (CC BY 3.0, via poly.pizza): Calendar.
- **William Murphy** (CC BY 3.0, via poly.pizza): clipboard.
- **jeremy** (CC BY 3.0, via poly.pizza): Hammer.
- **abass20** (CC BY 4.0, Sketchfab): Air Traffic Control Tower.
- **rikugo.studio** (CC BY 4.0, Sketchfab): Data Chart Graphic Table Infographic.
- **Quaternius** (CC0): Compass, Container Red, Cube Crate, Gold Bag, Man, Padlock, Pickup Key Card, Snowflake, Target, Woman Casual.
- **Kenney** (CC0): Signpost, Television.
- **CreativeTrio** (CC0): Present.

CC BY 3.0: https://creativecommons.org/licenses/by/3.0/ · CC BY 4.0: https://creativecommons.org/licenses/by/4.0/ · The models were rendered as pictures (lit, framed and scaled); no other changes.

## Sounds

- The terminal's crowd murmur is made from "Sound inside Tocumen airport in Panama" by **Wilfredor** (CC0, Wikimedia Commons, https://commons.wikimedia.org/wiki/File:Sound_inside_Tocumen_airport_in_Panama.flac): four stretches filtered to the murmur band, levelled and layered into a seamless loop by `Tools/art/ambience.py`.
- The music (`Tools/art/music.py`) and the airfield's jet, propeller and wind sounds (`Assets/Scripts/Game/App/Sfx.cs`) are made in code.

## Textures

- Ground, apron and floor textures from **Poly Haven** and **ambientCG** (CC0), fetched by `Tools/art/surfaces.py`.

The game lists these under EMPIRE > Credits.
