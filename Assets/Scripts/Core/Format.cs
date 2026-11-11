using System;
using System.Globalization;

namespace Crosswind.Core
{
    /// <summary>Numbers for the screen: money that grows from tens to quintillions, and short durations.</summary>
    public static class Format
    {
        static readonly string[] Suffix = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        /// <summary>1234 -> "1.23K", 15_600_000 -> "15.6M"; whole numbers below a thousand.</summary>
        public static string Money(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            string sign = v < 0 ? "-" : "";
            v = Math.Abs(v);
            if (v < 1000) return sign + Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);
            int tier = Math.Min(Suffix.Length - 1, (int)Math.Floor(Math.Log10(v) / 3));
            double scaled = v / Math.Pow(1000, tier);
            // Three significant figures, never rounding up into the next thousand ("999.9K" not "1000K").
            string digits = scaled >= 100 ? Math.Floor(scaled).ToString("0", CultureInfo.InvariantCulture)
                : scaled >= 10 ? (Math.Floor(scaled * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture)
                : (Math.Floor(scaled * 100) / 100).ToString("0.##", CultureInfo.InvariantCulture);
            return sign + digits + Suffix[tier];
        }

        /// <summary>"45s", "3m 20s", "2h 05m", "1d 4h".</summary>
        public static string Time(double seconds)
        {
            long s = (long)Math.Max(0, Math.Ceiling(seconds));
            if (s < 60) return s + "s";
            if (s < 3600) return s / 60 + "m " + (s % 60).ToString("00", CultureInfo.InvariantCulture) + "s";
            if (s < 86400) return s / 3600 + "h " + (s % 3600 / 60).ToString("00", CultureInfo.InvariantCulture) + "m";
            return s / 86400 + "d " + s % 86400 / 3600 + "h";
        }
    }
}
