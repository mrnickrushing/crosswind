using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Crosswind.Game
{
    /// <summary>
    /// Friends, kept by the Worker in Tools/friends-worker: sign in with the opaque account id from Sign in with
    /// Apple, get a friend code, add and remove friends by code, list their airports. Every call is a coroutine:
    /// start it on any MonoBehaviour, or with <see cref="Run"/> on a hidden runner that survives scene loads.
    /// Nothing here throws; without a connection the callback gets (false, "No connection"). The server keeps only
    /// hashes of the account id and the token; this device keeps the token and code in PlayerPrefs.
    /// </summary>
    public static class FriendsClient
    {
        public const string Endpoint = "https://crosswind-friends.mrnickrushing.workers.dev";
        public const string TokenPref = "cw_friends_token", CodePref = "cw_friends_code";
        public const string NoConnection = "No connection";
        const string TryLater = "Try again later", SignInAgain = "Sign in again";
        const int Timeout = 10;

        static readonly List<FriendInfo> _friends = new List<FriendInfo>();
        static Runner _runner;

        /// <summary>True once <see cref="Register"/> has succeeded on this device (and the token still stands).</summary>
        public static bool SignedIn { get { return Token.Length > 0; } }

        /// <summary>This player's friend code ("" until signed in).</summary>
        public static string Code { get { return PlayerPrefs.GetString(CodePref, ""); } }

        /// <summary>The friends as of the last <see cref="Refresh"/>, most stars first. Never null.</summary>
        public static List<FriendInfo> Friends { get { return _friends; } }

        /// <summary>False when the last <see cref="Refresh"/> couldn't reach the server (the list shown is the old one).</summary>
        public static bool Fresh { get; private set; }

        static string Token { get { return PlayerPrefs.GetString(TokenPref, ""); } }

        /// <summary>Starts one of the calls below on a hidden object that lives across scene loads.</summary>
        public static Coroutine Run(IEnumerator call)
        {
            if (call == null) return null;
            if (_runner == null)
            {
                var go = new GameObject("FriendsClient") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner.StartCoroutine(call);
        }

        /// <summary>
        /// Signs in: creates the player or, when the account exists, gets its code back with a new token (a token
        /// held by another device stops working; that device registers again). done(true, code) or done(false, why).
        /// </summary>
        public static IEnumerator Register(string account, string name, string map, int stars, int flights, Action<bool, string> done)
        {
            if (string.IsNullOrEmpty(account)) { Tell(done, false, SignInAgain); yield break; }
            var body = new RegisterBody { account = account, name = FriendRules.CleanName(name), map = map ?? "", stars = Mathf.Max(0, stars), flights = Mathf.Max(0, flights) };
            Reply reply = null; string error = null;
            yield return Send("POST", "/v1/register", JsonUtility.ToJson(body), false, (r, e) => { reply = r; error = e; });
            if (error == null && (reply == null || string.IsNullOrEmpty(reply.token) || string.IsNullOrEmpty(reply.code))) error = TryLater;
            if (error != null) { Tell(done, false, error); yield break; }
            PlayerPrefs.SetString(TokenPref, reply.token);
            PlayerPrefs.SetString(CodePref, reply.code);
            PlayerPrefs.Save();
            Tell(done, true, reply.code);
        }

        /// <summary>Sends what friends see of this airport, and marks the player as seen now.</summary>
        public static IEnumerator UpdateProfile(string name, string map, int stars, int flights, Action<bool, string> done = null)
        {
            if (!SignedIn) { Tell(done, false, SignInAgain); yield break; }
            var body = new ProfileBody { name = FriendRules.CleanName(name), map = map ?? "", stars = Mathf.Max(0, stars), flights = Mathf.Max(0, flights) };
            string error = null;
            yield return Send("POST", "/v1/profile", JsonUtility.ToJson(body), true, (r, e) => { error = e; });
            Tell(done, error == null, error ?? "");
        }

        /// <summary>Adds the airport with this code; both players become friends at once. done(true, their name) or
        /// done(false, why): "No airport has that code", "That's your own code", "Already friends", "Friend list is full".</summary>
        public static IEnumerator Add(string code, Action<bool, string> done)
        {
            if (!SignedIn) { Tell(done, false, SignInAgain); yield break; }
            string valid = FriendRules.ValidCode(code);
            if (valid == null) { Tell(done, false, "Codes are " + FriendRules.CodeLength + " letters and numbers"); yield break; }
            if (valid == Code) { Tell(done, false, "That's your own code"); yield break; }
            Reply reply = null; string error = null;
            yield return Send("POST", "/v1/friends/add", JsonUtility.ToJson(new CodeBody { code = valid }), true, (r, e) => { reply = r; error = e; });
            if (error != null) { Tell(done, false, error); yield break; }
            string name = "";
            if (reply != null && reply.friend != null && !string.IsNullOrEmpty(reply.friend.code))
            {
                var f = reply.friend.ToInfo();
                name = f.Name;
                _friends.RemoveAll(x => x.Code == f.Code);
                _friends.Add(f);
                Sort();
            }
            Tell(done, true, name);
        }

        /// <summary>Ends the friendship for both players.</summary>
        public static IEnumerator Remove(string code, Action<bool, string> done)
        {
            if (!SignedIn) { Tell(done, false, SignInAgain); yield break; }
            string valid = FriendRules.ValidCode(code);
            if (valid == null) { Tell(done, false, "No airport has that code"); yield break; }
            string error = null;
            yield return Send("POST", "/v1/friends/remove", JsonUtility.ToJson(new CodeBody { code = valid }), true, (r, e) => { error = e; });
            if (error != null) { Tell(done, false, error); yield break; }
            _friends.RemoveAll(x => x.Code == valid);
            Tell(done, true, "");
        }

        /// <summary>Ends the friendship and stops that player adding this one again; with <paramref name="report"/> the
        /// developer is also sent their name to look at.</summary>
        public static IEnumerator Block(string code, bool report, Action<bool, string> done)
        {
            if (!SignedIn) { Tell(done, false, SignInAgain); yield break; }
            string valid = FriendRules.ValidCode(code);
            if (valid == null) { Tell(done, false, "No airport has that code"); yield break; }
            string error = null;
            yield return Send("POST", report ? "/v1/friends/report" : "/v1/friends/block", JsonUtility.ToJson(new CodeBody { code = valid }), true, (r, e) => { error = e; });
            if (error != null) { Tell(done, false, error); yield break; }
            _friends.RemoveAll(x => x.Code == valid);
            Tell(done, true, "");
        }

        /// <summary>Fetches the friend list into <see cref="Friends"/> and hands it over. When the server can't be
        /// reached the callback gets the list as it was (<see cref="Fresh"/> says which).</summary>
        public static IEnumerator Refresh(Action<List<FriendInfo>> done)
        {
            if (!SignedIn) { Fresh = false; _friends.Clear(); Tell(done); yield break; }
            Reply reply = null; string error = null;
            yield return Send("GET", "/v1/friends", null, true, (r, e) => { reply = r; error = e; });
            if (error != null) { Fresh = false; if (!SignedIn) _friends.Clear(); Tell(done); yield break; }
            _friends.Clear();
            if (reply != null && reply.friends != null)
                foreach (var f in reply.friends)
                    if (f != null && !string.IsNullOrEmpty(f.code)) _friends.Add(f.ToInfo());
            Sort();
            Fresh = true;
            Tell(done);
        }

        /// <summary>Erases the player and every friendship on the server, then forgets the token and code here.
        /// done(false) when nothing was erased: the server couldn't be reached, or this device holds no working token
        /// (never registered, or replaced by a sign-in elsewhere). The caller registers again and retries then, so
        /// "Account deleted" is only ever said when the server has really erased it.</summary>
        public static IEnumerator DeleteAccount(Action<bool> done)
        {
            if (!SignedIn) { Tell(done, false); yield break; }
            string error = null;
            yield return Send("POST", "/v1/delete", "{}", true, (r, e) => { error = e; });
            if (error == null) Forget();
            Tell(done, error == null);
        }

        /// <summary>Forgets the token, code and cached friends on this device only (signing out).</summary>
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(TokenPref);
            PlayerPrefs.DeleteKey(CodePref);
            PlayerPrefs.Save();
            _friends.Clear();
            Fresh = false;
        }

        // ---- the wire ----

        /// <summary>One request. then(reply, null) on success, then(null, message) otherwise. A 401 drops the token:
        /// it was replaced by a sign-in elsewhere or the account was deleted, so the game should register again.</summary>
        static IEnumerator Send(string method, string path, string json, bool auth, Action<Reply, string> then)
        {
            string token = Token;
            if (auth && token.Length == 0) { then(null, SignInAgain); yield break; }

            UnityWebRequest req = null;
            try
            {
                req = new UnityWebRequest(Endpoint + path, method);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (auth) req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = Timeout;
            }
            catch (Exception)
            {
                if (req != null) req.Dispose();
                req = null;
            }
            if (req == null) { then(null, NoConnection); yield break; }

            using (req)
            {
                yield return req.SendWebRequest();

                Reply reply = null;
                string text = null;
                try { text = req.downloadHandler != null ? req.downloadHandler.text : null; } catch (Exception) { }
                if (!string.IsNullOrEmpty(text) && text[0] == '{')
                {
                    try { reply = JsonUtility.FromJson<Reply>(text); } catch (Exception) { reply = null; }
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (req.result != UnityWebRequest.Result.ProtocolError) { then(null, NoConnection); yield break; }
                    if (auth && req.responseCode == 401)
                    {
                        PlayerPrefs.DeleteKey(TokenPref);
                        PlayerPrefs.Save();
                        then(null, SignInAgain);
                        yield break;
                    }
                    then(null, reply != null && !string.IsNullOrEmpty(reply.error) ? reply.error : TryLater);
                    yield break;
                }
                if (reply == null) { then(null, TryLater); yield break; }
                then(reply, null);
            }
        }

        static void Sort()
        {
            _friends.Sort((a, b) =>
            {
                int c = b.Stars.CompareTo(a.Stars);
                if (c == 0) c = b.Flights.CompareTo(a.Flights);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        // A callback that throws must not take the coroutine (or the caller's frame) down with it.
        static void Tell(Action<bool, string> done, bool ok, string message)
        {
            if (done == null) return;
            try { done(ok, message); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Tell(Action<bool> done, bool ok)
        {
            if (done == null) return;
            try { done(ok); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Tell(Action<List<FriendInfo>> done)
        {
            if (done == null) return;
            try { done(_friends); } catch (Exception e) { Debug.LogException(e); }
        }

        sealed class Runner : MonoBehaviour { }

        // ---- JSON shapes (JsonUtility: public fields named as on the wire) ----
#pragma warning disable 0649   // filled in by JsonUtility

        [Serializable] sealed class RegisterBody { public string account; public string name; public string map; public int stars; public int flights; }
        [Serializable] sealed class ProfileBody { public string name; public string map; public int stars; public int flights; }
        [Serializable] sealed class CodeBody { public string code; }

        [Serializable]
        sealed class FriendDto
        {
            public string code; public string name; public string map; public int stars; public int flights; public double lastSeen;

            public FriendInfo ToInfo()
            {
                return new FriendInfo { Code = code, Name = FriendRules.CleanName(name), Map = map ?? "", Stars = stars, Flights = flights, LastSeen = lastSeen };
            }
        }

        [Serializable]
        sealed class Reply
        {
            public bool ok;
            public string error;
            public string code;
            public string token;
            public FriendDto friend;
            public FriendDto[] friends;
        }
#pragma warning restore 0649
    }
}
