using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Incidents: when they come up, what the answers do, and what happens when nobody answers.</summary>
    public class IncidentTests
    {
        double _clock;

        /// <summary>A three-star airport with money, flights behind it and the departments incidents talk about.</summary>
        Sim Airport()
        {
            _clock = 1_800_000_000;
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => _clock;
            s.State.money = 1e9;
            s.State.ratingXp = Content.StarXp[3];
            for (int i = 0; i < 2; i++) s.Upgrade("stands");
            s.Upgrade("baggage"); s.Upgrade("runway"); s.Upgrade("runway");
            for (int i = 0; i < 5; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            s.Hire("fuel"); s.Hire("baggage");
            s.State.flightsHandled = 40;
            s.State.nextIncidentAt = 1e12;      // none until a test asks for one
            s.Tick(0.1f);
            return s;
        }

        void Run(Sim s, float seconds) { for (float t = 0; t < seconds; t += 0.25f) { s.Tick(0.25f); _clock += 0.25; } }

        string AnAirline(Sim s) { return Content.Routes[s.State.contracts[0].route].Airline; }

        void Put(Sim s, string id, double value = 1000, float wait = 75)
        {
            s.State.incident = new IncidentState { id = id, expiresAt = s.State.time + wait, value = value, airline = AnAirline(s) };
        }

        [Test]
        public void NothingComesUpAtANewAirport()
        {
            var s = new Sim(GameState.New());
            s.RealNow = () => 1_800_000_000;
            for (int i = 0; i < 4 * 1500; i++) s.Tick(0.25f);
            Assert.IsNull(s.State.incident);
            Assert.IsEmpty(s.State.incidentLog);
        }

        [Test]
        public void AnIncidentComesUpSaysWhatItIsAndWaits()
        {
            var s = Airport();
            IncidentState seen = null;
            s.IncidentAppeared += i => seen = i;
            s.State.nextIncidentAt = 0;
            s.Tick(0.25f);
            Assert.IsNotNull(s.State.incident);
            Assert.AreSame(s.State.incident, seen);
            var def = s.Incident;
            Assert.IsNotNull(def);
            Assert.AreEqual(2, def.Choices.Length);
            Assert.IsNotEmpty(s.IncidentStory);
            Assert.IsNotEmpty(s.IncidentDoes(0));
            Assert.IsNotEmpty(s.IncidentDoes(1));
            Assert.Greater(s.IncidentLeft, Sim.IncidentSeconds - 1);
            Assert.Greater(s.State.incident.value, 0);
            Assert.Greater(s.State.nextIncidentAt, s.State.time + 200, "the next one is minutes away");
        }

        [Test]
        public void EveryIncidentCanBeAnsweredEitherWay()
        {
            foreach (var def in Sim.Incidents)
                for (int c = 0; c < def.Choices.Length; c++)
                {
                    var s = Airport();
                    string said = null;
                    s.IncidentSettled += line => said = line;
                    Put(s, def.Id);
                    Assert.IsNull(s.IncidentBlocked(c), def.Id + " " + c);
                    Assert.IsTrue(s.DecideIncident(c), def.Id + " " + c);
                    Assert.IsNull(s.State.incident);
                    Assert.AreEqual(1, s.State.incidentsDecided);
                    Assert.AreEqual(def.Id, s.State.incidentLog[0].id);
                    Assert.AreEqual(c, s.State.incidentLog[0].choice);
                    Assert.IsFalse(s.State.incidentLog[0].lapsed);
                    Assert.IsNotEmpty(s.State.incidentLog[0].line);
                    Assert.AreEqual(s.State.incidentLog[0].line, said);
                    Assert.GreaterOrEqual(s.State.money, 0);
                }
        }

        [Test]
        public void TheDefaultAnswerIsAlwaysFreeToGive()
        {
            foreach (var def in Sim.Incidents)
            {
                var s = Airport();
                s.State.money = 0;
                Put(s, def.Id);
                Assert.IsNull(s.IncidentBlocked(def.Default), def.Id + ": the answer given when nobody decides must not need money");
            }
        }

        [Test]
        public void AnAnswerThatCostsMoneyNeedsIt()
        {
            var s = Airport();
            s.State.money = 100;
            Put(s, "power", 1000);
            StringAssert.StartsWith("Need", s.IncidentBlocked(0));
            Assert.IsFalse(s.DecideIncident(0));
            Assert.IsNotNull(s.State.incident, "still waiting");
            s.State.money = 10000;
            Assert.IsTrue(s.DecideIncident(0));
            Assert.AreEqual(10000 - 1500, s.State.money, 0.5);
            Assert.AreEqual(0, s.MoodShift, "the generators kept the hall happy");
        }

        [Test]
        public void LeftUnansweredItSettlesThePassiveWay()
        {
            var s = Airport();
            string said = null;
            s.IncidentSettled += line => said = line;
            Put(s, "power", 1000, 1f);
            Run(s, 2);
            Assert.IsNull(s.State.incident);
            Assert.IsTrue(s.State.incidentLog[0].lapsed);
            Assert.AreEqual(1, s.State.incidentLog[0].choice);
            Assert.AreEqual(0, s.State.incidentsDecided, "nobody decided anything");
            Assert.Less(s.MoodShift, 0);
            StringAssert.StartsWith("No answer", said);
        }

        [Test]
        public void APassengersMoodShiftShowsInSatisfactionAndWearsOff()
        {
            var s = Airport();
            float before = s.Satisfaction;
            Assert.Greater(before, 0.55f, "room to fall");
            Put(s, "power");
            s.DecideIncident(1);
            Assert.Less(s.Satisfaction, before - 0.05f);
            Assert.Greater(s.MoodLeft, 90);
            Run(s, 105);
            Assert.AreEqual(0, s.MoodShift);
        }

        [Test]
        public void ClearingTheRunwayHoldsTrafficForAMoment()
        {
            var s = Airport();
            string line = AnAirline(s);
            float mood = s.Relation(line);
            Put(s, "medical");
            s.DecideIncident(0);
            Assert.IsTrue(s.RunwayClosed);
            Assert.Greater(s.Relation(line), mood, "the airline remembers");
            Run(s, 21);
            Assert.IsFalse(s.RunwayClosed);
        }

        [Test]
        public void TheDiscountTankerFillsTheTanksForLess()
        {
            var s = Airport();
            s.Upgrade("fuel");
            s.State.fuelStock = 0;
            double full = s.FuelCost(1f), deal = s.DiscountFuelCost;
            Assert.Less(deal, full * 0.65);
            Assert.Greater(deal, full * 0.55);
            double money = s.State.money;
            Put(s, "fuel");
            s.DecideIncident(0);
            Assert.AreEqual(1f, s.FuelFill, 0.001f);
            Assert.AreEqual(money - deal, s.State.money, 0.5);
        }

        [Test]
        public void TheInspectionLooksAtTheRunway()
        {
            var good = Airport();
            good.State.runwayWear = 0.1f;
            int gems = good.State.gems;
            Put(good, "inspection");
            good.DecideIncident(0);
            Assert.AreEqual(gems + 2, good.State.gems);

            var worn = Airport();
            worn.State.runwayWear = 0.7f;
            worn.State.money = 5000;
            Put(worn, "inspection", 1000);
            worn.DecideIncident(0);
            Assert.AreEqual(3000, worn.State.money, 0.5, "a fine of two minutes' takings");
        }

        [Test]
        public void TheyKeepComingAndNeverTheSameTwiceRunning()
        {
            var s = Airport();
            s.State.nextIncidentAt = 0;
            var seen = new List<string>();
            s.IncidentAppeared += i => seen.Add(i.id);
            for (int i = 0; i < 4 * 3600; i++)
            {
                s.Tick(0.25f); _clock += 0.25;
                if (s.State.incident != null && i % 40 == 0 && !s.DecideIncident(0)) s.DecideIncident(1);
            }
            Assert.GreaterOrEqual(seen.Count, 6, "an hour brings at least six");
            Assert.LessOrEqual(seen.Count, 16);
            for (int i = 1; i < seen.Count; i++) Assert.AreNotEqual(seen[i - 1], seen[i]);
            Assert.GreaterOrEqual(seen.Distinct().Count(), 4);
            Assert.LessOrEqual(s.State.incidentLog.Count, Sim.IncidentLogSize);
        }

        [Test]
        public void DecisionsCountTowardAnAchievement()
        {
            var s = Airport();
            var a = Sim.Achievements.First(x => x.Id == "incidents_10");
            s.State.incidentsDecided = 9;
            Assert.IsFalse(s.ClaimAchievement(a.Id));
            Put(s, "fox");
            s.DecideIncident(1);
            Assert.IsTrue(s.ClaimAchievement(a.Id));
        }

        [Test]
        public void ASaveWithAnEmptyIncidentIsRepaired()
        {
            var st = GameState.New();
            st.incident = new IncidentState();      // what JsonUtility writes for null
            st.incidentLog = null;
            st.Repair();
            Assert.IsNull(st.incident);
            Assert.IsNotNull(st.incidentLog);
        }
    }
}
