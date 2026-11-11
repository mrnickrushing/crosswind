using System;
using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    public enum PurchaseOutcome { Paid, Cancelled, Failed }

    /// <summary>The real-money store as the game sees it.</summary>
    public interface IStore
    {
        /// <summary>Purchases can be made (in a release build: the App Store answered with the products).</summary>
        bool Ready { get; }
        /// <summary>The App Store's own price text ("$4.99", "4,99 €"), or null until it is known.</summary>
        string Price(string productId);
        /// <summary>Calls back with the outcome and the transaction id (Paid) or a message for the player.</summary>
        void Purchase(string productId, Action<PurchaseOutcome, string> done);
        /// <summary>Brings back the account's one-time purchases; calls back with how many were added, or -1 if it failed.</summary>
        void Restore(Action<int> done);
        /// <summary>The account's transactions, for <see cref="Sim.ReconcilePurchases"/> (null if they can't be read now).</summary>
        void Receipts(Action<List<StoreReceipt>> done);
        /// <summary>Ties purchases to the signed-in account (Sign in with Apple user id), or null to go anonymous.</summary>
        void Identify(string appUserId);
    }

    /// <summary>
    /// Buying things. On an iPhone or iPad purchases go through the App Store by way of RevenueCat (the package
    /// defines CROSSWIND_REVENUECAT; see docs/STORE.md). In the Editor and in development builds a test store grants
    /// at once so the shop can be tried; a release build without a working store sells nothing, never gives for free.
    /// Every grant goes through <see cref="Sim.Grant"/>, once per App Store transaction, and a purchase is noted
    /// before it starts so one cut short by a crash is paid out at the next launch.
    /// </summary>
    public static class Store
    {
        static IStore _store;
        public static IStore Current
        {
            get
            {
                if (_store != null) return _store;
#if CROSSWIND_REVENUECAT
                _store = RevenueCatStore.Configured && !Application.isEditor ? RevenueCatStore.Create() : (IStore)new TestStore();
#else
                _store = new TestStore();
#endif
                return _store;
            }
        }

        /// <summary>Call once the save is loaded: starts the store, so prices load and anything owed is delivered.</summary>
        public static void Start()
        {
            var s = Current;
            if (AppleAccount.SignedIn) s.Identify(AppleAccount.UserId);
            AppleAccount.Changed -= OnAccount; AppleAccount.Changed += OnAccount;
        }

        static void OnAccount() { Current.Identify(AppleAccount.SignedIn ? AppleAccount.UserId : null); }

        /// <summary>The test store is in use (Editor and development builds): purchases are pretend.</summary>
        public static bool Testing { get { return Current is TestStore; } }
        public static bool Available { get { return Current.Ready; } }

        public static string Price(ProductDef p) { var s = Current.Price(p.Id); return string.IsNullOrEmpty(s) ? p.FallbackPrice : s; }

        static bool _buying;

        public static void Buy(ProductDef p, Action<bool, string> done)
        {
            if (!Available) { done(false, "The shop can't reach the App Store right now. Check your connection and try again."); return; }
            if (_buying) { done(false, "A purchase is already under way"); return; }
            var sim = GameRoot.Sim;
            if (p.Once && sim.Owns(p.Id)) { done(false, "You already own this"); return; }
            _buying = true;
            sim.BeginPurchase(p.Id);
            GameRoot.Instance.Save();
            Current.Purchase(p.Id, (outcome, txOrMessage) =>
            {
                _buying = false;
                bool ok = false; string msg;
                switch (outcome)
                {
                    case PurchaseOutcome.Paid:
                        ok = GameRoot.Sim.Grant(p.Id, txOrMessage);
                        msg = ok ? p.Name + (Testing ? " added (test purchase)" : " added. Thank you!") : "Already delivered";
                        if (!ok) GameRoot.Sim.EndPurchase(p.Id);
                        break;
                    case PurchaseOutcome.Cancelled:
                        GameRoot.Sim.EndPurchase(p.Id);
                        msg = txOrMessage;
                        break;
                    default:        // the App Store may still have charged (a dropped connection, Ask to Buy): keep watching
                        msg = txOrMessage;
                        Reconcile();
                        break;
                }
                GameRoot.Instance.Save();
                done(ok, msg);
            });
        }

        public static void Restore(Action<string> done)
        {
            Current.Restore(n =>
            {
                if (n > 0) GameRoot.Instance.Save();
                done(n < 0 ? "Couldn't reach the App Store. Try again in a moment." : n == 0 ? "Nothing to restore: one-time purchases you own are already here" : n == 1 ? "1 purchase restored" : n + " purchases restored");
            });
        }

        /// <summary>Delivers purchases this device started that never got their reward.</summary>
        public static void Reconcile()
        {
            if (GameRoot.Sim == null || !GameRoot.Sim.HasPendingPurchases) return;
            Current.Receipts(Deliver);
        }

        public static void Deliver(List<StoreReceipt> receipts)
        {
            var sim = GameRoot.Sim;
            if (sim == null || !sim.HasPendingPurchases || receipts == null) return;
            var got = sim.ReconcilePurchases(receipts);
            GameRoot.Instance.Save();
            if (got.Count > 0 && Hud.Instance != null) Hud.Instance.Toast("Your purchase arrived: " + string.Join(", ", got));
        }
    }

    /// <summary>Pretend purchases for the Editor and development builds. A release build never uses it to give anything.</summary>
    public sealed class TestStore : IStore
    {
        static bool Allowed { get { return Application.isEditor || Debug.isDebugBuild; } }
        public bool Ready { get { return Allowed; } }
        public string Price(string productId) { return null; }

        public void Purchase(string productId, Action<PurchaseOutcome, string> done)
        {
            if (!Allowed) { done(PurchaseOutcome.Cancelled, "The shop isn't open yet"); return; }
            done(PurchaseOutcome.Paid, "dev-" + Guid.NewGuid().ToString("N"));
        }

        public void Restore(Action<int> done) { done(0); }
        public void Receipts(Action<List<StoreReceipt>> done) { done(new List<StoreReceipt>()); }
        public void Identify(string appUserId) { }
    }

