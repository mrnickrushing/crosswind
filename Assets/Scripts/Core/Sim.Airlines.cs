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
