#if UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace Crosswind.EditorTools
{
    /// <summary>
    /// The app's capabilities in the exported Xcode project: iCloud key-value storage (the save), in
    /// Crosswind.entitlements with the team and bundle written out (an ad-hoc signed archive doesn't expand
    /// $(TeamIdentifierPrefix)). The bundle id needs iCloud enabled on App Store Connect before a signed build.
    /// Reminders are local notifications and need no capability.
    /// </summary>
    public sealed class IosCapabilities : IPreprocessBuildWithReport
    {
        public const string Entitlements = "Crosswind.entitlements";
        public int callbackOrder { get { return 0; } }

        /// <summary>The game asks for notification permission after the first long build, not at launch.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            Unity.Notifications.NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
            Unity.Notifications.NotificationSettings.iOSSettings.AddRemoteNotificationCapability = false;
        }

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string team = PlayerSettings.iOS.appleDeveloperTeamID;
            string bundle = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            var ent = new PlistDocument();
            ent.root.CreateArray("com.apple.developer.applesignin").AddString("Default");
            ent.root.SetString("com.apple.developer.ubiquity-kvstore-identifier", team + "." + bundle);
            ent.WriteToFile(Path.Combine(path, Entitlements));

            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFile(Entitlements, Entitlements);
            proj.SetBuildProperty(proj.GetUnityMainTargetGuid(), "CODE_SIGN_ENTITLEMENTS", Entitlements);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", true);     // Sign in with Apple
            proj.WriteToFile(projPath);
        }
    }
}
#endif
