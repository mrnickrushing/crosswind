#if UNITY_EDITOR
using System.Linq;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Editor play-testing: a greedy player driving the real game (same Sim calls as the buttons) at a multiple of
    /// real speed, so the world, the HUD and the save are exercised together.
    /// From the CLI: enter Play mode, <c>Crosswind.Game.PlayBot.Begin(20)</c>, then read <c>PlayBot.Report()</c>.
    /// </summary>
    public sealed class PlayBot : MonoBehaviour
    {
        public static PlayBot Instance { get; private set; }
        float _think;

        public static void Begin(float speed = 20)
        {
            if (Instance == null) Instance = new GameObject("PlayBot").AddComponent<PlayBot>();
            GameRoot.Speed = speed;
        }

        public static void Stop()
        {
            GameRoot.Speed = 1;
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        public static string Report()
        {
            var s = GameRoot.Sim;
            return s.Map.Name + " t=" + Format.Time(s.State.time) + " " + s.Stars + " stars, money " + Format.Money(s.State.money) + ", " + Format.Money(s.State.incomeRate * 60) + "/min, flights " + s.State.flightsHandled
                + " (" + s.State.late + " late), routes " + s.State.contracts.Count + ", stands " + s.StandCount + ", runway " + s.Level("runway") + ", managers " + s.State.managers.Count + ", research " + s.State.researched.Count
                + ", goal " + s.State.goalIndex + "/" + Goals.All.Count + ", gems " + s.State.gems + ", weather " + s.Weather + ", planes " + s.State.flights.Count;
        }

        void Update()
        {
            var s = GameRoot.Sim;
            foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
            if ((_think -= Time.deltaTime * GameRoot.Speed) > 0) return;
            _think = 1f;
            while (s.ClaimGoal()) { }
            s.TakeOpportunity();
            s.ClaimSignIn();
            s.ClaimEvent();
            for (int k = 0; k < s.State.tasks.Count; k++) s.ClaimTask(k);
            foreach (var a in Sim.Achievements) s.ClaimAchievement(a.Id);
            foreach (var r in s.Offers.ToList()) s.Sign(r.Id);
            foreach (var id in Content.FacilityOrder) if (s.HireBlocked(id) == null) s.Hire(id);
            if (s.FreeCrateReady) s.TakeFreeCrate();
            while (s.State.crates > 0) s.OpenCrate();
            {
                var worst = s.WorstNeed;
                if (worst.Value < 0.9f)
                {
                    var item = Sim.Amenities.Where(a => a.Gives == worst.Want && s.Stars >= a.Stars && s.AmenityCost(a.Id) <= s.State.money * 0.5).OrderByDescending(a => a.Points).FirstOrDefault();
                    if (item != null && !s.PlaceAnywhere(item.Id)) { var up = s.State.terminal.Where(t => Sim.AmenityById(t.id).Gives == worst.Want && s.AmenityUpgradeBlocked(t) == null && s.AmenityUpgradeCost(t) <= s.State.money * 0.5).FirstOrDefault(); if (up != null) s.UpgradeAmenity(up); }
                }
                if (s.LoungeLevel < s.Stars - 3 && s.Stars >= 4 && s.AmenityCost("lounge") <= s.State.money * 0.3) s.PlaceAnywhere("lounge");
            }

            if (s.PlannerOpen && (!s.State.contracts.Exists(c => c.slot > 0) || (int)(s.State.time / 60) % 5 == 0)) s.AutoPlan();
            if (s.State.research == null)
            {
                var next = Content.Research.Where(r => s.ResearchBlocked(r.Id) == null).OrderBy(r => r.Cost).FirstOrDefault();
                if (next != null && s.State.money > s.ResearchCost(next) * 1.5) s.StartResearch(next.Id);
            }
            else if (s.State.gems >= s.ResearchSkipGems + 20) s.SkipResearch();
            if (s.ReportWaiting) s.ClaimReport();
            if (s.RunwayCondition < 0.45f && s.ResurfaceBlocked() == null && s.ResurfaceCost < s.State.money * 0.3) s.Resurface();
            if (s.FuelFill < 0.3f && (s.FuelPrice <= 1.05f || s.FuelFill < 0.08f)) s.BuyFuel(s.FuelPrice < 0.95f ? 1f : 0.6f);
            var wanted = Content.Routes.Values.Where(r => !s.Signed(r.Id) && s.Stars >= r.Stars)
                .SelectMany(s.Missing).Distinct().ToList();
            var pick = wanted.Where(f => s.UpgradeBlocked(f) == null).OrderBy(s.Cost).FirstOrDefault()
                ?? (wanted.Count == 0 ? Content.FacilityOrder.Where(f => s.UpgradeBlocked(f) == null).OrderBy(s.Cost).FirstOrDefault() : null);
            if (pick != null) s.Upgrade(pick);
            foreach (AircraftClass ac in System.Enum.GetValues(typeof(AircraftClass)))
                if (s.BuyAircraftBlocked(ac) == null && s.State.money > s.AircraftCost(ac) * 3) s.BuyAircraft(ac);
        }
    }
}
#endif
