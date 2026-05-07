using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNotificationService
    {
        private readonly ICoreAPI api;

        public QuestNotificationService(ICoreAPI api)
        {
            this.api = api;
        }

        public void BroadcastQuestCompleted(IServerPlayer player, string questId)
        {
            if (player == null || string.IsNullOrWhiteSpace(questId)) return;

            try
            {
                string title = LocalizationUtils.GetSafe(questId + "-title");
                if (string.IsNullOrWhiteSpace(title) || string.Equals(title, questId + "-title", StringComparison.OrdinalIgnoreCase))
                {
                    title = questId;
                }

                string playerName = ChatFormatUtil.Font(player.PlayerName, "#ffd75e");
                string hoverText = BuildQuestHoverText(questId);
                string questName;
                if (string.IsNullOrWhiteSpace(hoverText))
                {
                    questName = ChatFormatUtil.Font(title, "#77ddff");
                }
                else
                {
                    questName = $"<font color=\"#77ddff\"><qhover text=\"{hoverText}\">{title}</qhover></font>";
                }
                string text = ChatFormatUtil.PrefixAlert(Lang.Get("alegacyvsquest:quest-completed-broadcast", playerName, questName));

                string discordText = LocalizationUtils.GetSafe("alegacyvsquest:discord-quest-completed", player.PlayerName, title);
                if (string.IsNullOrWhiteSpace(discordText) || string.Equals(discordText, "alegacyvsquest:discord-quest-completed", StringComparison.OrdinalIgnoreCase))
                {
                    discordText = Lang.Get("alegacyvsquest:quest-completed-broadcast", player.PlayerName, title);
                }
                else
                {
                    discordText = discordText.Replace("{0}", player.PlayerName).Replace("{1}", title);
                }

                if (api is ICoreServerAPI sapi)
                {
                    GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(sapi, text, discordText, EnumChatType.Notification, DiscordBroadcastKind.QuestCompleted);
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[QuestNotificationService] Failed to broadcast quest completion: {0}", ex.Message);
            }
        }

        public void BroadcastQuestStageCompleted(IServerPlayer player, string questId, int stage)
        {
            // Stage completion notifications can be added here if needed
            // Currently not used, but kept for future extensibility
        }

        private string BuildQuestHoverText(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;

            // Check for custom hover text first
            string hoverKey = questId + "-hover";
            string hoverText = LocalizationUtils.GetSafe(hoverKey);
            if (!string.IsNullOrWhiteSpace(hoverText)
                && !string.Equals(hoverText, hoverKey, StringComparison.OrdinalIgnoreCase))
            {
                return hoverText.Replace("\r", "").Replace("\"", "&quot;");
            }

            // If only custom hover is allowed, don't use description
            if (OnlyCustomHoverTextEnabled())
            {
                return null;
            }

            // Only use description as hover if global config allows it
            if (!ShouldShowDescriptionInHover())
            {
                return null;
            }

            string langKey = questId + "-desc";
            string desc = LocalizationUtils.GetSafe(langKey);
            if (string.IsNullOrWhiteSpace(desc) || string.Equals(desc, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string[] lines = desc.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                return line.Trim().Replace("\"", "&quot;");
            }

            return null;
        }

        private bool ShouldShowDescriptionInHover()
        {
            if (api is ICoreServerAPI sapi)
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                return questSystem?.CoreConfig?.ShowQuestDescriptionInHover ?? false;
            }

            return false;
        }

        private bool OnlyCustomHoverTextEnabled()
        {
            if (api is ICoreServerAPI sapi)
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                return questSystem?.CoreConfig?.OnlyCustomHoverText ?? false;
            }

            return false;
        }

        public bool ShouldNotifyOnComplete(Quest quest)
        {
            // If quest has explicit setting, use it
            if (quest?.notifyOnComplete.HasValue == true)
            {
                return quest.notifyOnComplete.Value;
            }

            // Otherwise use global config default
            if (api is ICoreServerAPI sapi)
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                return questSystem?.CoreConfig?.DefaultNotifyOnComplete ?? true;
            }

            return true;
        }
    }
}
