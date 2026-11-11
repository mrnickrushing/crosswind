using System;

namespace Crosswind.Core
{
    /// <summary>A loan the bank offers: how many hours of today's income it lends, and what it wants back for each unit lent.</summary>
    public sealed class LoanDef { public string Id, Name; public double Hours, Interest; public int Stars; }

    /// <summary>
    /// The money side you steer. Fee policy: charge airlines less and they send more flights and warm to you, charge
    /// more and each flight pays better while they cool. Loans: the bank advances some hours of the airport's income
    /// and takes a fifth of every payment until it has its money back with interest; one loan at a time.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly string[] FeeName = { "BARGAIN", "KEEN", "STANDARD", "FIRM", "PREMIUM" };
        static readonly float[] FeePayScale = { 0.8f, 0.9f, 1f, 1.12f, 1.25f }, FeeFlightScale = { 1.16f, 1.08f, 1f, 0.93f, 0.85f }, FeeMood = { 0.25f, 0.12f, 0f, -0.12f, -0.25f };
        public const int FeeStars = 3;

        public int FeeLevel { get { return Math.Max(0, Math.Min(4, State.feeLevel)); } }
        public float FeePay { get { return FeePayScale[FeeLevel]; } }
        public float FeeFlights { get { return FeeFlightScale[FeeLevel]; } }
        /// <summary>What every handled flight adds to (or takes from) its airline's mood because of your fees.</summary>
        public float FeeMoodPerFlight { get { return FeeMood[FeeLevel]; } }
        public static string FeeBlurb(int level)
        {
            return "Each flight pays " + Math.Round(FeePayScale[level] * 100) + "%, airlines send " + Math.Round(FeeFlightScale[level] * 100) + "% of their flights"
                + (FeeMood[level] > 0 ? " and like you more for it." : FeeMood[level] < 0 ? " and like you a little less each time." : ".");
        }

        public string FeeBlocked(int level)
        {
            if (level < 0 || level > 4) return "Unknown";
            if (Stars < FeeStars && level != 2) return "Needs a " + FeeStars + "-star airport";
            return null;
        }

        public bool SetFees(int level)
        {
            if (FeeBlocked(level) != null) return false;
            State.feeLevel = level;
            Changed?.Invoke();
            return true;
        }

        public static readonly LoanDef[] Loans =
        {
            new LoanDef { Id = "bridge", Name = "Bridging Loan", Hours = 0.5, Interest = 1.08, Stars = 2 },
            new LoanDef { Id = "growth", Name = "Growth Loan", Hours = 2, Interest = 1.18, Stars = 3 },
            new LoanDef { Id = "expansion", Name = "Expansion Loan", Hours = 6, Interest = 1.32, Stars = 5 },
        };
        public const double LoanShare = 0.2;

        public bool InDebt { get { return State.loanOwed > 0.5; } }

        /// <summary>What the bank would lend: that many hours of what the airport earns by itself, and never less than a small float.</summary>
        public double LoanAmount(LoanDef l) { return Math.Floor(Math.Max(2000 * Math.Pow(4, Stars - 1), IdleRate() * 3600 * l.Hours)); }

        public string LoanBlocked(LoanDef l)
        {
            if (Stars < l.Stars) return "Needs a " + l.Stars + "-star airport";
            if (InDebt) return "Repay the loan you have first";
            return null;
        }

        public bool TakeLoan(string id)
        {
            var l = Array.Find(Loans, x => x.Id == id);
            if (l == null || LoanBlocked(l) != null) return false;
            double amount = LoanAmount(l);
            State.money += amount;                    // borrowed, not earned: no rating, no lifetime income
            State.loanOwed = Math.Floor(amount * l.Interest);
            State.loanTotal = State.loanOwed;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Pays the loan down from cash in hand (all of it, or as much as there is).</summary>
        public bool RepayLoan()
        {
            if (!InDebt || State.money < 1) return false;
            double pay = Math.Min(State.loanOwed, State.money);
            State.money -= pay; State.loanOwed -= pay;
            if (State.loanOwed < 0.5) { State.loanOwed = 0; Toast?.Invoke("Loan repaid in full"); }
            Changed?.Invoke();
            return true;
        }

        /// <summary>The bank's share of a payment that is about to be banked; returns what is left for the airport.</summary>
        double AfterLoan(double income)
        {
            if (!InDebt || income <= 0) return income;
            double take = Math.Min(State.loanOwed, income * LoanShare);
            State.loanOwed -= take;
            if (State.loanOwed < 0.5) { State.loanOwed = 0; Toast?.Invoke("Loan repaid in full"); }
            return income - take;
        }
    }
}
