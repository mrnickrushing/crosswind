using System.Globalization;

namespace Crosswind.Core
{
    /// <summary>What sits beside the save in iCloud: when it was saved, how many stars it has and which device wrote it.</summary>
    public sealed class CloudMeta
    {
        public double SavedAt;
        public int Stars;
        public string Device;

        public override string ToString()
        {
            return SavedAt.ToString("R", CultureInfo.InvariantCulture) + "|" + Stars + "|" + Device;
        }

        public static CloudMeta Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var p = s.Split('|');
            if (p.Length < 3) return null;
            double at; int lv;
            if (!double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return null;
            int.TryParse(p[1], out lv);
            return new CloudMeta { SavedAt = at, Stars = lv, Device = p[2] };
        }
    }

    public enum CloudStep
    {
        Upload,   // ours is the one to keep: write it to iCloud
        Adopt,    // nothing here yet: continue from the iCloud save at once (a new phone)
        Offer,    // another device saved since we last looked: ask before replacing either
    }

    /// <summary>
    /// When the iCloud copy of the save wins. A device never overwrites a save another device wrote after the last
    /// one this device took, kept or wrote itself ("seen"), so a new game started before iCloud has caught up can't
    /// wipe a long-running airport: the player is asked instead.
    /// </summary>
    public static class CloudRules
    {
        public static CloudStep Decide(bool haveLocal, CloudMeta cloud, string me, double seen)
        {
            if (cloud == null) return CloudStep.Upload;
            if (!haveLocal) return CloudStep.Adopt;
            if (cloud.Device != me && cloud.SavedAt > seen + 1) return CloudStep.Offer;
            return CloudStep.Upload;
        }
    }
}
