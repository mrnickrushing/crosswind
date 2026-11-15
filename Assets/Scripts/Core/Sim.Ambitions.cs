using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>One thing an ambition needs, whether it is there yet, and the facility that provides it (null: not a build).</summary>
    public sealed class AmbitionStep
    {
        public string Text; public bool Ready; public string Facility;
    }

    /// <summary>A big target at this airport: what it is called, its pitch, and its reward.</summary>
    public sealed class AmbitionDef
    {
        public string Id, Name, Pitch, Icon;
        public int Gems;
        public AircraftClass? Class;    // the first aircraft of this class landing here (null: another kind of ambition)
    }

    /// <summary>
    /// Ambitions: the big moments of an airport, each with what it still needs in plain sight and a reward: the first
    /// jet, wide-body and jumbo to land here, a full apron, the whole terminal floor open. One can be pinned to the HUD
    /// (<see cref="GameState.pinnedAmbition"/>). They are per airport (claims are kept as "map:id"), and they complete
    /// the moment they happen (<see cref="AmbitionReached"/>: the HUD makes an occasion of it), not when someone remembers
    /// to look. The first landing of each class at this airport is kept in <see cref="GameState.classesHere"/>.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly List<AmbitionDef> Ambitions = new List<AmbitionDef>
        {
            new AmbitionDef { Id = "first_jet", Name = "Welcome your first jet", Pitch = "A real jet on your runway: the airport's name starts to travel.", Icon = "plane", Gems = 10, Class = AircraftClass.C },
            new AmbitionDef { Id = "full_apron", Name = "Fill the apron", Pitch = "Six stands in a row, a jet bridge at each.", Icon = "stands", Gems = 12 },
            new AmbitionDef { Id = "first_widebody", Name = "Bring in a wide-body", Pitch = "Two aisles, three hundred seats, and a stand built for it.", Icon = "heavy", Gems = 20, Class = AircraftClass.D },
            new AmbitionDef { Id = "grand_hall", Name = "Open the whole terminal", Pitch = "Every bay of the concourse floor open for business.", Icon = "checkin", Gems = 20 },
            new AmbitionDef { Id = "first_jumbo", Name = "Prepare for your first jumbo", Pitch = "The biggest aircraft there is, landing at your airport.", Icon = "award", Gems = 40, Class = AircraftClass.E },
        };

        /// <summary>An ambition was reached just now (the HUD's moment: the camera, a fanfare, the banner).</summary>
        public event Action<AmbitionDef> AmbitionReached;

        public static AmbitionDef AmbitionById(string id) { return Ambitions.Find(a => a.Id == id); }
        string AmbitionKey(AmbitionDef a) { return State.map + ":" + a.Id; }

        public bool AmbitionDone(AmbitionDef a) { return State.ambitionsDone.Contains(AmbitionKey(a)); }
        public bool AmbitionClaimed(AmbitionDef a) { return State.ambitionsClaimed.Contains(AmbitionKey(a)); }

        /// <summary>Whether it has happened (the condition itself, read from the airport).</summary>
        bool AmbitionMet(AmbitionDef a)
        {
            if (a.Class.HasValue) return State.classesHere.Contains(ClassId(a.Class.Value));
            switch (a.Id)
            {
                case "full_apron": return StandCount >= MaxStands;
                case "grand_hall": return BayCount > 0 && Bays.All(BayOpen);
                default: return false;
            }
        }

        /// <summary>The route that would bring the first aircraft of a class here (the easiest to reach), or null.</summary>
        public RouteDef AmbitionRoute(AircraftClass c)
        {
            return Content.Routes.Values.Where(r => r.Class == c && r.Airline != "own" && !Content.IsContractRoute(r.Id)).OrderBy(r => r.Stars).ThenBy(r => r.Needs.Count()).FirstOrDefault();
        }

        /// <summary>What an ambition still needs, each thing ticked or not, in the order to do them.</summary>
        public List<AmbitionStep> AmbitionSteps(AmbitionDef a)
        {
            var steps = new List<AmbitionStep>();
            if (a.Class.HasValue)
            {
                var r = AmbitionRoute(a.Class.Value);
                if (r == null) return steps;
                var plane = Content.Aircraft[r.Class];
                steps.Add(new AmbitionStep { Text = "A " + r.Stars + "-star airport", Ready = Stars >= r.Stars });
                steps.Add(new AmbitionStep { Text = "Runway level " + plane.RunwayLevel + " for the " + plane.Name, Ready = Level("runway") >= plane.RunwayLevel, Facility = "runway" });
                if (Heavy(r.Class)) steps.Add(new AmbitionStep { Text = "A wide-body stand", Ready = HeavyStands > 0, Facility = "heavy" });
                foreach (var n in r.Needs.Distinct())
                {
                    string f = Content.ServiceFacility(n);
                    steps.Add(new AmbitionStep { Text = Content.Facilities[f].Name, Ready = Level(f) > 0, Facility = f });
                }
                bool signed = State.contracts.Exists(x => Content.Routes[x.route].Class == r.Class);
                steps.Add(new AmbitionStep { Text = "A route flown by the " + plane.Name + " (AIRLINES)", Ready = signed });
                return steps;
            }
            switch (a.Id)
            {
                case "full_apron":
                    steps.Add(new AmbitionStep { Text = "Stands: " + StandCount + " of " + MaxStands, Ready = StandCount >= MaxStands, Facility = "stands" });
                    break;
                case "grand_hall":
                    int open = Bays.Count(BayOpen);
                    steps.Add(new AmbitionStep { Text = "Floor bays open: " + open + " of " + BayCount + " (tap a fenced bay inside the terminal)", Ready = open >= BayCount });
                    break;
            }
            return steps;
        }

        /// <summary>The ambition the HUD shows: the pinned one while it waits, else none.</summary>
        public AmbitionDef Pinned
        {
            get
            {
                var a = AmbitionById(State.pinnedAmbition);
                return a != null && !AmbitionClaimed(a) ? a : null;
            }
        }

        /// <summary>The next ambition not yet reached here (for EMPIRE's heading and a pin's default).</summary>
        public AmbitionDef NextAmbition { get { return Ambitions.FirstOrDefault(a => !AmbitionDone(a)); } }

        public void Pin(string id) { State.pinnedAmbition = State.pinnedAmbition == id ? "" : id ?? ""; Changed?.Invoke(); }

        public int AmbitionsWaiting { get { return Ambitions.Count(a => AmbitionDone(a) && !AmbitionClaimed(a)); } }

        /// <summary>Collects an ambition's reward. Returns the gems paid (0: nothing to collect).</summary>
        public int ClaimAmbition(string id)
        {
            var a = AmbitionById(id);
            if (a == null || !AmbitionDone(a) || AmbitionClaimed(a)) return 0;
            State.ambitionsClaimed.Add(AmbitionKey(a));
            State.gems += a.Gems;
            if (State.pinnedAmbition == id) State.pinnedAmbition = "";
            Changed?.Invoke();
            return a.Gems;
        }

        /// <summary>An aircraft of this class is landing here: the first of its kind may be an ambition reached.</summary>
        void NoteLanding(AircraftClass c)
        {
            string id = ClassId(c);
            if (State.classesHere.Contains(id)) return;
            State.classesHere.Add(id);
            CheckAmbitions();
        }

        /// <summary>Marks what has just been reached and says so, once each (cheap: called when something may have changed).</summary>
        void CheckAmbitions()
        {
            foreach (var a in Ambitions)
            {
                if (AmbitionDone(a) || !AmbitionMet(a)) continue;
                State.ambitionsDone.Add(AmbitionKey(a));
                AmbitionReached?.Invoke(a);
                Changed?.Invoke();
            }
        }

        /// <summary>An airport opened before ambitions were kept: what it has already done counts, quietly (no fanfare).</summary>
        void EnsureAmbitions()
        {
            if (State.ambitionsSet) return;
            State.ambitionsSet = true;
            foreach (var c in State.contracts.Select(x => Content.Routes[x.route].Class).Distinct())
                if (State.flightsHandled > 0 && !State.classesHere.Contains(ClassId(c))) State.classesHere.Add(ClassId(c));
            foreach (var a in Ambitions) if (AmbitionMet(a) && !AmbitionDone(a)) State.ambitionsDone.Add(AmbitionKey(a));
        }
    }
}
