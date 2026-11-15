using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// Advice that names the actual constraint. The terminal moves passengers only as fast as its slowest part
    /// (check-in, security, the gates: <see cref="TerminalFlow"/>), so "upgrade the slowest" is replaced by which part it
    /// is, what one more level of it would do to the queues, and, when two or three are level, that they must go up
    /// together.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>The three parts of the terminal passengers queue at, in the order they meet them.</summary>
        public static readonly string[] FlowParts = { "checkin", "security", "gates" };

        static string PartName(string id) { return id == "checkin" ? "Check-in" : id == "security" ? "Security" : "The gates"; }
        static string PartNoun(string id) { return id == "checkin" ? "check-in" : id == "security" ? "security" : "the gates"; }

        /// <summary>The parts holding the flow back: those at the lowest level (one, or several level with each other).</summary>
        public List<string> FlowLimiters
        {
            get
            {
                int low = FlowParts.Min(p => Level(p));
                return FlowParts.Where(p => Level(p) == low).ToList();
            }
        }

        /// <summary>Passengers a minute the terminal would move with these parts one level higher.</summary>
        public float FlowWith(IEnumerable<string> raised)
        {
            var up = new HashSet<string>(raised);
            int low = FlowParts.Min(p => Level(p) + (up.Contains(p) ? 1 : 0));
            return 6f * low + 4;
        }

        /// <summary>The queues rating (0..1) at a given flow, against today's passengers.</summary>
        public float QueueValueAt(float flow) { return (float)Math.Max(0, Math.Min(1, flow / Math.Max(1, PassengerDemand))); }

        /// <summary>One sentence: which part limits the flow, and what upgrading it would do.</summary>
        public string LimiterSentence(List<string> limit)
        {
            float now = QueueValueAt(TerminalFlow), then = QueueValueAt(FlowWith(limit));
            string effect = then > now + 0.005f ? ": queues " + Pct(now) + " to " + Pct(then) : "";
            if (limit.Count == 1)
            {
                var others = FlowParts.Where(p => p != limit[0]).Select(p => PartNoun(p) + " " + Level(p));
                return PartName(limit[0]) + " is limiting passenger flow (level " + Level(limit[0]) + "; " + string.Join(", ", others) + "). One more level" + effect;
            }
            string names = limit.Count == 3 ? "Check-in, security and the gates" : PartName(limit[0]) + " and " + PartNoun(limit[1]);
            return names + " are level at " + Level(limit[0]) + ": flow rises only when " + (limit.Count == 3 ? "all three go" : "both go") + " up a level" + effect;
        }

        /// <summary>The queue advice for the report and the BUILD sheet (null when the queues are short).</summary>
        public string QueueAdvice
        {
            get
            {
                if (QueueValueAt(TerminalFlow) >= 0.999f) return null;
                // Asked for every time the passengers are rated: built once per change of levels or demand.
                string key = Level("checkin") + "/" + Level("security") + "/" + Level("gates") + "/" + (int)PassengerDemand;
                if (key != _adviceKey) { _adviceKey = key; _advice = LimiterSentence(FlowLimiters); }
                return _advice;
            }
        }
        string _adviceKey, _advice;

        /// <summary>Which limiter to upgrade first: the cheapest of them that isn't already being built (null: all are).</summary>
        public string FlowFix
        {
            get
            {
                return FlowLimiters.Where(p => Building(p) == null).OrderBy(p => Cost(p)).FirstOrDefault();
            }
        }

        /// <summary>What it costs to raise every limiter one level (the price of actually moving the flow).</summary>
        public double FlowFixCost { get { return FlowLimiters.Sum(p => Cost(p)); } }
    }
}
