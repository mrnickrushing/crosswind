using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>One of the people you can have on your team: who they are and what they're good at.</summary>
    public sealed class CrewDef
    {
        public string Id, Name, Role, Line;
        public Rarity Rarity;
        public Effect Effect;
        public float PerLevel;          // what each level adds while on duty
        public bool Hours;              // the effect is in hours (shown as "+1 h"), not a share
    }

    /// <summary>What came out of a crate.</summary>
    public sealed class CrateResult
    {
        public CrewDef Who;
        public bool New, LevelUp;
        public int Level, GemsBack;     // gems back when the card is already at the top level
    }

    /// <summary>
    /// The team: sixteen people in four rarities, collected from crates. A crate comes free every eight hours, with
    /// every star, or for gems; the odds are fixed and shown, and an Epic or better is certain by every tenth crate.
    /// More copies of someone level them up (to 5). Only those on duty count: three places, one more for each airport
    /// sold on (up to six). The team is yours for good and moves with you.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly List<CrewDef> Crew = new List<CrewDef>
        {
            C("dot", "Dot Pemberton", "Ramp Marshal", "Nobody taxis the long way round on her apron.", Rarity.Common, Effect.TaxiSpeed, 0.04f),
            C("gus", "Gus Okafor", "Fuel Boss", "Knows the price of a litre in nine currencies.", Rarity.Common, Effect.FuelMargin, 0.06f),
            C("marnie", "Marnie Vale", "Gate Agent", "Boards a full flight before the coffee's cold.", Rarity.Common, Effect.BoardSpeed, 0.04f),
            C("tobias", "Tobias Reed", "Site Foreman", "Gets a quote, then gets a better one.", Rarity.Common, Effect.BuildDiscount, 0.02f),
            C("ines", "Ines Calder", "Shop Floor Lead", "Puts the good chocolate where you can't miss it.", Rarity.Common, Effect.Retail, 0.06f),
            C("hal", "Hal Brandt", "Night Porter", "Keeps the place earning while you sleep.", Rarity.Common, Effect.OfflineEfficiency, 0.03f),
            C("priya", "Priya Nand", "Approach Controller", "Threads them in a mile closer than anyone dares.", Rarity.Rare, Effect.RunwaySpeed, 0.04f),
            C("luc", "Luc Fontaine", "Crew Chief", "His trucks are rolling before the engines stop.", Rarity.Rare, Effect.CrewSpeed, 0.04f),
            C("olwen", "Olwen Hart", "Customer Champion", "Remembers your name and your seat.", Rarity.Rare, Effect.PassengerFee, 0.08f),
            C("sam", "Sam Abara", "Met Officer", "Saw this storm coming on Tuesday.", Rarity.Rare, Effect.WeatherShield, 0.06f),
            C("kit", "Kit Larsen", "Route Scout", "Always on the phone to an airline.", Rarity.Rare, Effect.Demand, 0.03f),
            C("quill", "Captain Rosalind Quill", "Chief Pilot", "If she says it leaves on time, it leaves on time.", Rarity.Epic, Effect.OnTimeSlack, 0.05f),
            C("dmitri", "Dmitri Vance", "Commercial Director", "Every landing is a negotiation he has already won.", Rarity.Epic, Effect.LandingFee, 0.08f),
            C("aya", "Aya Morimoto", "Station Master", "Inspectors leave her airport smiling.", Rarity.Epic, Effect.RatingGain, 0.06f),
            C("flint", "Sir Barnaby Flint", "Chairman", "Built three airports. Yours is his favourite.", Rarity.Legendary, Effect.Income, 0.06f),
            new CrewDef { Id = "zara", Name = "Zara Okonkwo", Role = "The Fixer", Line = "Whatever it is, it was handled an hour ago.", Rarity = Rarity.Legendary, Effect = Effect.OfflineHours, PerLevel = 1f, Hours = true },
        };

        static CrewDef C(string id, string name, string role, string line, Rarity rarity, Effect effect, float per)
        {
            return new CrewDef { Id = id, Name = name, Role = role, Line = line, Rarity = rarity, Effect = effect, PerLevel = per };
        }

        /// <summary>The chance of each rarity from a crate, in percent (shown in the game as they are here).</summary>
        public static readonly int[] CrateOdds = { 70, 22, 7, 1 };
        public const int CrateGems = 40, CratePity = 10, FreeCrateHours = 8, MaxCrewLevel = 5, GemsPerSpare = 5;

        public static CrewDef CrewById(string id) { return Crew.Find(c => c.Id == id); }

        public int Copies(string id) { var e = State.crew.Find(l => l.id == id); return e != null ? e.level : 0; }
        /// <summary>1 copy is level 1, 3 is level 2, then 7, 15 and 31.</summary>
        public static int LevelFor(int copies) { int l = 0; while (l < MaxCrewLevel && copies >= (1 << (l + 1)) - 1) l++; return l; }
        public int CrewLevel(string id) { return LevelFor(Copies(id)); }
        /// <summary>Copies still needed for the next level (0 at the top).</summary>
        public int CopiesToNext(string id) { int l = CrewLevel(id); return l >= MaxCrewLevel ? 0 : (1 << (l + 1)) - 1 - Copies(id); }

        public int DutyPlaces { get { return Math.Min(6, 3 + State.mapsRun.Count); } }
        public bool OnDuty(string id) { int i = State.duty.IndexOf(id); return i >= 0 && i < DutyPlaces; }

        public string DutyBlocked(string id)
        {
            if (CrewLevel(id) <= 0) return "Not on your team yet";
            if (!OnDuty(id) && State.duty.Count(OnDuty) >= DutyPlaces) return "Every place is taken: rest someone first";
            return null;
        }

        /// <summary>Puts someone on duty, or rests them.</summary>
        public bool ToggleDuty(string id)
        {
            if (State.duty.Contains(id)) { State.duty.Remove(id); Changed?.Invoke(); return true; }
            if (DutyBlocked(id) != null) return false;
            State.duty.Add(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>What the people on duty add to one effect.</summary>
        float CrewFx(Effect e)
        {
            float v = 0;
            for (int i = 0; i < State.duty.Count && i < DutyPlaces; i++)
            {
                var d = CrewById(State.duty[i]);
                if (d != null && d.Effect == e) v += d.PerLevel * CrewLevel(d.Id);
            }
            return v;
        }

        public static string CrewPerk(CrewDef d, int level)
        {
            float v = d.PerLevel * Math.Max(1, level);
            string amount = d.Hours ? "+" + v.ToString("0") + " h" : "+" + Math.Round(v * 100) + "%";
            switch (d.Effect)
            {
                case Effect.TaxiSpeed: return amount + " taxi speed";
                case Effect.FuelMargin: return amount + " fuel margin";
                case Effect.BoardSpeed: return amount + " boarding speed";
                case Effect.BuildDiscount: return amount.Replace("+", "-") + " build costs";
                case Effect.Retail: return amount + " shop income";
                case Effect.OfflineEfficiency: return amount + " earned while away";
                case Effect.RunwaySpeed: return amount + " runway speed";
                case Effect.CrewSpeed: return amount + " service crew speed";
                case Effect.PassengerFee: return amount + " passenger fees";
                case Effect.WeatherShield: return amount + " less weather delay";
                case Effect.Demand: return amount + " flights on every route";
                case Effect.OnTimeSlack: return amount + " time before a flight is late";
                case Effect.LandingFee: return amount + " landing fees";
                case Effect.RatingGain: return amount + " rating gained";
                case Effect.Income: return amount + " all income";
                case Effect.OfflineHours: return amount + " earning while away";
                default: return amount;
            }
        }

        // ------------------------------------------------------------------ crates

        public bool FreeCrateReady { get { return RealNow() >= State.freeCrateAt; } }
        public double FreeCrateIn { get { return Math.Max(0, State.freeCrateAt - RealNow()); } }

        public bool TakeFreeCrate()
        {
            if (!FreeCrateReady) return false;
            State.freeCrateAt = RealNow() + FreeCrateHours * 3600;
            State.crates++;
            Changed?.Invoke();
            return true;
        }

        public bool BuyCrate()
        {
            if (State.gems < CrateGems) return false;
            State.gems -= CrateGems;
            State.crates++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Opens a crate you hold: one person, by the published odds.</summary>
        public CrateResult OpenCrate()
        {
            if (State.crates <= 0) return null;
            State.crates--;
            State.cratesOpened++;
            State.sinceEpic++;
            double roll = Rand() * 100;
            Rarity rarity = roll < CrateOdds[3] ? Rarity.Legendary : roll < CrateOdds[3] + CrateOdds[2] ? Rarity.Epic : roll < CrateOdds[3] + CrateOdds[2] + CrateOdds[1] ? Rarity.Rare : Rarity.Common;
            if (State.sinceEpic >= CratePity && rarity < Rarity.Epic) rarity = Rarity.Epic;
            if (rarity >= Rarity.Epic) State.sinceEpic = 0;
            var pool = Crew.Where(c => c.Rarity == rarity).ToList();
            var who = pool[Math.Min(pool.Count - 1, (int)(Rand() * pool.Count))];
            var res = new CrateResult { Who = who };
            int before = CrewLevel(who.Id);
            if (before >= MaxCrewLevel) { res.GemsBack = GemsPerSpare * (1 + (int)rarity); State.gems += res.GemsBack; }
            else
            {
                var e = State.crew.Find(l => l.id == who.Id);
                if (e == null) State.crew.Add(new LevelEntry { id = who.Id, level = 1 }); else e.level++;
            }
            res.Level = CrewLevel(who.Id);
            res.New = before == 0;
            res.LevelUp = before > 0 && res.Level > before;
            if (res.New && State.duty.Count(OnDuty) < DutyPlaces && !State.duty.Contains(who.Id)) State.duty.Add(who.Id);    // a free place is filled at once
            Changed?.Invoke();
            return res;
        }
    }
}