#if CROSSWIND_REVENUECAT
    /// <summary>
    /// App Store purchases through RevenueCat. The product ids are <see cref="Sim.Products"/>; the game buys and
    /// restores by product id, so RevenueCat needs no entitlements or offerings.
    /// </summary>
    public sealed class RevenueCatStore : IStore
    {
        /// <summary>RevenueCat, project Crosswind, API keys: the Apple App Store public SDK key (made to ship in the app).</summary>
        public const string ApiKey = "appl_REPLACE";
        public static bool Configured { get { return ApiKey.StartsWith("appl_") && !ApiKey.Contains("REPLACE"); } }

        Purchases _purchases;
        bool _booted;                               // Purchases configures itself in its Start(); calls before that fail
        string _pendingUser; bool _hasPendingUser;  // Identify() before boot
        float _pricesAskedAt = -999;
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        bool _loaded;

        /// <summary>Asks again for the products while they haven't arrived (the first try can be offline).</summary>
        public bool Ready { get { if (!_loaded && _booted && Time.realtimeSinceStartup - _pricesAskedAt > 15) LoadPrices(); return _loaded; } }

        sealed class Boot : MonoBehaviour
        {
            public RevenueCatStore Store;
            System.Collections.IEnumerator Start()
            {
                yield return null;
                Store._booted = true;
                if (Store._hasPendingUser) Store.Identify(Store._pendingUser);
                else Crosswind.Game.Store.Reconcile();
                Store.LoadPrices();
            }
        }

        /// <summary>Transactions the App Store delivers again at launch (after a crash mid-purchase) arrive here.</summary>
        sealed class Listener : Purchases.UpdatedCustomerInfoListener
        {
            public override void CustomerInfoReceived(Purchases.CustomerInfo info)
            {
                if (GameRoot.Sim != null && GameRoot.Sim.HasPendingPurchases) Crosswind.Game.Store.Deliver(ToReceipts(info));
            }
        }

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static List<StoreReceipt> ToReceipts(Purchases.CustomerInfo info)
        {
            if (info == null || info.NonSubscriptionTransactions == null) return null;
            return info.NonSubscriptionTransactions.Where(t => t != null)
                .Select(t => new StoreReceipt(t.ProductIdentifier, t.TransactionIdentifier, (t.PurchaseDate.ToUniversalTime() - Epoch).TotalSeconds)).ToList();
        }

        static string[] Ids { get { return Sim.Products.Select(p => p.Id).ToArray(); } }

        public static RevenueCatStore Create()
        {
            var s = new RevenueCatStore();
            var go = new GameObject("RevenueCat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            s._purchases = go.AddComponent<Purchases>();
            s._purchases.revenueCatAPIKeyApple = ApiKey;
            s._purchases.useRuntimeSetup = false;
            s._purchases.productIdentifiers = Ids;
            s._purchases.listener = go.AddComponent<Listener>();
            go.AddComponent<Boot>().Store = s;      // prices load one frame after Purchases.Start has configured the SDK
            go.SetActive(true);
            return s;
        }

        void LoadPrices()
        {
            _pricesAskedAt = Time.realtimeSinceStartup;
            _purchases.GetProducts(Ids, (products, error) =>
            {
                if (error != null) { Debug.LogWarning("[RevenueCat] products: " + error.Message); return; }
                foreach (var p in products) _prices[p.Identifier] = p.PriceString;
                _loaded = products.Count > 0;
            }, "inapp");
        }

        public string Price(string productId) { string p; return _prices.TryGetValue(productId, out p) ? p : null; }

        public void Purchase(string productId, Action<PurchaseOutcome, string> done)
        {
            if (!_booted) { done(PurchaseOutcome.Cancelled, "The shop is still loading. Try again in a moment."); return; }
            _purchases.PurchaseProduct(productId, result =>
            {
                if (result.UserCancelled) { done(PurchaseOutcome.Cancelled, "Purchase cancelled"); return; }
                if (result.Error != null) { done(PurchaseOutcome.Failed, result.Error.Message); return; }
                var tx = result.StoreTransaction;
                string id = tx != null ? tx.TransactionIdentifier : null;
                if (string.IsNullOrEmpty(id))       // fall back to the account's newest transaction for this product
                {
                    var r = ToReceipts(result.CustomerInfo);
                    if (r != null) id = r.Where(x => x.Product == productId).OrderByDescending(x => x.At).Select(x => x.Transaction).FirstOrDefault();
                }
                if (string.IsNullOrEmpty(id)) id = "rc-" + Guid.NewGuid().ToString("N");
                done(PurchaseOutcome.Paid, id);
            }, "inapp");
        }

        public void Receipts(Action<List<StoreReceipt>> done)
        {
            if (!_booted) { done(null); return; }
            _purchases.GetCustomerInfo((info, error) =>
            {
                if (error != null) { Debug.LogWarning("[RevenueCat] customer info: " + error.Message); done(null); return; }
                done(ToReceipts(info));
            });
        }

        public void Identify(string appUserId)
        {
            if (!_booted) { _pendingUser = appUserId; _hasPendingUser = true; return; }
            _hasPendingUser = false;
            if (!string.IsNullOrEmpty(appUserId))
                _purchases.LogIn(appUserId, (info, created, error) =>
                {
                    if (error != null) { Debug.LogWarning("[RevenueCat] log in: " + error.Message); return; }
                    if (GameRoot.Sim != null && GameRoot.Sim.RestoreOwned(info.AllPurchasedProductIdentifiers) > 0) GameRoot.Instance.Save();   // one-time purchases follow the account
                    Crosswind.Game.Store.Deliver(ToReceipts(info));
                });
            else
                _purchases.GetCustomerInfo((current, e) =>
                {
                    // Logging out an account that was never logged in is an error in RevenueCat: only leave a named one.
                    if (e != null || current == null || current.OriginalAppUserId == null || current.OriginalAppUserId.StartsWith("$RCAnonymousID")) return;
                    _purchases.LogOut((info, error) => { if (error != null) Debug.LogWarning("[RevenueCat] log out: " + error.Message); });
                });
        }

        public void Restore(Action<int> done)
        {
            if (!_booted) { done(-1); return; }
            _purchases.RestorePurchases((info, error) =>
            {
                if (error != null) { Debug.LogWarning("[RevenueCat] restore: " + error.Message); done(-1); return; }
                done(GameRoot.Sim.RestoreOwned(info.AllPurchasedProductIdentifiers));
            });
        }
    }
#endif
}
