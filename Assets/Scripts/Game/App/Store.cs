using System;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Buying things. The platform store isn't wired in yet (it needs the App Store Connect app and its products):
    /// in the Editor and in development builds a purchase is granted at once so the shop can be tested; in a release
    /// build without a store it says so and grants nothing.
    /// </summary>
    public static class Store
    {
        public static bool Available { get { return Application.isEditor || Debug.isDebugBuild; } }

        public static string Price(ProductDef p) { return p.FallbackPrice; }

        public static void Buy(ProductDef p, Action<bool, string> done)
        {
            if (!Available) { done(false, "The shop opens with the App Store release"); return; }
            bool ok = GameRoot.Sim.Grant(p.Id, "dev-" + Guid.NewGuid().ToString("N"));
            GameRoot.Instance.Save();
            done(ok, ok ? p.Name + " added (test purchase)" : "You already own this");
        }
    }
}
