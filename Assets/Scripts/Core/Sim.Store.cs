using System;
using System.Collections.Generic;

namespace Crosswind.Core
{
    /// <summary>Something sold for real money. Ids are the App Store product ids.</summary>
    public sealed class ProductDef
    {
        public string Id, Name, Blurb, FallbackPrice;
        public int Gems;
        public bool Once;               // non-consumable: bought once, restored on a new phone
    }

    /// <summary>
    /// The shop. Purchases go through the platform (the game layer); this is what each one gives, granted once per
    /// transaction id so a retry or a restore never pays twice. Everything sold is a speed-up or a convenience:
    /// gems are also earned by playing, and nothing in the game needs a purchase.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly List<ProductDef> Products = new List<ProductDef>
        {
            new ProductDef { Id = "crosswind.gems.small", Name = "Handful of Gems", Blurb = "80 gems", Gems = 80, FallbackPrice = "$1.99" },
            new ProductDef { Id = "crosswind.gems.medium", Name = "Case of Gems", Blurb = "500 gems: best for a first upgrade spree", Gems = 500, FallbackPrice = "$9.99" },
            new ProductDef { Id = "crosswind.gems.large", Name = "Vault of Gems", Blurb = "1,400 gems", Gems = 1400, FallbackPrice = "$19.99" },
            new ProductDef { Id = "crosswind.starter", Name = "Founder's Pack", Blurb = "300 gems and a third building crew, for good. One time only.", Gems = 300, Once = true, FallbackPrice = "$4.99" },
            new ProductDef { Id = "crosswind.double", Name = "Golden Runway", Blurb = "All income doubled, for good, at every airport.", Once = true, FallbackPrice = "$9.99" },
        };

        public bool Owns(string product) { return State.owned.Contains(product); }

        /// <summary>Grants a purchase (idempotent per transaction). Returns false if this transaction was already granted.</summary>
        public bool Grant(string product, string transaction)
        {
            var p = Products.Find(x => x.Id == product);
            if (p == null) return false;
            if (!string.IsNullOrEmpty(transaction)) { if (State.transactions.Contains(transaction)) return false; State.transactions.Add(transaction); }
            if (p.Once) { if (State.owned.Contains(product)) return false; State.owned.Add(product); }
            State.gems += p.Gems;
            Changed?.Invoke();
            return true;
        }
    }
}
