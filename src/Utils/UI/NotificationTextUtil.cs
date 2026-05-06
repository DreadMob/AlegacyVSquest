using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class NotificationTextUtil
    {
        private static string TryBuildNotificationFromTemplate(ShowNotificationMessage message, ILogger logger)
        {
            if (message == null) return null;
            if (string.IsNullOrWhiteSpace(message.Template) || string.IsNullOrWhiteSpace(message.MobCode)) return null;

            string mobName = MobLocalizationUtils.GetMobDisplayName(message.MobCode);
            try
            {
                string translated = LocalizationUtils.GetSafe(message.Template, message.Need, mobName);
                if (translated == message.Template)
                {
                    return $"{message.Template} ({message.Need}, {mobName})";
                }
                return translated;
            }
            catch (Exception e)
            {
                logger.Warning($"[alegacyvsquest] Could not format notification template '{message.Template}': {e.Message}");
                return message.Template;
            }
        }

        private static string TryBuildNotificationFromLegacyText(ShowNotificationMessage message)
        {
            if (message == null) return null;
            if (string.IsNullOrEmpty(message.Notification)) return message.Notification;

            if (message.Need != 0)
            {
                return LocalizationUtils.GetSafe(message.Notification, message.Need);
            }

            return LocalizationUtils.GetSafe(message.Notification);
        }

        public static string Build(ShowNotificationMessage message, ILogger logger)
        {
            string text = TryBuildNotificationFromTemplate(message, logger);
            if (string.IsNullOrEmpty(text))
            {
                text = TryBuildNotificationFromLegacyText(message);
            }

            return text;
        }
    }
}
