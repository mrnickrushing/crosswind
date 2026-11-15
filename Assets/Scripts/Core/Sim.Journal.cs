using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>One first in the journal: what it was, the airport and the airport day it happened (0: before the journal was kept).</summary>
    [Serializable]
    public sealed class JournalEntry
    {
        public string kind;             // Sim.PageAirlines, PageClasses, PageContracts or JournalMilestone
        public string id;               // an airline id, a class letter ("a".."e"), a contract kind (Sim.KindIds) or a milestone (Sim.MilestoneIds)
        public string map;
        public int day;
    }

    /// <summary>An airport sold on: what it came to, kept in the hall of fame for good.</summary>
    [Serializable]
    public sealed class LegacyRecord
    {
        public string map, name;
        public int stars, flights, daysAtA, items;      // items: terminal items standing when it was sold
        public double earned;
        public bool before;             // sold before the hall of fame was kept: only the airport is known
        public string bestAirline = ""; // the contract that earned the most reputation ("": none flown)
        public int bestKind, bestPlane, bestRep, bestNumber;
        public string photo = "";       // the last photo taken there (a file on the device that took it)
        public double soldAt;           // real time
    }

    /// <summary>
    /// The journal and the hall of fame. The journal keeps firsts as they happen: each airline's first flight, each
    /// aircraft class, each kind of contract flown, and a few milestones. It is kept for good, across airports. A
    /// full page (every airline, every class, every contract kind) pays a few gems once. Selling an airport writes
    /// what it came to into the hall of fame and adds a trophy; trophies are a record, nothing more.
    /// </summary>
    public sealed partial class Sim
    {
        public const string PageAirlines = "airline", PageClasses = "class", PageContracts = "contract", JournalMilestone = "milestone";
        public static readonly string[] JournalPages = { PageAirlines, PageClasses, PageContracts };
        public static readonly string[] KindIds = { "charter", "series", "freight", "vip" };
        public static readonly string[] MilestoneIds = { "flights_100", "flights_1000", "flights_10000", "widebody", "grade_a" };
        static readonly string[] MilestoneNames = { "100 flights at one airport", "1,000 flights at one airport", "10,000 flights at one airport", "First wide-body", "First day at grade A" };
        static readonly int[] MilestoneFlights = { 100, 1000, 10000 };

        public static string ClassId(AircraftClass c) { return ((char)('a' + (int)c)).ToString(); }

        /// <summary>Everything a page is made of, in order.</summary>
        public static List<string> PageItems(string page)
        {
            switch (page)
            {
                case PageAirlines: return Content.Airlines.Keys.Where(a => a != "own").ToList();      // visitors: your own airline is not collected
                case PageClasses: return Content.Aircraft.Keys.OrderBy(c => c).Select(ClassId).ToList();
                case PageContracts: return KindIds.ToList();
                case JournalMilestone: return MilestoneIds.ToList();
            }
            return new List<string>();
        }

        public static int PageGems(string page) { return page == PageAirlines ? 25 : page == PageClasses ? 15 : page == PageContracts ? 10 : 0; }

        /// <summary>What an entry is called on the page.</summary>
        public static string JournalName(string kind, string id)
        {
            switch (kind)
            {
                case PageAirlines: AirlineDef a; return Content.Airlines.TryGetValue(id, out a) ? a.Name : id;
                case PageClasses:
                    int c = string.IsNullOrEmpty(id) ? -1 : id[0] - 'a';
                    AircraftDef d; return Content.Aircraft.TryGetValue((AircraftClass)c, out d) ? d.Name + " (" + id.ToUpper() + ")" : id;
                case PageContracts: int k = Array.IndexOf(KindIds, id); return k < 0 ? id : KindName(k).Substring(0, 1) + KindName(k).Substring(1).ToLower();
                case JournalMilestone: int m = Array.IndexOf(MilestoneIds, id); return m < 0 ? id : MilestoneNames[m];
            }
            return id;
        }

        public JournalEntry Noted(string kind, string id) { return State.journal.Find(e => e.kind == kind && e.id == id); }
        public int PageSeen(string page) { return PageItems(page).Count(i => Noted(page, i) != null); }
        public bool PageDone(string page) { var items = PageItems(page); return items.Count > 0 && items.All(i => Noted(page, i) != null); }
        public bool PagePaid(string page) { return State.journalPaid.Contains(page); }
        /// <summary>Pages finished and not yet collected (for a dot on EMPIRE).</summary>
        public int PagesWaiting { get { return JournalPages.Count(p => PageDone(p) && !PagePaid(p)); } }

        public string ClaimPageBlocked(string page)
        {
            if (PageGems(page) <= 0) return "Nothing to collect";
            if (PagePaid(page)) return "Already collected";
            if (!PageDone(page)) return "Not complete yet";
            return null;
        }

        /// <summary>Collects a full page's gems, once. Returns the gems paid.</summary>
        public int ClaimPage(string page)
        {
            if (ClaimPageBlocked(page) != null) return 0;
            int g = PageGems(page);
            State.journalPaid.Add(page);
            State.gems += g;
            Changed?.Invoke();
            return g;
        }

        /// <summary>Writes a first into the journal (nothing when it is there already). True when it is new.</summary>
        bool Note(string kind, string id, int day)
        {
            if (Noted(kind, id) != null) return false;
            State.journal.Add(new JournalEntry { kind = kind, id = id, map = State.map, day = day });
            if (Array.IndexOf(JournalPages, kind) >= 0 && PageDone(kind) && !PagePaid(kind))
                Toast?.Invoke("Journal page complete: " + PageGems(kind) + " gems to collect in EMPIRE");
            return true;
        }

        /// <summary>A flight is paying (from Pay, before its contract is closed): whatever it is a first of.</summary>
        void JournalFlight(FlightState f, RouteDef r)
        {
            int day = FuelDay + 1, flights = State.flightsHandled + 1;      // this one counted
            if (r.Airline != "own") Note(PageAirlines, r.Airline, day);
            Note(PageClasses, ClassId(r.Class), day);
            if (f.contract != 0) { var b = Booking(f.contract); if (b != null) Note(PageContracts, KindIds[Math.Max(0, Math.Min(KindIds.Length - 1, b.kind))], day); }
            for (int i = 0; i < MilestoneFlights.Length; i++) if (flights >= MilestoneFlights[i]) Note(JournalMilestone, MilestoneIds[i], day);
            if (Heavy(r.Class)) Note(JournalMilestone, "widebody", day);
        }

        /// <summary>A day closed at grade A (from TurnDay).</summary>
        void JournalGradeA(int day) { Note(JournalMilestone, "grade_a", day); }

        /// <summary>
        /// A save from before the journal: writes in what it certainly saw, on day 0 ("before the journal"). The
        /// airlines and classes of the routes it has flown (when it has flown any), the contracts in its track record,
        /// the milestones its counters show, and a hall-of-fame line for each airport it sold, with only the name known.
        /// </summary>
        public static void BackfillJournal(GameState s)
        {
            if (s.journalSet) return;
            s.journalSet = true;
            var seen = new List<JournalEntry>();
            void Add(string kind, string id) { if (!s.journal.Exists(e => e.kind == kind && e.id == id) && !seen.Exists(e => e.kind == kind && e.id == id)) seen.Add(new JournalEntry { kind = kind, id = id, map = s.map, day = 0 }); }
            if (s.flightsHandled > 0)
                foreach (var c in s.contracts)
                {
                    RouteDef r;
                    if (!Content.Routes.TryGetValue(c.route, out r)) continue;
                    if (r.Airline != "own") Add(PageAirlines, r.Airline);
                    Add(PageClasses, ClassId(r.Class));
                    if (Heavy(r.Class)) Add(JournalMilestone, "widebody");
                }
            foreach (var rec in s.record)
            {
                if (rec.airline != "own") Add(PageAirlines, rec.airline);
                if (rec.plane >= 0 && rec.plane <= 4) { Add(PageClasses, ClassId((AircraftClass)rec.plane)); if (Heavy((AircraftClass)rec.plane)) Add(JournalMilestone, "widebody"); }
                if (!rec.dropped && rec.kind >= 0 && rec.kind < KindIds.Length) Add(PageContracts, KindIds[rec.kind]);
            }
            foreach (var r in s.rep) if (r.level > 0 && r.id != "own") Add(PageAirlines, r.id);       // reputation is only earned by flying
            for (int i = 0; i < MilestoneFlights.Length; i++) if (s.flightsHandled >= MilestoneFlights[i]) Add(JournalMilestone, MilestoneIds[i]);
            if (s.daysAtA > 0) Add(JournalMilestone, "grade_a");
            s.journal.AddRange(seen);
            if (s.legacy.Count == 0)
                foreach (var id in s.mapsRun)
                {
                    var m = Content.Maps.Find(x => x.Id == id);
                    s.legacy.Add(new LegacyRecord { map = id, name = m != null ? m.Name : id, before = true });
                }
            s.trophies = Math.Max(s.trophies, s.legacy.Count);
        }

        // ------------------------------------------------------------------ the hall of fame

        /// <summary>Writes the airport being sold into the hall of fame (from SellAndMove, before anything is reset).</summary>
        void KeepLegacy(string photo)
        {
            var best = State.record.Where(r => !r.dropped).OrderByDescending(r => r.rep).FirstOrDefault();
            // Days at grade A are counted for good; this airport's share is what the airports before it didn't have.
            int earlier = State.legacy.Sum(l => l.daysAtA);
            State.legacy.Add(new LegacyRecord
            {
                map = State.map, name = Map.Name, stars = Stars, flights = State.flightsHandled, earned = State.lifetimeEarned,
                daysAtA = Math.Max(0, State.daysAtA - earlier), items = State.terminal.Count,
                bestAirline = best != null ? best.airline : "", bestKind = best != null ? best.kind : 0, bestPlane = best != null ? best.plane : 0,
                bestRep = best != null ? best.rep : 0, bestNumber = best != null ? best.number : 0,
                photo = photo ?? "", soldAt = RealNow(),
            });
            State.trophies++;
        }
    }
}
