using System;
using Crosswind.Core;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Crosswind.Game
{
    /// <summary>
    /// Local reminder notifications (<see cref="Sim.ReminderPlan"/>), scheduled when the game goes to the background
    /// and cleared when it comes back. iOS is asked for permission after the first build that takes time ("we'll
    /// tell you when it's done"), never at launch. Settings can turn them off.
    /// </summary>
    public static class Reminders
    {
        const string AskedKey = "notif.asked", OffKey = "notif.off";

        public static bool On
        {
            get { return PlayerPrefs.GetInt(OffKey, 0) == 0; }
            set { PlayerPrefs.SetInt(OffKey, value ? 0 : 1); PlayerPrefs.Save(); if (!value) Clear(); else AskOnce(); }
        }

        public static void AskOnce()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
            GameRoot.Instance.StartCoroutine(Ask());
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static System.Collections.IEnumerator Ask()
        {
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
                while (!req.IsFinished) yield return null;
        }
#endif

        /// <summary>Clears pending and delivered reminders and the badge (launch and resume).</summary>
        public static void Clear()
        {
#if UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }

        /// <summary>Schedules the plan (going to the background).</summary>
        public static void Schedule()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Clear();
            if (GameRoot.Sim == null || !On) return;
            if (iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus != AuthorizationStatus.Authorized) return;
            int i = 0;
            foreach (var r in GameRoot.Sim.ReminderPlan())
            {
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = "crosswind." + (i++),
                    Title = r.Title,
                    Body = r.Body,
                    ShowInForeground = false,
                    ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                    Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(Math.Max(1, r.In)), Repeats = false },
                });
            }
#endif
        }
    }
}
