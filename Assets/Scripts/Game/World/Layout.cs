using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Where everything stands. The runway runs south to north on the west side; a taxiway runs beside it; the stands
    /// are columns of eight east of that, facing the terminal, which sits east of the last column. Aircraft land
    /// heading north, turn off, taxi south or north to their stand, and leave by the south end.
    /// </summary>
    public static class Layout
    {
        public const float RunwayX = -30f, TaxiX = -12f, StandX0 = 6f, StandGapZ = 13f, ColumnGap = 20f;
        public const int PerColumn = 8;

        public static float RunwayHalf(Sim s) { return Mathf.Min(120f, 46f + s.Level("runway") * 4f); }
        public static float RunwayWidth(Sim s) { return 7f + Mathf.Min(8, s.Level("runway")) * 0.6f; }
        public static int Columns(Sim s) { return Mathf.Max(1, (s.StandCount + PerColumn - 1) / PerColumn); }

        public static Vector3 Stand(int i)
        {
            return new Vector3(StandX0 + (i / PerColumn) * ColumnGap, 0, -45.5f + (i % PerColumn) * StandGapZ);
        }

        /// <summary>The terminal's west face (x) for this many columns of stands.</summary>
        public static float TerminalX(Sim s) { return StandX0 + (Columns(s) - 1) * ColumnGap + 13f; }

        /// <summary>Runway exit and entry points.</summary>
        public static float ExitZ(Sim s) { return RunwayHalf(s) * 0.45f; }
        public static float LineUpZ(Sim s) { return -RunwayHalf(s) + 7f; }

        public static List<Vector3> TaxiIn(Sim s, int stand)
        {
            var p = Stand(stand);
            float lane = p.x - 9f;      // the apron lane just west of this column
            return new List<Vector3>
            {
                new Vector3(RunwayX, 0, ExitZ(s)), new Vector3(TaxiX, 0, ExitZ(s)), new Vector3(TaxiX, 0, p.z),
                new Vector3(lane, 0, p.z), p,
            };
        }

        public static List<Vector3> TaxiOut(Sim s, int stand)
        {
            var p = Stand(stand);
            return new List<Vector3>
            {
                p, new Vector3(p.x - 9f, 0, p.z), new Vector3(TaxiX, 0, p.z), new Vector3(TaxiX, 0, LineUpZ(s)),
                new Vector3(RunwayX + RunwayWidth(s) * 0.5f + 4f, 0, LineUpZ(s)),
            };
        }

        /// <summary>A point a fraction of the way along a path, and the direction of travel there.</summary>
        public static Vector3 Along(List<Vector3> path, float t, out Vector3 dir)
        {
            float total = 0;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float want = Mathf.Clamp01(t) * total;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (want <= seg || i == path.Count - 1)
                {
                    dir = (path[i] - path[i - 1]).normalized;
                    return Vector3.Lerp(path[i - 1], path[i], seg <= 0 ? 1 : Mathf.Clamp01(want / seg));
                }
                want -= seg;
            }
            dir = Vector3.forward;
            return path[path.Count - 1];
        }
    }
}
