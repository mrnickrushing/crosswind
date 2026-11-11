using System;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// Airlines as partners: each has a satisfaction with you that on-time flights raise and late ones drop fast.
    /// Happy airlines pay more and bring their better routes; a neglected one cancels a route and walks.
    /// Each contract is signed on terms you choose: standard, busy (more flights, tighter turns) or premium
    /// (fewer flights that pay more and wait a little longer).
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>An airline cancelled a route (the HUD says so).</summary>
        public event Action<RouteDef> RouteLost;

        public static readonly string[] TermsName = { "STANDARD", "BUSY", "PREMIUM" };
        public static readonly string[] TermsBlurb = { "The airline's usual schedule.", "40% more flights, but they expect a tighter turn.", "30% fewer flights, each paying 60% more." };
        public static float TermsFlights(int t) { return t == 1 ? 1.4f : t == 2 ? 0.7f : 1f; }
        public static float TermsPay(int t) { return t == 2 ? 1.6f : 1f; }
        public static float TermsPatience(int t) { return t == 1 ? 0.8f : t == 2 ? 1.15f : 1f; }

        public float Relation(string airline)
        {
            var e = State.relations.Find(l => l.id == airline);
            return e != null ? e.level / 10f : 50f;
        }

        void Relate(string airline, float delta)
        {
            float v = Math.Max(0, Math.Min(100, Relation(airline) + delta));
            var e = State.relations.Find(l => l.id == airline);
            if (e == null) State.relations.Add(new LevelEntry { id = airline, level = (int)Math.Round(v * 10) }); else e.level = (int)Math.Round(v * 10);
            if (v <= 0.01f) Walk(airline);
        }

        /// <summary>What an airline's mood does to its fees: 0.8 at rock bottom, 1.0 at 50, 1.3 when delighted.</summary>
        public double RelationPay(string airline)
        {
            float v = Relation(airline);
            return v >= 50 ? 1 + (v - 50) / 50 * 0.3 : 0.8 + v / 50 * 0.2;
        }

        public static string Mood(float v) { return v >= 85 ? "Delighted" : v >= 65 ? "Happy" : v >= 40 ? "Content" : v >= 20 ? "Unhappy" : "Furious"; }

        /// <summary>An airline out of patience cancels its newest route here and starts again at "unhappy".</summary>
        void Walk(string airline)
        {
            var c = State.contracts.LastOrDefault(x => Content.Routes[x.route].Airline == airline);
            if (c == null || State.contracts.Count <= 1) { SetRelation(airline, 15); return; }
            State.contracts.Remove(c);
            SetRelation(airline, 25);
            var r = Content.Routes[c.route];
            Toast?.Invoke(Content.Airlines[airline].Name + " cancelled " + r.Destination + ": too many late flights");
            RouteLost?.Invoke(r);
            Changed?.Invoke();
        }

        void SetRelation(string airline, float v)
        {
            var e = State.relations.Find(l => l.id == airline);
            if (e == null) State.relations.Add(new LevelEntry { id = airline, level = (int)(v * 10) }); else e.level = (int)(v * 10);
        }

        // ------------------------------------------------------------------ punctuality deals and partner levels

        /// <summary>An airline's deal finished: the airline, whether it was met, and the bonus paid.</summary>
        public event Action<string, bool, double> DealDone;

        public const int MaxPartner = 3;
        public static readonly string[] PartnerName = { "Customer", "Preferred airport", "Hub partner", "Alliance hub" };
        /// <summary>Each partner level adds this much to everything that airline pays here.</summary>
        public const double PartnerBonus = 0.06;

        public int Partner(string airline) { var e = State.partner.Find(l => l.id == airline); return e != null ? Math.Max(0, Math.Min(MaxPartner, e.level)) : 0; }
        public double PartnerPay(string airline) { return 1 + PartnerBonus * Partner(airline); }

        /// <summary>How many flights the airline's current deal runs over: longer at each level.</summary>
        public int DealFlights(string airline) { return 20 + 10 * Partner(airline); }
        /// <summary>How many of them must leave on time: 85% at first, 97% to keep the top level's bonus coming.</summary>
        public int DealNeeds(string airline)
        {
            int p = Partner(airline);
            double share = p == 0 ? 0.85 : p == 1 ? 0.9 : p == 2 ? 0.95 : 0.97;
            return (int)Math.Ceiling(DealFlights(airline) * share);
        }

        public AirlineDeal Deal(string airline)
        {
            var d = State.deals.Find(x => x.airline == airline);
            if (d == null) State.deals.Add(d = new AirlineDeal { airline = airline });
            return d;
        }

        /// <summary>The deal can still be met with the flights that are left.</summary>
        public bool DealAlive(string airline) { var d = Deal(airline); return d.onTime + (DealFlights(airline) - d.flights) >= DealNeeds(airline); }

        /// <summary>
        /// Counts a departure toward its airline's deal. Met: a bonus worth a quarter of those flights, a step up as
        /// a partner (to the top), and a happier airline. Missed: no bonus, and they hold it against you.
        /// </summary>
        void CountDeal(RouteDef r, bool onTime, double flightPaid)
        {
            var d = Deal(r.Airline);
            d.flights++; if (onTime) d.onTime++;
            int size = DealFlights(r.Airline);
            if (d.flights < size) return;
            bool met = d.onTime >= DealNeeds(r.Airline);
            double bonus = 0;
            string name = Content.Airlines[r.Airline].Name;
            if (met)
            {
                bonus = Math.Floor(flightPaid * size * 0.25);
                Earn(bonus, false);
                int before = Partner(r.Airline);
                if (before < MaxPartner)
                {
                    var e = State.partner.Find(l => l.id == r.Airline);
                    if (e == null) State.partner.Add(new LevelEntry { id = r.Airline, level = before + 1 }); else e.level = before + 1;
                    Toast?.Invoke(name + " names you a " + PartnerName[before + 1].ToLower() + ": +" + Format.Money(bonus) + ", and they pay " + (int)Math.Round(PartnerBonus * 100 * (before + 1)) + "% more from now on");
                }
                else Toast?.Invoke(name + " punctuality bonus: +" + Format.Money(bonus));
                Relate(r.Airline, 6);
            }
            else
            {
                Toast?.Invoke(name + ": only " + d.onTime + " of " + size + " flights left on time. No bonus this time");
                Relate(r.Airline, -5);
            }
            d.flights = 0; d.onTime = 0;
            DealDone?.Invoke(r.Airline, met, bonus);
        }

        /// <summary>Changes a signed route's terms (free, any time: the next flight follows the new schedule).</summary>
        public bool SetTerms(string route, int terms)
        {
            var c = State.contracts.Find(x => x.route == route);
            if (c == null || terms < 0 || terms > 2) return false;
            c.terms = terms;
            Changed?.Invoke();
            return true;
        }
    }
}
