﻿using Android.Content;

namespace Muazun.Droid.Services
{
    [BroadcastReceiver(Enabled = true, Label = "Local Notifications Broadcast Receiver")]
    public class AlarmHandler : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Extras != null)
            {
                string title = intent.GetStringExtra(AndroidNotificationService.TitleKey);
                string message = intent.GetStringExtra(AndroidNotificationService.MessageKey);
                bool isFajr = message.Contains("Fajr");

                AndroidNotificationService notificationService = AndroidNotificationService.Instance ?? new AndroidNotificationService();
                notificationService.Show(title, message, isFajr);
            }
        }
    }
}
