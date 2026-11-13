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

    /// <summary>A purchase this device started and hasn't been paid out for yet.</summary>
    [Serializable]
    public sealed class PendingPurchase
    {
        public string product;
        public double since;            // real time it was started
    }

    /// <summary>One App Store transaction as the platform store reports it.</summary>
    public readonly struct StoreReceipt
    {
        public readonly string Product, Transaction;
        public readonly double At;      // real time of purchase, seconds
        public StoreReceipt(string product, string transaction, double at) { Product = product; Transaction = transaction; At = at; }
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
            new ProductDef { Id = "crosswind.starter", Name = "Founder's Pack", Blurb = "300 gems now and a third building crew for good. One time only.", Gems = 300, Once = true, FallbackPrice = "$4.99" },
            new ProductDef { Id = "crosswind.double", Name = "Golden Runway", Blurb = "All income doubled, for good, at every airport.", Once = true, FallbackPrice = "$9.99" },
        };

        public bool Owns(string product) { return State.owned.Contains(product); }

        /// <summary>Grants a purchase (idempotent per transaction). Returns false if this transaction was already granted.
        /// A restore (<paramref name="restored"/>) brings back what is kept for good (the crew, the doubled income) but not a
        /// pack's gems: those were paid out with the purchase itself, so erasing the game and restoring can't mint more.</summary>
        public bool Grant(string product, string transaction, bool restored = false)
        {
            var p = Products.Find(x => x.Id == product);
            if (p == null) return false;
            if (!string.IsNullOrEmpty(transaction)) { if (State.transactions.Contains(transaction)) return false; State.transactions.Add(transaction); }
            if (p.Once) { if (State.owned.Contains(product)) return false; State.owned.Add(product); }
            if (!restored) State.gems += p.Gems;
            EndPurchase(product);
            Changed?.Invoke();
            return true;
        }

        public static ProductDef Product(string id) { return Products.Find(x => x.Id == id); }

        /// <summary>How long a started purchase is watched for (Ask to Buy can take days).</summary>
        public const double PendingFor = 7 * 86400;
        /// <summary>A receipt may be stamped a little before our own clock said the purchase began.</summary>
        public const double ClockSlack = 120;

        public bool HasPendingPurchases { get { return State.pending.Count > 0; } }

        /// <summary>Notes that a purchase is starting, so it can be paid out later if the app dies before the store answers.</summary>
        public void BeginPurchase(string product)
        {
            if (Product(product) == null) return;
            State.pending.Add(new PendingPurchase { product = product, since = RealNow() });
        }

        /// <summary>The purchase was cancelled or has been paid out: stop watching for it.</summary>
        public void EndPurchase(string product)
        {
            int i = State.pending.FindIndex(x => x.product == product);
            if (i >= 0) State.pending.RemoveAt(i);
        }

        /// <summary>
        /// Pays out purchases this device started that the store charged but never reported back (a crash, a lost
        /// connection, Ask to Buy approved later). Only transactions made after the purchase began count, so an old
        /// gem purchase from another phone is never paid twice. Returns the names of what arrived.
        /// </summary>
        public List<string> ReconcilePurchases(IEnumerable<StoreReceipt> receipts)
        {
            var got = new List<string>();
            if (receipts == null) return got;
            var list = new List<StoreReceipt>(receipts);
            list.Sort((a, b) => a.At.CompareTo(b.At));
            foreach (var pend in State.pending.ToArray())
            {
                foreach (var r in list)
                {
                    if (r.Product != pend.product || string.IsNullOrEmpty(r.Transaction)) continue;
                    if (State.transactions.Contains(r.Transaction) || r.At < pend.since - ClockSlack) continue;
                    if (Grant(r.Product, r.Transaction)) got.Add(Product(r.Product).Name);
                    else EndPurchase(r.Product);     // owned already
                    break;
                }
            }
            double now = RealNow();
            State.pending.RemoveAll(x => now - x.since > PendingFor);
            return got;
        }

        /// <summary>One-time purchases the App Store account owns come back on a new phone. Returns how many were added.</summary>
        public int RestoreOwned(IEnumerable<string> productIds)
        {
            int n = 0;
            if (productIds == null) return 0;
            foreach (var id in productIds)
            {
                var p = Product(id);
                if (p == null || !p.Once || State.owned.Contains(id)) continue;
                if (Grant(id, null, restored: true)) n++;
            }
            return n;
        }
    }
}
