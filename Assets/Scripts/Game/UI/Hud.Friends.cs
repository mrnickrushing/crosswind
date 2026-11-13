using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// FRIENDS: sign in with Apple to get an account and a six-letter code, add friends by theirs, see how their
    /// airports are doing against yours, collect the gift each one sends every day, and sign out or delete the
    /// account. Typing (a name, a code) happens in a prompt above the sheet, which is rebuilt too often to hold a
    /// text field.
    /// </summary>
    public sealed partial class Hud
    {
        const int FriendsTab = 5;
        RectTransform _prompt;
        bool _friendManage, _friendBusy;
        float _friendsFetched = -999, _profileSent = -999;

        void Prompt(string title, string hint, string start, int max, bool code, System.Action<string> done)
        {
            if (_prompt != null) Destroy(_prompt.gameObject);
            _prompt = Ui.Img("Prompt", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _prompt.Fill(); _prompt.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _prompt, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 330));      // high on the screen: the keyboard takes the bottom half
            Ui.Label("T", box, title, 40, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(760, 56));
            var well = Ui.ArtImg("Field", box, "well", Color.white).rectTransform;
            well.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(680, 90));
            well.GetComponent<Image>().raycastTarget = true;
            var text = Ui.Label("Text", well, "", 46, Color.white); text.rectTransform.Fill(12); text.supportRichText = false; text.raycastTarget = false;
            var ph = Ui.Label("Hint", well, hint, 36, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, false); ph.rectTransform.Fill(12);
            var field = well.gameObject.AddComponent<InputField>();
            field.textComponent = text; field.placeholder = ph; field.characterLimit = max; field.text = start ?? "";
            field.lineType = InputField.LineType.SingleLine;
            if (code) { field.characterValidation = InputField.CharacterValidation.Alphanumeric; field.onValueChanged.AddListener(v => { if (v != v.ToUpper()) field.text = v.ToUpper(); }); }
            var ok = Ui.Button("Ok", box, "OK", Ui.Green, () => { string v = field.text; Destroy(_prompt.gameObject); _prompt = null; done(v); }, 38);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 26), new Vector2(300, 92));
            var no = Ui.Button("No", box, "CANCEL", Ui.Off, () => { Destroy(_prompt.gameObject); _prompt = null; }, 38);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 26), new Vector2(300, 92));
            field.ActivateInputField();
        }

        /// <summary>Signed in with Apple but not (or no longer) known to the friends service: register, quietly.</summary>
        void EnsureRegistered(System.Action then = null)
        {
            if (_friendBusy && then != null && !FriendsClient.SignedIn) { Toast("Connecting to the server, try again in a moment"); return; }
            if (!AppleAccount.SignedIn || FriendsClient.SignedIn || _friendBusy) { if (FriendsClient.SignedIn && then != null) then(); return; }
            _friendBusy = true;
            FriendsClient.Run(FriendsClient.Register(AppleAccount.UserId, _sim.DisplayName, _sim.Map.Name, _sim.Stars, _sim.State.flightsHandled, (ok, msg) =>
            {
                _friendBusy = false; _dirty = true;
                if (!ok) Toast(msg ?? "No connection"); else { _friendsFetched = -999; if (then != null) then(); }
            }));
        }

        /// <summary>Keeps what friends see of you current (every five minutes while signed in).</summary>
        void SyncProfile(bool now = false)
        {
            if (!FriendsClient.SignedIn || (!now && Time.unscaledTime - _profileSent < 300)) return;
            _profileSent = Time.unscaledTime;
            FriendsClient.Run(FriendsClient.UpdateProfile(_sim.DisplayName, _sim.Map.Name, _sim.Stars, _sim.State.flightsHandled));
        }

        void FriendsSheet()
        {
            var s = _sim;
            if (!AppleAccount.SignedIn)
            {
                Header("YOUR ACCOUNT");
                Ico("user", TintSocial).Entry("Sign in with Apple", null, "Your Crosswind account: add friends by code, compare airports, swap a daily gift. We never see your email.",
                    "SIGN IN", AppleAccount.Supported ? null : "On iPhone and iPad", () => AppleAccount.SignIn((ok, msg) => { if (ok) { Toast("Signed in"); EnsureRegistered(); } else if (msg != null) Toast(msg); _dirty = true; }), Ui.Ink + new Color(0.1f, 0.1f, 0.1f, 0));
                Ico("pen").Entry("Manager's name", s.DisplayName.ToUpper(), "What friends will see beside your airport.", "RENAME", null, AskName, Ui.Blue);
                GameCenterRows();
                return;
            }
            EnsureRegistered();
            SyncProfile();
            if (FriendsClient.SignedIn && Time.unscaledTime - _friendsFetched > 30) { _friendsFetched = Time.unscaledTime; FriendsClient.Run(FriendsClient.Refresh(list => _dirty = true)); }

            Header("YOU");
            Ico("id", TintSocial).Entry(s.DisplayName, FriendsClient.SignedIn ? "CODE  " + FriendsClient.Code : "CONNECTING...", s.Map.Name + "  ·  " + s.Stars + (s.Stars == 1 ? " star" : " stars") + "  ·  " + s.State.flightsHandled + " flights.  Give your code to a friend so they can add you.", "RENAME", null, AskName, Ui.Blue);
            Ico("userplus", TintGreen).Entry("Add a friend", null, "Type the six-letter code from their FRIENDS screen. You become friends at once, both ways.", "ADD", FriendsClient.SignedIn ? null : "Connecting", () =>
                Prompt("FRIEND'S CODE", "ABC123", "", FriendRules.CodeLength, true, v => FriendsClient.Run(FriendsClient.Add(v, (ok, msg) => { Toast(ok ? msg + " is now your friend" : msg); if (ok) Sfx.Play("confirm"); _dirty = true; }))), Ui.Green);

            var friends = FriendsClient.Friends;
            Header(friends.Count == 0 ? "NO FRIENDS YET" : "FRIENDS  ·  " + friends.Count + " OF " + FriendRules.MaxFriends + (FriendsClient.Fresh ? "" : "  ·  OFFLINE"));
            int rank = 1 + friends.Count(f => f.Stars > s.Stars || (f.Stars == s.Stars && f.Flights > s.State.flightsHandled));
            if (friends.Count > 0) Ico("trophy", TintGold).Entry("You are #" + rank + " of " + (friends.Count + 1), null, "Ranked by stars, then flights handled.", _friendManage ? "DONE" : "MANAGE", null, () => { _friendManage = !_friendManage; }, Ui.Card);
            foreach (var f in friends)
            {
                var fr = f; bool ready = s.GiftReady(f.Code);
                string body = f.Map + "  ·  " + f.Flights + " flights  ·  " + FriendRules.LastSeenText(f.LastSeen, SaveSystem.Now);
                if (_friendManage)
                    Ico("user", TintAir).Entry(f.Name, "★ " + f.Stars, body, "REMOVE", null, () => FriendChoices(fr), Ui.Red);
                else
                    Ico("user", TintAir).Entry(f.Name, "★ " + f.Stars, body, ready ? "GIFT\n<size=26>+" + Sim.FriendGiftGems + " gems</size>" : "GIFT", ready ? null : "in " + Format.Time(s.GiftWait(f.Code)),
                        () => { if (s.CollectGift(fr.Code)) { Reward(0, Sim.FriendGiftGems); Sfx.Play("claim"); } }, Ui.Gold);
            }

            GameCenterRows();
            Header("ACCOUNT");
            Ico("signout").Entry("Sign out", null, "Your airport stays on this device and in your iCloud. Your friends keep you on their list.", "SIGN OUT", null, () => { AppleAccount.SignOut(); Toast("Signed out"); }, Ui.Card);
            Ico("trash", TintRed).Entry("Delete account", null, "Erases your account, your code and every friendship from our server for good. Your airport stays on this device (Settings > Erase everything removes that too).", "DELETE", null, ConfirmDeleteAccount, Ui.Red);
        }

        /// <summary>Apple's own boards: the whole world, under the player's Game Center name.</summary>
        void GameCenterRows()
        {
            Header("GAME CENTER  ·  THE WHOLE WORLD");
            Ico("trophy", TintGold).Entry("Leaderboards", null, "Flights, Wings and grade-A days against every Crosswind manager, under your Game Center name.", "OPEN", GameCenter.Blocked, GameCenter.ShowBoards, Ui.Gold);
            Ico("star", TintGold).Entry("Achievements", null, "Every achievement you finish here is also kept on your Game Center profile.", "OPEN", GameCenter.Blocked, GameCenter.ShowAchievements, Ui.Card);
        }

        void AskName()
        {
            Prompt("MANAGER'S NAME", FriendRules.DefaultName, _sim.State.playerName, FriendRules.NameMax, false, v => { _sim.Rename(v); GameRoot.Instance.Save(); SyncProfile(true); Toast("Name: " + _sim.DisplayName); _dirty = true; });
        }

        /// <summary>Erases the account on the server. With no working token here (offline at sign-in, or replaced by a
        /// sign-in on another device) it registers again with the Apple ID first, which finds the same account.</summary>
        void DeleteAccountNow(bool retried)
        {
            System.Action again = () => EnsureRegistered(() => DeleteAccountNow(true));
            if (!FriendsClient.SignedIn) { if (retried) Toast("Couldn't reach the server: nothing was deleted. Try again."); else again(); return; }
            FriendsClient.Run(FriendsClient.DeleteAccount(ok =>
            {
                if (!ok && !FriendsClient.SignedIn && !retried) { again(); return; }
                if (!ok) { Toast("Couldn't reach the server: nothing was deleted. Try again."); return; }
                AppleAccount.SignOut(); _sim.State.gifts.Clear(); GameRoot.Instance.Save();
                Toast("Account deleted"); _dirty = true;
            }));
        }

        /// <summary>REMOVE on a friend: just remove, or block (they can't add you again), or report their name and block.</summary>
        void FriendChoices(FriendInfo fr)
        {
            if (_confirm != null) Destroy(_confirm.gameObject);
            _confirm = Ui.Img("Confirm", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _confirm.Fill(); _confirm.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _confirm, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 560));
            Ui.Label("T", box, fr.Name.ToUpperInvariant(), 52, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1100, 70));
            Ui.Label("B", box, "REMOVE ends the friendship for both of you. BLOCK also stops them adding you again. REPORT sends us their name to check, and blocks them.", 32, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1080, 200));
            System.Action close = () => { if (_confirm != null) Destroy(_confirm.gameObject); _confirm = null; };
            System.Action<bool, string> after = null;
            System.Action<string> run = what => after = (ok, msg) => { Toast(ok ? what : msg); _dirty = true; };
            var remove = Ui.Button("Remove", box, "REMOVE", Ui.Blue, () => { close(); run(fr.Name + " removed"); FriendsClient.Run(FriendsClient.Remove(fr.Code, after)); }, 38);
            ((RectTransform)remove.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-405, 150), new Vector2(330, 100));
            var block = Ui.Button("Block", box, "BLOCK", Ui.Red, () => { close(); run(fr.Name + " blocked"); FriendsClient.Run(FriendsClient.Block(fr.Code, false, after)); }, 38);
            ((RectTransform)block.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(330, 100));
            var report = Ui.Button("Report", box, "REPORT", Ui.Red, () => { close(); run("Reported and blocked. Thank you"); FriendsClient.Run(FriendsClient.Block(fr.Code, true, after)); }, 38);
            ((RectTransform)report.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(405, 150), new Vector2(330, 100));
            var keep = Ui.Button("Keep", box, "CANCEL", Ui.Off, close, 36);
            ((RectTransform)keep.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 92));
        }

        void ConfirmDeleteAccount()
        {
            if (_confirm != null) Destroy(_confirm.gameObject);
            _confirm = Ui.Img("Confirm", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _confirm.Fill(); _confirm.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _confirm, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 560));
            Ui.Label("T", box, "DELETE YOUR ACCOUNT?", 52, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(840, 70));
            Ui.Label("B", box, "Your account, your friend code and all your friendships are erased from our server. This can't be undone.\n\nYour airport itself is not part of the account: it stays on this device.", 32, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(820, 250));
            var yes = Ui.Button("Yes", box, "DELETE", Ui.Red, () =>
            {
                Destroy(_confirm.gameObject); _confirm = null;
                DeleteAccountNow(false);
            }, 40);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 40), new Vector2(380, 104));
            var no = Ui.Button("No", box, "KEEP IT", Ui.Green, () => { Destroy(_confirm.gameObject); _confirm = null; }, 40);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 40), new Vector2(380, 104));
        }
    }
}
