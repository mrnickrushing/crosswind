using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// What stands on the concourse floor. Every item is put together from free furniture models (<see cref="Props"/>):
    /// counters, chairs, shelves, machines, plants. Only the architecture round them (a shop's walls and fascia, a
    /// carpet, a lit sign) is drawn in code. Higher levels add more of what the item is for.
    /// </summary>
    public static class Furnish
    {
        public static readonly Color[] Brand = { Palette.Hex(0xD9432F), Palette.Hex(0x2F8F5B), Palette.Hex(0xE2A521), Palette.Hex(0x6B4FBB), Palette.Hex(0x1E8FB0), Palette.Hex(0xC2417C) };

        /// <summary>
        /// One amenity, modelled about its middle with its front toward -x (the side passengers come from):
        /// w along x, d along z.
        /// </summary>
        public static void Amenity(MeshBuilder mb, AmenityDef def, float w, float d, int level, int seed)
        {
            Color wall = Palette.Hex(0xEDEAE2), dark = Palette.Hex(0x2B3138), wood = Palette.Hex(0x7A5A3A), steel = Palette.Hex(0xB9BEC4);
            Color brand = Brand[(seed + def.Id.Length) % Brand.Length];
            Color darkWood = new Color(0.5f, 0.42f, 0.4f), plum = new Color(0.62f, 0.5f, 0.95f), wine = new Color(0.62f, 0.3f, 0.3f);
            float hx = w * 0.5f, hz = d * 0.5f;

            // Props: yaw 0 faces -x (the passengers), 90 +z, 180 +x, 270 -z.
            void P(string id, float x, float z, float yaw, float tall, float y = 0) { mb.Tall(id, new Vector3(x, y, z), yaw, tall); }
            void T(string id, float x, float z, float yaw, float tall, Color tint, float y = 0) { mb.Tall(id, new Vector3(x, y, z), yaw, tall, tint); }
            void L(string id, float x, float z, float yaw, float longest, float y = 0) { mb.Long(id, new Vector3(x, y, z), yaw, longest); }
            void F(string id, float x, float z, float yaw, float across, float tall, float deep, float y = 0) { mb.Fit(id, new Vector3(x, y, z), yaw, across, tall, deep); }
            void Ft(string id, float x, float z, float yaw, float across, float tall, float deep, Color tint, float y = 0) { mb.Fit(id, new Vector3(x, y, z), yaw, across, tall, deep, tint); }
            // A counter of bar units side by side, along z (facing the passengers) or along x (facing yaw 90 or 270).
            void CounterZ(float x, float z0, float z1, Color? tint = null, float tall = 1.05f)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / 1.1f)); float each = (z1 - z0) / n;
                for (int k = 0; k < n; k++) mb.Fit("bar", new Vector3(x, 0, z0 + (k + 0.5f) * each), 0, each, tall, 0.62f, tint);
            }
            void CounterX(float z, float x0, float x1, float yaw, Color? tint = null, float tall = 1.05f)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 1.1f)); float each = (x1 - x0) / n;
                for (int k = 0; k < n; k++) mb.Fit("bar", new Vector3(x0 + (k + 0.5f) * each, 0, z), yaw, each, tall, 0.62f, tint);
            }
            void Till(float x, float z, float yaw) { F("bar", x, z, yaw, 0.85f, 1.0f, 0.5f); mb.Wide("register", new Vector3(x, 1.0f, z), yaw, 0.42f); }
            // A table for two, the chairs either side of it along z.
            void TableForTwo(float x, float z, string chair = "chair_round")
            {
                F("table_round", x, z, 0, 0.8f, 0.74f, 0.8f);
                P(chair, x, z - 0.64f, 90, 0.88f); P(chair, x, z + 0.64f, 270, 0.88f);
                L("cup_coffee", x - 0.08f, z - 0.16f, 0, 0.13f, 0.74f); L("cup_tea", x + 0.1f, z + 0.18f, 0, 0.13f, 0.74f);
            }
            void Lamp(float x, float z, float y, Color? tint = null) { mb.Glow = true; mb.Tall("lamp_ceiling", new Vector3(x, y, z), 0, 0.8f, tint); mb.Glow = false; }
            void FloorLamp(float x, float z) { mb.Glow = true; P("lamp_floor", x, z, 0, 1.75f); mb.Glow = false; }

            void Sign(float y, Color c) { mb.Box(new Vector3(-hx + 0.12f, y, 0), new Vector3(0.24f, 0.6f, d - 0.3f), c); if (level >= 2) { mb.Glow = true; mb.Box(new Vector3(-hx - 0.02f, y, 0), new Vector3(0.05f, 0.3f, d * 0.55f), Palette.Hex(0xFFF2C8)); mb.Glow = false; } }
            void Unit(Color c)       // a shop unit: back and side walls, a fascia, a canopy at level 3
            {
                mb.Box(new Vector3(hx - 0.15f, 1.5f, 0), new Vector3(0.3f, 3.0f, d - 0.1f), wall);
                // Side partitions are glass above a low plinth, so what is inside shows.
                foreach (float e in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(0, 0.25f, e * (hz - 0.1f)), new Vector3(w - 0.3f, 0.5f, 0.16f), wall);
                    mb.Smoothness = 0.92f; mb.Box(new Vector3(0, 1.3f, e * (hz - 0.1f)), new Vector3(w - 0.3f, 1.6f, 0.05f), Palette.Hex(0x9FD3E8)); mb.Smoothness = 0.3f;
                    mb.Box(new Vector3(0, 2.15f, e * (hz - 0.1f)), new Vector3(w - 0.3f, 0.1f, 0.16f), c);
                    mb.Box(new Vector3(-hx + 0.12f, 1.25f, e * (hz - 0.1f)), new Vector3(0.24f, 2.5f, 0.24f), c);       // the fascia's posts
                }
                mb.Box(new Vector3(hx - 0.32f, 1.5f, 0), new Vector3(0.05f, 2.9f, d - 0.3f), MeshBuilder.Lighten(c, 0.25f));       // the back wall in its colour
                Sign(2.75f, c);
                if (level >= 3) mb.Box(new Vector3(-hx - 0.5f, 2.35f, 0), new Vector3(1.2f, 0.08f, d - 0.2f), c);
            }

            mb.Mat(0.3f);
            switch (def.Id)
            {
                case "vending":
                    if (level == 1) P("vending", 0.3f, 0, 0, 1.75f);
                    else { P("vending", 0.3f, -0.42f, 0, 1.7f); P("atm", 0.4f, 0.62f, 0, 1.75f); }
                    if (level >= 3) P("gacha", -0.5f, 0.72f, 0, 1.15f);
                    break;
                case "cafe":
                    {
                        Unit(Palette.Hex(0x4A2E20));
                        float cx = hx - 1.75f;
                        CounterZ(cx, -hz + 0.5f, hz - 0.5f);
                        P("coffee_machine", cx, hz - 1.05f, 180, 0.48f, 1.05f);
                        mb.Wide("register", new Vector3(cx, 1.05f, 0.25f), 0, 0.42f);
                        string[] bakes = { "croissant", "donut", "muffin", "cupcake", "croissant" };
                        for (int k = 0; k < bakes.Length; k++) { L("plate", cx, -0.45f - k * 0.24f, 0, 0.22f, 1.05f); L(bakes[k], cx, -0.45f - k * 0.24f, k * 40, 0.16f, 1.07f); }
                        // Behind the counter: the fridge, cupboards with the blender and the microwave on them.
                        P("fridge", hx - 0.75f, hz - 0.85f, 0, 1.75f);
                        foreach (float z in new[] { -0.2f, -1.1f }) { F("cabinet", hx - 0.65f, z, 0, 0.88f, 0.92f, 0.6f); F("cabinet_upper", hx - 0.52f, z, 0, 0.88f, 0.55f, 0.36f, 1.6f); }
                        P("blender", hx - 0.65f, -0.2f, 0, 0.42f, 0.92f); mb.Wide("microwave", new Vector3(hx - 0.65f, 0.92f, -1.1f), 0, 0.5f);
                        for (float z = -hz + 1.0f; z <= hz - 0.9f; z += 2.0f) TableForTwo(-hx + 1.1f, z);
                        if (level >= 2) { Lamp(cx, -0.8f, 1.8f); Lamp(cx, 0.8f, 1.8f); }
                        if (level >= 3) P("plant_pot", -hx + 0.42f, 0, 0, 0.95f);
                    }
                    break;
                case "noodle":
                    {
                        Unit(Palette.Hex(0xB3261E));
                        // An open kitchen: a long bar with stools and bowls, the stoves and the steamers behind, red lamps over it.
                        float bx = hx - 2.0f;
                        CounterZ(bx, -hz + 0.45f, hz - 0.45f, darkWood);
                        int seat = 0;
                        for (float z = -hz + 0.75f; z <= hz - 0.7f; z += 0.84f, seat++) { P("stool", bx - 0.62f, z, 0, 0.72f); L(seat % 2 == 0 ? "ramen" : "udon", bx - 0.06f, z, 0, 0.3f, 1.05f); }
                        float kx = hx - 0.68f;
                        F("stove", kx, -1.25f, 0, 0.8f, 0.92f, 0.62f); F("stove", kx, -0.45f, 0, 0.8f, 0.92f, 0.62f); F("sink", kx, 0.35f, 0, 0.8f, 0.92f, 0.62f); F("cabinet", kx, 1.15f, 0, 0.8f, 0.92f, 0.62f);
                        L("pot_stew", kx, -1.25f, 0, 0.42f, 0.92f); L("pan", kx, -0.45f, 30, 0.44f, 0.92f); L("steamer", kx, 1.0f, 0, 0.36f, 0.92f); L("steamer_buns", kx, 1.36f, 0, 0.34f, 0.92f);
                        F("hood", kx + 0.05f, -0.85f, 0, 1.6f, 0.7f, 0.56f, 1.95f);
                        for (float z = -hz + 0.9f; z <= hz - 0.8f; z += 1.1f) Lamp(bx - 0.3f, z, 1.95f, new Color(1f, 0.35f, 0.25f));
                        if (w > 5f) for (float z = -hz + 1.0f; z <= hz - 0.9f; z += 2.0f) TableForTwo(-hx + 1.2f, z, "chair_wood");
                    }
                    break;
                case "restaurant":
                    Unit(Palette.Hex(0x1F3A36));
                    // Laid tables, a banquette along the back wall, the host's stand and a plant at the door, lamps over the tables.
                    foreach (float z in new[] { -1.3f, 1.3f }) Ft("sofa", hx - 0.72f, z, 0, 2.5f, 0.85f, 0.8f, wine);
                    for (float x = -hx + 1.5f; x <= hx - 2.3f; x += 2.05f)
                        for (float z = -hz + 1.1f; z <= hz - 1.0f; z += 1.9f)
                        {
                            F("table_cloth", x, z, 0, 1.05f, 0.75f, 0.8f);
                            P("chair_wood", x - 0.78f, z, 180, 0.9f); P("chair_wood", x + 0.78f, z, 0, 0.9f);
                            L("plate", x - 0.2f, z, 0, 0.26f, 0.75f); L("plate", x + 0.2f, z, 0, 0.26f, 0.75f);
                            P("glass_wine", x - 0.12f, z + 0.3f, 0, 0.18f, 0.75f); P("glass_wine", x + 0.12f, z - 0.3f, 0, 0.18f, 0.75f);
                            if (level >= 2) Lamp(x, z, 1.9f);
                        }
                    P("bar_end", -hx + 0.5f, hz - 0.7f, 0, 1.1f);
                    P("houseplant_c", -hx + 0.45f, -hz + 0.55f, 0, 1.2f);
                    if (level >= 3) { P("wine_red", -hx + 0.5f, hz - 0.62f, 0, 0.32f, 1.1f); P("fiddle", hx - 0.75f, 0, 0, 1.9f); }
                    break;
                case "newsstand":
                    Unit(Palette.Hex(0x1E5FA8));
                    // A wall of books and papers, a gondola of snacks down the middle, the till by the door.
                    P("bookcase_books", hx - 0.55f, 0, 0, 1.9f);
                    foreach (float e in new[] { -1f, 1f }) P("display_small", hx - 0.62f, e * 0.74f, 0, 1.0f);
                    P(seed % 2 == 0 ? "shelf_bags" : "shelf_boxes", 0.35f, 0, 90, 1.3f);
                    Till(-hx + 0.8f, -0.42f, 0);
                    L("books", -hx + 0.8f, -0.7f, 0, 0.26f, 1.0f);
                    break;
                case "gifts":
                    {
                        Unit(Palette.Hex(0xC2417C));
                        // Shelves along the back with soft toys on top, a table of them in the middle, two spinners, the till at the side.
                        string[] toys = { "panda", "koala", "duck", "panda", "duck", "koala" };
                        foreach (float e in new[] { -1f, 1f }) F("bookcase_wide", hx - 0.58f, e * 0.88f, 0, 1.7f, 1.7f, 0.5f);
                        for (int k = 0; k < 4; k++) P(toys[(k + seed) % toys.Length], hx - 0.58f, -1.35f + k * 0.9f, 0, 0.38f, 1.7f);
                        F("table", 0.15f, 0.25f, 0, 1.6f, 0.8f, 0.95f);
                        P("panda", 0.15f, -0.3f, 0, 0.5f, 0.8f); P("koala", 0.2f, 0.25f, 0, 0.42f, 0.8f); P("duck", 0.1f, 0.78f, 20, 0.4f, 0.8f); L("cube", -0.15f, 0.5f, 25, 0.2f, 0.8f);
                        foreach (float e in new[] { -1f, 1f }) P("display_small", -hx + 0.9f, e * 1.05f, 0, 1.5f);
                        Till(0.2f, -hz + 0.58f, 90);
                        if (level >= 2) P("gacha", -hx + 0.48f, hz - 0.45f, 0, 1.15f);
                        if (level >= 3) mb.Wide("display_flowers", new Vector3(-1.05f, 0, 0.1f), 0, 0.85f);
                    }
                    break;
                case "fashion":
                    {
                        Unit(Palette.Hex(0x22262C));
                        // Rails of clothes, a table of folded things, stands and cases at the front, a mirror, a sofa and the till at the back.
                        int rack = 0;
                        foreach (float x in new[] { -0.3f, 1.3f })
                            foreach (float z in new[] { 0f, -1.15f, 1.15f })
                                if (rack++ < 3 + level) P("clothing_rack", x, z, 90, 1.45f);
                        mb.Wide("clothing_table", new Vector3(-hx + 1.45f, 0, 0), 0, 1.25f);
                        foreach (float e in new[] { -1f, 1f }) { P("display_small", -hx + 0.7f, e * 1.3f, 0, 1.5f); P("luggage", -hx + 0.75f, e * 0.75f, 20 * e, 0.7f); }
                        P("mirror", hx - 0.5f, -0.55f, 0, 1.8f);
                        Ft("sofa", hx - 0.66f, 0.85f, 0, 1.5f, 0.8f, 0.6f, new Color(0.45f, 0.45f, 0.5f));
                        Till(hx - 0.75f, -1.45f, 0);
                    }
                    break;
                case "dutyfree":
                    {
                        // Walk-through: no walls at the sides, a bright floor, gondolas with a lit plinth, tables of bottles at the door, lit posters behind.
                        mb.Box(new Vector3(0, 0.025f, 0), new Vector3(w - 0.1f, 0.03f, d - 0.1f), Palette.Hex(0xF4F1EA));
                        mb.Box(new Vector3(hx - 0.15f, 1.5f, 0), new Vector3(0.3f, 3.0f, d - 0.1f), Palette.Hex(0x15191F));
                        Sign(2.75f, Palette.Hex(0x15191F));
                        string[] shelf = { "shelf_boxes", "shelf_bags", "display_cans", "shelf_boxes", "shelf_bags" };
                        string[] bottles = { "wine", "wine_red", "wine_white", "bottle", "wine_red" };
                        int g = 0;
                        foreach (float x in new[] { -1.6f, 0.6f, 2.6f })
                            foreach (float z in new[] { -1.9f, 0f, 1.9f })
                            {
                                if (x < -1f && z == 0f) continue;       // the way in
                                string s = shelf[(g++ + seed) % shelf.Length];
                                mb.Glow = true; mb.Box(new Vector3(x, 0.07f, z), new Vector3(1.5f, 0.05f, 1.25f), Palette.Hex(0xFFE9B0)); mb.Glow = false;
                                P(s, x, z, 90, s == "display_cans" ? 1.2f : 1.4f, 0.08f);
                            }
                        foreach (float e in new[] { -1f, 1f })
                        {
                            F("coffee_square", -hx + 1.0f, e * 1.0f, 0, 0.95f, 0.85f, 0.95f);
                            for (int k = 0; k < 5; k++) P(bottles[k], -hx + 1.0f + (k % 2 == 0 ? -0.2f : 0.2f), e * 1.0f - 0.3f + k * 0.15f, 0, 0.36f, 0.85f);
                            Till(-hx + 0.9f, e * 2.45f, 0);
                        }
                        if (level >= 2) { L("rug_round", -1.6f, 0, 0, 1.7f, 0.04f); P("luggage", -1.75f, -0.3f, 20, 0.75f, 0.05f); T("luggage", -1.45f, 0.3f, 340, 0.62f, new Color(0.6f, 0.9f, 1.2f), 0.05f); }
                        mb.Glow = true; for (float z = -hz + 0.8f; z <= hz - 0.7f; z += 1.6f) mb.Box(new Vector3(hx - 0.32f, 1.9f, z), new Vector3(0.05f, 1.0f, 1.1f), Palette.Hex(0xFFF2C8)); mb.Glow = false;      // lit posters
                    }
                    break;
                case "seats":
                    // Rows of seats on a steel beam, back to back from level 2, a plant at each end at level 3.
                    for (int side = 0; side < (level >= 2 ? 2 : 1); side++)
                    {
                        float z = level >= 2 ? (side == 0 ? -0.3f : 0.3f) : 0;
                        mb.Mat(0.7f, 0.6f); mb.Box(new Vector3(0, 0.2f, z), new Vector3(w - 0.7f, 0.06f, 0.08f), steel); mb.Mat(0.3f);
                        for (float x = -hx + 0.5f; x <= hx - 0.45f; x += 0.5f) P("chair", x, z, side == 0 ? 270 : 90, 0.88f);
                    }
                    if (level >= 3) foreach (float e in new[] { -1f, 1f }) P("plant_pot", e * (hx - 0.14f), level >= 2 ? 0 : 0.3f, 0, 0.9f);
                    break;
                case "recliners":
                    {
                        // A quiet corner: a carpet, recliners with a small table between them, tall plants behind, a floor lamp.
                        mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.3f, 0.04f, d - 0.3f), Palette.Hex(0x31506A));
                        int n = 0;
                        for (float z = -hz + 0.6f; z <= hz - 0.5f; z += 1.0f, n++) P(n % 2 == 0 ? "houseplant_c" : "fiddle", hx - 0.45f, z, 0, n % 2 == 0 ? 1.3f : 1.9f);
                        foreach (float x in level >= 2 ? new[] { 0.9f, -1.2f } : new[] { 0.9f })
                        {
                            foreach (float z in new[] { -1.25f, 0f, 1.25f }) T("recliner", x, z, 0, 1.0f, new Color(0.6f, 0.82f, 1.15f));
                            foreach (float z in new[] { -0.63f, 0.63f }) { F("coffee_square", x + 0.25f, z, 0, 0.4f, 0.45f, 0.4f); L("books", x + 0.25f, z, 30, 0.2f, 0.45f); }
                        }
                        FloorLamp(-hx + 0.5f, hz - 0.45f);
                        if (level >= 3) P("bookcase_books", -hx + 1.6f, -hz + 0.35f, 90, 1.7f);
                    }
                    break;
                case "charging":
                    {
                        // A long work bar with stools, laptops left open on it and phones on charge.
                        CounterX(0.3f, -hx + 0.25f, hx - 0.25f, 270, null, 1.08f);
                        int stool = 0;
                        for (float x = -hx + 0.5f; x <= hx - 0.45f; x += 0.75f, stool++)
                        {
                            P("stool", x, -0.38f, 270, 0.74f);
                            if (stool % 2 == 0) L("laptop", x, 0.26f, 270, 0.34f, 1.08f); else L("phone", x, 0.2f, 20, 0.16f, 1.08f);
                        }
                        mb.Glow = true; for (float x = -hx + 0.5f; x <= hx - 0.45f; x += 0.75f) mb.Box(new Vector3(x, 1.1f, 0.52f), new Vector3(0.1f, 0.04f, 0.03f), Palette.Hex(0x6BE38A)); mb.Glow = false;
                        if (level >= 2) { Lamp(-0.9f, 0.3f, 1.9f); Lamp(0.9f, 0.3f, 1.9f); }
                        if (level >= 3) P("houseplant_c", hx - 0.3f, -0.6f, 0, 1.1f);
                    }
                    break;
                case "lounge":
                    {
                        // Carpeted, behind frosted glass with a brass rail: armchairs round low tables, a bar with stools and bottles, reception, lamps.
                        mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.2f, 0.04f, d - 0.2f), Palette.Hex(0x3B2F63));
                        mb.Box(new Vector3(0, 0.035f, 0), new Vector3(w - 1.6f, 0.04f, d - 1.6f), Palette.Hex(0x4B3A78));
                        mb.Smoothness = 0.9f;
                        foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0, 0.6f, e * (hz - 0.1f)), new Vector3(w - 0.2f, 1.2f, 0.06f), Palette.Hex(0xBFE0EC));
                        mb.Box(new Vector3(hx - 0.1f, 1.4f, 0), new Vector3(0.2f, 2.8f, d - 0.2f), Palette.Hex(0x2E2540));
                        mb.Mat(0.8f, 0.8f);
                        foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0, 1.23f, e * (hz - 0.1f)), new Vector3(w - 0.2f, 0.06f, 0.1f), Palette.Hex(0xC9A24A));
                        mb.Mat(0.3f);
                        for (int k = 0; k < 3; k++)
                        {
                            float x = -hx + 1.7f + k * 2.3f, z = -hz + 1.9f;
                            F("coffee_glass", x, z, 0, 0.6f, 0.42f, 0.9f);
                            T("armchair", x, z - 0.98f, 90, 0.85f, plum); T("armchair", x, z + 0.98f, 270, 0.85f, plum);
                            P("cocktail", x - 0.15f, z, 0, 0.2f, 0.42f); P("glass_wine", x + 0.2f, z + 0.1f, 0, 0.17f, 0.42f);
                        }
                        // The bar along the far side, its shelves behind it.
                        float bz = hz - 1.25f;
                        CounterX(bz, -hx + 2.4f, hx - 0.8f, 270, darkWood);
                        string[] bottles = { "wine", "wine_white", "bottle", "wine_red" };
                        int b = 0;
                        for (float x = -hx + 2.7f; x < hx - 1.0f; x += 0.62f, b++) P(b % 3 == 2 ? "cocktail" : bottles[b % bottles.Length], x, bz + 0.1f, 0, b % 3 == 2 ? 0.2f : 0.34f, 1.05f);
                        for (float x = -hx + 2.9f; x < hx - 1.0f; x += 0.95f) P("stool", x, bz - 0.7f, 270, 0.74f);
                        for (float x = -hx + 3.2f; x < hx - 0.9f; x += 1.65f) Ft("bookcase_wide", x, hz - 0.42f, 270, 1.6f, 1.9f, 0.4f, darkWood);
                        mb.Glow = true; mb.Box(new Vector3(0.8f, 1.98f, hz - 0.66f), new Vector3(w - 3.6f, 0.04f, 0.05f), Palette.Hex(0xFFD98A)); mb.Glow = false;
                        // Reception at the door, floor lamps, a screen on the back wall.
                        F("desk_white", -hx + 0.75f, hz - 1.5f, 0, 1.4f, 1.0f, 0.7f);
                        L("screen", -hx + 0.8f, hz - 1.3f, 180, 0.45f, 1.0f); P("plant_pot", -hx + 0.6f, hz - 0.5f, 0, 0.9f);
                        FloorLamp(-hx + 0.6f, -hz + 0.5f); FloorLamp(hx - 0.6f, -hz + 0.5f);
                        if (level >= 2) { mb.Glow = true; mb.Wide("tv", new Vector3(hx - 0.36f, 1.15f, -hz + 1.9f), 0, 1.9f); mb.Glow = false; }
                        if (level >= 3) { P("fiddle", hx - 0.6f, 0.4f, 0, 2.0f); L("rug_round", -hx + 1.7f + 2.3f, -hz + 1.9f, 0, 2.6f, 0.06f); }
                        Sign(2.6f, Palette.Hex(0x6B4FBB));
                    }
                    break;
                case "toilets":
                    {
                        // A tiled block, open above so its cubicles and basins show: two doorways (blue and pink surrounds, pictogram plates).
                        Color tile = Palette.Hex(0xE4ECEF);
                        mb.Smoothness = 0.6f;
                        mb.Box(new Vector3(hx - 0.2f, 1.1f, 0), new Vector3(0.3f, 2.2f, d - 0.2f), tile);                                  // back
                        foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0.15f, 1.1f, e * (hz - 0.2f)), new Vector3(w - 0.5f, 2.2f, 0.2f), tile);   // sides
                        mb.Box(new Vector3(0.15f, 1.1f, 0), new Vector3(w - 0.5f, 2.2f, 0.14f), tile);                                    // the wall between
                        mb.Box(new Vector3(0.15f, 0.02f, 0), new Vector3(w - 0.5f, 0.03f, d - 0.3f), Palette.Hex(0x8FA3AD));
                        // Front: a pier in the middle and at each end, a lintel, the two openings left clear.
                        foreach (float e in new[] { -1f, 0f, 1f }) mb.Box(new Vector3(-hx + 0.42f, 1.1f, e * (hz - 0.35f)), new Vector3(0.18f, 2.2f, e == 0 ? 0.9f : 0.5f), tile);
                        mb.Box(new Vector3(-hx + 0.42f, 2.2f, 0), new Vector3(0.18f, 0.5f, d - 0.2f), tile);
                        foreach (float e in new[] { -1f, 1f })
                        {
                            var cc = e < 0 ? Palette.Hex(0x1F5FA8) : Palette.Hex(0xC2417C);
                            float z = e * d * 0.24f;
                            // The pictogram plate: a head and a body (a skirt on one).
                            mb.Box(new Vector3(-hx + 0.3f, 2.22f, z), new Vector3(0.06f, 0.5f, 0.5f), cc);
                            mb.Glow = true; mb.Box(new Vector3(-hx + 0.26f, 2.36f, z), new Vector3(0.02f, 0.1f, 0.1f), Color.white); mb.Box(new Vector3(-hx + 0.26f, 2.16f, z), new Vector3(0.02f, 0.24f, e < 0 ? 0.12f : 0.22f), Color.white); mb.Glow = false;
                            // Inside: two cubicles at the back, basins and mirrors along the wall between.
                            foreach (float cz in new[] { 0.5f, 1.32f }) P("toilet", hx - 0.78f, e * cz, 0, 0.78f);
                            mb.Box(new Vector3(hx - 0.85f, 0.8f, e * 0.92f), new Vector3(1.0f, 1.6f, 0.05f), MeshBuilder.Lighten(cc, 0.55f));
                            foreach (float bx in new[] { -0.75f, 0.05f })
                            {
                                P("basin", bx, e * 0.32f, e > 0 ? 90 : 270, 0.85f);
                                P("mirror_bath", bx, e * 0.12f, e > 0 ? 90 : 270, 0.62f, 1.2f);
                            }
                        }
                        mb.Smoothness = 0.3f;
                    }
                    break;
                case "fountain":
                    // A water station: a basin unit with a tap, cups, a blue panel behind with a drop on it.
                    F("sink", 0.3f, 0, 0, 0.9f, 0.95f, 0.6f);
                    P("soda", 0.3f, -0.3f, 0, 0.2f, 0.95f); L("mug", 0.25f, 0.32f, 0, 0.12f, 0.95f);
                    mb.Mat(0.4f); mb.Box(new Vector3(0.66f, 1.2f, 0), new Vector3(0.08f, 2.0f, 0.9f), Palette.Hex(0x1E8FB0));
                    mb.Glow = true; mb.Sphere(new Vector3(0.6f, 1.8f, 0), new Vector3(0.03f, 0.18f, 0.14f), 8, Palette.Hex(0xDFF4FF)); mb.Glow = false;
                    mb.Mat(0.3f);
                    break;
                case "bins":
                    {
                        // Three sorted bins.
                        Color[] bc = { Color.white, new Color(0.6f, 0.95f, 1.9f), new Color(1.9f, 0.75f, 0.7f) };
                        for (int k = 0; k < 3; k++) T("wheelie", 0.25f, (k - 1) * 0.62f, 0, 1.0f, bc[k]);
                    }
                    break;
                case "janitor":
                    // A store with its roller door half up; the machines, a bucket and a broom outside it.
                    mb.Box(new Vector3(0.45f, 1.2f, 0), new Vector3(0.9f, 2.4f, d - 0.4f), Palette.Hex(0xCFD6DA), Palette.Hex(0xAEB8BE));
                    for (int k = 0; k < 5; k++) mb.Box(new Vector3(-0.02f, 1.3f + k * 0.2f, 0), new Vector3(0.04f, 0.17f, d - 1.2f), Palette.Hex(0x9AA5AC));
                    mb.Box(new Vector3(-0.01f, 0.6f, 0), new Vector3(0.03f, 1.2f, d - 1.2f), Palette.Hex(0x39424A));
                    P("vacuum", -0.6f, -0.5f, 0, 1.15f);
                    P("bucket", -0.7f, 0.4f, 0, 0.36f);
                    mb.Place("broom", new Vector3(-0.42f, 0, 0.72f), Quaternion.Euler(0, 0, 9), 1.45f);
                    if (level >= 2) P("wheelie", -1.45f, 0.35f, 0, 1.05f);
                    if (level >= 3) L("roomba", -1.4f, -0.5f, 0, 0.5f);
                    break;
                case "plants":
                    // A group of potted plants round a small tree, flowers from level 2.
                    P("fiddle", 0.15f, 0.1f, 0, 2.1f);
                    P("houseplant_a", -0.42f, -0.36f, 0, 0.75f); P("houseplant_c", 0.5f, -0.48f, 0, 1.1f); P("houseplant_b", -0.35f, 0.5f, 0, 0.8f);
                    if (level >= 2) { P("plant_a", 0.55f, 0.6f, 0, 0.6f); P("plant_c", -0.72f, 0.08f, 0, 0.55f); }
                    if (level >= 3) { P("flower_red", -0.1f, -0.75f, 0, 0.45f); P("flower_yellow", 0.2f, 0.78f, 40, 0.45f); P("flower_purple", -0.75f, -0.7f, 80, 0.45f); }
                    break;
                case "tv":
                    {
                        // A dark media wall of screens, a sofa in front of it, speakers at level 3.
                        mb.Box(new Vector3(0, 1.5f, hz - 0.2f), new Vector3(w - 0.3f, 3.0f, 0.2f), dark);
                        mb.Glow = true;
                        for (int k = 0; k < level + 1; k++) mb.Wide("tv", new Vector3(k % 2 == 0 ? -0.92f : 0.92f, k < 2 ? 0.5f : 1.65f, hz - 0.42f), 270, 1.7f);
                        mb.Glow = false;
                        F("design_sofa", 0, -0.45f, 90, 2.3f, 0.75f, 0.8f);
                        F("coffee_table", 0, 0.12f, 90, 1.0f, 0.36f, 0.3f);
                        if (level >= 3) foreach (float e in new[] { -1f, 1f }) P("speaker", e * (hx - 0.32f), -0.2f, 270, 1.2f);
                    }
                    break;
                case "arcade":
                    // Cabinets along the back, a table game and a dance stage in front as it grows.
                    mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.3f, 0.04f, d - 0.3f), Palette.Hex(0x1B1630));
                    P("arcade_machine", hx - 0.9f, -1.35f, 0, 1.7f);
                    P("pinball", hx - 0.95f, 1.2f, 0, 1.3f);
                    if (level >= 2) { P("claw", hx - 0.95f, -0.12f, 0, 1.75f); L("air_hockey", -0.85f, -0.95f, 90, 1.5f); }
                    else P("ticket_machine", hx - 0.6f, 0, 0, 1.35f);
                    if (level >= 3) { P("dance", -0.75f, 1.0f, 0, 1.3f); P("prize_wheel", -hx + 0.5f, -hz + 0.5f, 90, 1.4f); }
                    break;
                case "play":
                    // Soft flooring in two colours behind a low fence: a slide, a swing, big soft toys and blocks, a bench for the grown-ups.
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(w - 0.3f, 0.08f, d - 0.3f), Palette.Hex(0x4CB8A0));
                    for (int k = 0; k < 4; k++) mb.Box(new Vector3((k % 2 == 0 ? -1 : 1) * (hx - 1.3f) * 0.5f, 0.06f, (k < 2 ? -1 : 1) * (hz - 1.3f) * 0.5f), new Vector3(1.6f, 0.08f, 1.6f), k % 3 == 0 ? Palette.Hex(0xF2C230) : Palette.Hex(0x3FA7D6));
                    P("slide", 1.3f, 1.35f, 0, 1.9f, 0.09f);
                    P("swing", 1.2f, -1.45f, 90, 1.7f, 0.09f);
                    P("panda", -1.5f, 0.9f, 30, 0.75f, 0.09f); P("duck", -1.9f, -0.7f, 300, 0.6f, 0.09f); P("koala", -0.5f, 0.1f, 60, 0.6f, 0.09f);
                    L("cube", -0.9f, -1.5f, 20, 0.45f, 0.09f); L("cube", -1.45f, -1.9f, 50, 0.4f, 0.09f); L("cube", -1.05f, -1.75f, 75, 0.36f, 0.5f);
                    if (level >= 2) mb.Wide("bench_park", new Vector3(-1.2f, 0.09f, hz - 0.6f), 270, 1.6f);
                    if (level >= 3) { F("coffee_square", -hx + 0.9f, 0.2f, 0, 0.9f, 0.45f, 0.9f, 0.09f); L("cube", -hx + 0.9f, 0.2f, 15, 0.3f, 0.54f); P("duck", -hx + 1.0f, 0.5f, 250, 0.3f, 0.54f); }
                    foreach (float ez in new[] { -1f, 1f }) { for (float x = -hx + 0.25f; x <= hx - 0.2f; x += 0.5f) mb.Box(new Vector3(x, 0.32f, ez * (hz - 0.2f)), new Vector3(0.07f, 0.56f, 0.07f), Palette.White); mb.Box(new Vector3(0, 0.6f, ez * (hz - 0.2f)), new Vector3(w - 0.4f, 0.06f, 0.08f), Palette.Hex(0x3FA7D6)); }
                    for (float z = -hz + 0.25f; z <= hz - 0.2f; z += 0.5f) mb.Box(new Vector3(hx - 0.2f, 0.32f, z), new Vector3(0.07f, 0.56f, 0.07f), Palette.White); mb.Box(new Vector3(hx - 0.2f, 0.6f, 0), new Vector3(0.08f, 0.06f, d - 0.4f), Palette.Hex(0x3FA7D6));
                    break;
                case "gallery":
                    {
                        // A dark floor and a display wall with lit panels; model aircraft on plinths, a rope barrier in front.
                        mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.2f, 0.04f, d - 0.2f), Palette.Hex(0x34404E));
                        mb.Box(new Vector3(hx - 0.2f, 1.5f, 0), new Vector3(0.14f, 3.0f, d - 0.2f), Palette.Hex(0x26303C));
                        mb.Glow = true; for (int k = 0; k < 3; k++) mb.Box(new Vector3(hx - 0.29f, 1.7f, (k - 1) * (d - 1.0f) / 3f), new Vector3(0.03f, 1.2f, (d - 1.4f) / 3f), k == 1 ? Palette.Hex(0xBFE6FF) : Palette.Hex(0xF4E3B8)); mb.Glow = false;
                        string[] planes = { "airliner", "jet", "airliner_b" };
                        for (int k = 0; k < 3; k++)
                        {
                            var at = new Vector3(-hx + 1.4f + k * 1.7f, 0, (k - 1) * 0.5f);
                            mb.Box(at + Vector3.up * 0.5f, new Vector3(0.9f, 1.0f, 0.9f), Palette.White);
                            mb.Mat(0.7f, 0.6f); mb.Cylinder(at + Vector3.up * 1.0f, at + Vector3.up * 1.3f, 0.02f, 4, steel); mb.Mat(0.3f);
                            mb.Place(planes[(k + seed) % planes.Length], at + Vector3.up * 1.3f, Quaternion.Euler(0, 35 + k * 40, 0), 1.25f);
                        }
                        for (float z = -hz + 0.4f; z <= hz - 0.3f; z += (d - 0.7f) / 3f) { mb.Mat(0.8f, 0.8f); mb.Cylinder(new Vector3(-hx + 0.35f, 0, z), new Vector3(-hx + 0.35f, 0.9f, z), 0.035f, 6, Palette.Hex(0xC9A24A)); mb.Sphere(new Vector3(-hx + 0.35f, 0.93f, z), Vector3.one * 0.06f, 6, Palette.Hex(0xC9A24A)); mb.Mat(0.3f); }
                        mb.Box(new Vector3(-hx + 0.35f, 0.78f, 0), new Vector3(0.05f, 0.05f, d - 0.7f), Palette.Hex(0x8E1F2C));
                        if (level >= 2) P("fiddle", hx - 0.55f, hz - 0.5f, 0, 1.9f);
                    }
                    break;
                case "screens":
                    // A departures totem: two posts, a screen on each face with amber rows under a blue header.
                    foreach (float e in new[] { -1f, 1f }) { mb.Mat(0.7f, 0.6f); mb.Cylinder(new Vector3(0, 0, e * 0.6f), new Vector3(0, 2.3f, e * 0.6f), 0.05f, 6, steel); mb.Mat(0.3f); }
                    mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.06f, 1.5f), dark);
                    mb.Wide("tv", new Vector3(-0.07f, 2.1f, 0), 0, 1.6f); mb.Wide("tv", new Vector3(0.07f, 2.1f, 0), 180, 1.6f);
                    mb.Glow = true;
                    foreach (float e in new[] { -1f, 1f })
                    {
                        mb.Box(new Vector3(e * 0.235f, 3.02f, 0), new Vector3(0.01f, 0.13f, 1.4f), Palette.Hex(0x2F7FD6));
                        for (int k = 0; k < 5; k++)
                        {
                            float y = 2.86f - k * 0.125f;
                            mb.Box(new Vector3(e * 0.235f, y, -0.42f), new Vector3(0.01f, 0.07f, 0.5f), Palette.Hex(0xF2C230));
                            mb.Box(new Vector3(e * 0.235f, y, 0.2f), new Vector3(0.01f, 0.07f, 0.3f + (k * 37 % 5) * 0.04f), Palette.Hex(0xF2C230));
                            mb.Box(new Vector3(e * 0.235f, y, 0.6f), new Vector3(0.01f, 0.07f, 0.16f), k % 3 == 1 ? Palette.Hex(0xE8372E) : Palette.Hex(0x6BE38A));
                        }
                    }
                    mb.Glow = false;
                    break;
                case "infodesk":
                    // A desk with a screen and leaflets, its chair behind, a plant, and a blue "i" beacon on a mast.
                    F("desk_white", 0.1f, 0, 0, 1.7f, 1.02f, 0.9f);
                    L("screen", 0.2f, 0.42f, 180, 0.5f, 1.02f); L("books", 0, -0.5f, 20, 0.28f, 1.02f);
                    P("chair_desk", 0.95f, 0, 0, 1.0f);
                    P("houseplant_c", 0.5f, -hz + 0.35f - 0.0f + 0.0f, 0, 1.15f);
                    mb.Mat(0.7f, 0.6f); mb.Cylinder(new Vector3(0.1f, 0, 1.2f), new Vector3(0.1f, 3f, 1.2f), 0.05f, 6, steel); mb.Mat(0.3f);
                    mb.Cylinder(new Vector3(0, 3.3f, 1.2f), new Vector3(0.2f, 3.3f, 1.2f), 0.42f, 0.42f, 16, Palette.Hex(0x1F5FA8));
                    mb.Glow = true;
                    foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(0.1f + e * 0.11f, 3.23f, 1.2f), new Vector3(0.01f, 0.3f, 0.09f), Color.white); mb.Box(new Vector3(0.1f + e * 0.11f, 3.48f, 1.2f), new Vector3(0.01f, 0.09f, 0.09f), Color.white); }
                    mb.Glow = false;
                    break;
                case "sculpture":
                    // A polished jet climbing off a stone plinth.
                    mb.Box(new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.5f, 1.1f), Palette.Hex(0xC9C4B8));
                    mb.Box(new Vector3(0, 0.53f, 0), new Vector3(0.9f, 0.06f, 0.9f), Palette.Hex(0x8E8A82));
                    mb.Mat(0.85f, 0.8f);
                    mb.Cylinder(new Vector3(0, 0.56f, 0), new Vector3(0.1f, 1.75f, 0), 0.05f, 0.03f, 8, steel);
                    mb.Place("jet", new Vector3(0.1f, 1.55f, 0), Quaternion.Euler(0, 225, 0) * Quaternion.Euler(-30, 0, 0) * Quaternion.Euler(0, 90, 0), 1.9f, new Color(0.82f, 0.86f, 0.92f));
                    mb.Mat(0.3f);
                    break;
                case "clock":
                    // A cast-iron post on a round step, four faces under a little roof.
                    mb.Cylinder(Vector3.zero, Vector3.up * 0.22f, 0.5f, 0.45f, 14, Palette.Hex(0x8E8A82));
                    mb.Mat(0.6f, 0.4f);
                    mb.Cylinder(Vector3.up * 0.22f, Vector3.up * 2.7f, 0.1f, 0.08f, 8, Palette.Hex(0x1F3A2E));
                    mb.Cylinder(Vector3.up * 0.22f, Vector3.up * 0.6f, 0.17f, 0.11f, 8, Palette.Hex(0x1F3A2E));
                    mb.Box(new Vector3(0, 3.1f, 0), new Vector3(0.84f, 0.84f, 0.84f), Palette.Hex(0x1F3A2E));
                    mb.Mat(0.3f);
                    for (int k = 0; k < 4; k++)
                    {
                        mb.Push().RotateY(k * 90);
                        mb.Glow = true; mb.Cylinder(new Vector3(-0.42f, 3.1f, 0), new Vector3(-0.45f, 3.1f, 0), 0.31f, 0.31f, 20, Palette.Hex(0xFFF8E6)); mb.Glow = false;      // the lit dial behind the hands
                        mb.Tall("clock", new Vector3(-0.5f, 2.74f, 0), 180, 0.72f);
                        mb.Pop();
                    }
                    mb.Cylinder(Vector3.up * 3.52f, Vector3.up * 3.8f, 0.62f, 0.05f, 4, Palette.Hex(0x16291F));
                    mb.Sphere(Vector3.up * 3.86f, Vector3.one * 0.08f, 6, Palette.Hex(0xD9A92A));
                    break;
                case "flags":
                    // A row of flags on a low kerb.
                    mb.Box(new Vector3(0, 0.08f, 0), new Vector3(0.7f, 0.16f, d - 0.3f), Palette.Hex(0xC9C4B8));
                    for (int k = 0; k < 4; k++) mb.Painted("flag", new Vector3(0, 0.16f, -hz + 0.5f + k * (d - 1.0f) / 3f), FlagYaw, 3.0f, Brand[(k + seed) % Brand.Length]);
                    break;
                case "piano":
                    // A black grand on a round rug, with its stool.
                    L("rug_round", 0, 0, 0, 1.95f, 0.01f);
                    mb.Mat(0.85f); L("piano", 0.15f, 0, 0, 1.6f, 0.02f); mb.Mat(0.3f);
                    T("stool_square", -0.82f, 0, 0, 0.5f, new Color(0.35f, 0.3f, 0.3f), 0.02f);
                    break;
                case "palms":
                    {
                        // A raised bed with a timber rim to sit on: three palms over ferns and flowers.
                        mb.Box(new Vector3(0, 0.22f, 0), new Vector3(w - 0.4f, 0.44f, d - 0.4f), Palette.Hex(0xD6D9DC));
                        mb.Box(new Vector3(0, 0.47f, 0), new Vector3(w - 0.3f, 0.06f, d - 0.3f), wood);
                        mb.Box(new Vector3(0, 0.5f, 0), new Vector3(w - 1.0f, 0.04f, d - 1.0f), Palette.Hex(0x4A3A2A));
                        P("palm_tall", -0.5f, -0.6f, 0, 3.4f, 0.5f); P("palm_bend", 0.6f, 0.1f, 40, 2.9f, 0.5f); P("palm_short", -0.3f, 0.8f, 200, 2.5f, 0.5f);
                        var prng = new System.Random(seed + 11);
                        string[] under = { "fern", "leaves", "bush_small", "fern", "leaves" };
                        string[] blooms = { "flower_red", "flower_yellow", "flower_purple" };
                        for (int k = 0; k < 12; k++)
                        {
                            float x = ((float)prng.NextDouble() - 0.5f) * (w - 1.5f), z = ((float)prng.NextDouble() - 0.5f) * (d - 1.5f), yaw = k * 67;
                            if (level >= 2 && k % 4 == 3) P(blooms[k % 3], x, z, yaw, 0.42f, 0.52f);
                            else { string u = under[k % under.Length]; if (u == "fern") P(u, x, z, yaw, 0.6f, 0.52f); else L(u, x, z, yaw, 0.7f, 0.52f); }
                        }
                    }
                    break;
                case "pool":
                    // A fountain, taller at level 3, with plants at its corners from level 2.
                    L(level >= 3 ? "fountain_tiers" : "fountain", 0, 0, 0, w - 0.5f);
                    if (level >= 2) foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) P("houseplant_b", ex * (hx - 0.3f), ez * (hz - 0.3f), 0, 0.8f);
                    break;
                case "aquarium":
                    {
                        // A long tank on a dark cabinet, open to the hall on both faces: a reef and a shoal against lit blue water.
                        mb.Box(new Vector3(0, 0.4f, 0), new Vector3(w - 0.5f, 0.8f, d - 0.3f), dark);
                        mb.Box(new Vector3(0, 2.52f, 0), new Vector3(w - 0.5f, 0.16f, d - 0.3f), dark);
                        foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(new Vector3(ex * (hx - 0.29f), 1.62f, ez * (hz - 0.19f)), new Vector3(0.08f, 1.64f, 0.08f), dark);
                        mb.Box(new Vector3(0, 0.83f, 0), new Vector3(w - 0.56f, 0.06f, d - 0.36f), Palette.Hex(0xE6D9B0));
                        mb.Glow = true;
                        mb.Box(new Vector3(0, 1.64f, 0), new Vector3(0.1f, 1.6f, d - 0.4f), Palette.Hex(0x2C9BD0));
                        mb.Box(new Vector3(0, 2.42f, 0), new Vector3(w - 0.62f, 0.05f, d - 0.42f), Palette.Hex(0xDFF6FF));
                        mb.Glow = false;
                        string[] reef = { "coral_b", "anemone", "coral_a", "anemone", "coral_b" };
                        string[] shoal = { "goldfish", "fish", "goldfish", "seahorse", "fish", "goldfish" };
                        foreach (float e in new[] { -1f, 1f })
                        {
                            float x = e * 0.4f;
                            for (int k = 0; k < 5; k++) P(reef[(k + (e > 0 ? 2 : 0)) % reef.Length], x, -hz + 0.6f + k * (d - 1.2f) / 4f, k * 70, 0.42f + (k % 3) * 0.12f, 0.86f);
                            for (int k = 0; k < 6; k++)
                            {
                                string f = shoal[(k + (e > 0 ? 3 : 0)) % shoal.Length];
                                float z = -hz + 0.5f + (k * 53 % 17) / 17f * (d - 1.0f), y = 1.45f + (k * 29 % 11) / 11f * 0.75f;
                                if (f == "seahorse") P(f, x, z, FishYaw, 0.4f, y - 0.15f); else L(f, x, z, FishYaw + (k % 2) * 180, 0.4f, y);
                            }
                        }
                    }
                    break;
                default:
                    mb.Box(new Vector3(0, 0.6f, 0), new Vector3(w - 0.3f, 1.2f, d - 0.3f), brand);
                    break;
            }
            mb.Mat(0.25f);
        }

        // How a few models sit in their files: the yaw that points a fish along its tank and a flag's fly across its row.
        const float FishYaw = 90, FlagYaw = 90;
    }
}
