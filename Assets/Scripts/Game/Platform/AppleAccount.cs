using System;
using Crosswind.Core;
using UnityEngine;
#if UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

namespace Crosswind.Game
{
    /// <summary>
    /// The player's account: Sign in with Apple (optional). Signing in creates the account the friends service knows
    /// (only a hash of Apple's opaque user id ever leaves the phone), and names the manager from the first name Apple
    /// sends if none was chosen. The airport itself travels through iCloud (<see cref="CloudSave"/>). In the Editor a
    /// stand-in account lets the whole flow be played without an iPhone.
    /// </summary>
    public static class AppleAccount
    {
        const string UserKey = "apple.user";
        public static string UserId { get { return PlayerPrefs.GetString(UserKey, ""); } }
        public static bool SignedIn { get { return UserId.Length > 0; } }
        public static event Action Changed;

#if UNITY_IOS
        static IAppleAuthManager _auth;
        public static bool Supported { get { return Application.isEditor || (Application.platform == RuntimePlatform.IPhonePlayer && AppleAuthManager.IsCurrentPlatformSupported); } }

        /// <summary>Call once at startup: checks a saved sign-in is still valid.</summary>
        public static void Start()
        {
            if (Application.isEditor || !AppleAuthManager.IsCurrentPlatformSupported) return;
            _auth = new AppleAuthManager(new PayloadDeserializer());
            if (!SignedIn) return;
            _auth.GetCredentialState(UserId, state => { if (state != CredentialState.Authorized) SignOut(); }, error => { });   // offline: keep the saved sign-in
        }

        /// <summary>Pumps the native callbacks (call every frame).</summary>
        public static void Update() { if (_auth != null) _auth.Update(); }

        public static void SignIn(Action<bool, string> done)
        {
            if (Application.isEditor) { EditorSignIn(done); return; }
            if (_auth == null) { done(false, "Sign in with Apple works on iPhone and iPad."); return; }
            _auth.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.IncludeFullName), credential =>
            {
                var apple = credential as IAppleIDCredential;
                PlayerPrefs.SetString(UserKey, credential.User);
                PlayerPrefs.Save();
                // Apple sends the name only on the very first sign-in.
                if (apple != null && apple.FullName != null && !string.IsNullOrEmpty(apple.FullName.GivenName)
                    && GameRoot.Sim != null && string.IsNullOrEmpty(GameRoot.Sim.State.playerName))
                    GameRoot.Sim.Rename(apple.FullName.GivenName);
                Changed?.Invoke();
                done(true, null);
            }, error =>
            {
                var cancelled = error.GetAuthorizationErrorCode() == AuthorizationErrorCode.Canceled;
                done(false, cancelled ? null : "Sign in with Apple failed. Please try again.");
            });
        }
#else
        public static bool Supported { get { return Application.isEditor; } }
        public static void Start() { }
        public static void Update() { }
        public static void SignIn(Action<bool, string> done) { if (Application.isEditor) EditorSignIn(done); else done(false, "Sign in with Apple works on iPhone and iPad."); }
#endif

        static void EditorSignIn(Action<bool, string> done)
        {
            PlayerPrefs.SetString(UserKey, "editor-" + SystemInfo.deviceUniqueIdentifier);
            PlayerPrefs.Save();
            Changed?.Invoke();
            done(true, null);
        }

        public static void SignOut()
        {
            PlayerPrefs.DeleteKey(UserKey);
            PlayerPrefs.Save();
            FriendsClient.Forget();
            Changed?.Invoke();
        }
    }
}
