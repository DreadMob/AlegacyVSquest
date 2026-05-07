using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class BossKillAnnouncementUtil
    {
        private static readonly string[] TemplateLangKeys = new[]
        {
            "alegacyvsquest:bosskill-template-1",
            "alegacyvsquest:bosskill-template-2",
            "alegacyvsquest:bosskill-template-3",
            "alegacyvsquest:bosskill-template-4",
            "alegacyvsquest:bosskill-template-5",
            "alegacyvsquest:bosskill-template-6",
            "alegacyvsquest:bosskill-template-7",
            "alegacyvsquest:bosskill-template-8",
            "alegacyvsquest:bosskill-template-9",
            "alegacyvsquest:bosskill-template-10"
        };

        public static void AnnouncePlayerKilledByBoss(ICoreServerAPI sapi, IServerPlayer victim, Entity killerBossEntity)
        {
            if (sapi == null || victim == null || killerBossEntity == null) return;

            string bossName = MobLocalizationUtils.GetMobDisplayName(killerBossEntity);
            if (string.IsNullOrWhiteSpace(bossName)) bossName = killerBossEntity.Code?.ToShortString() ?? "?";

            string victimName = ChatFormatUtil.Font(victim.PlayerName, "#ffd75e");
            string bossNameColored = ChatFormatUtil.Font(bossName, "#ff77ff");

            string template = LocalizationUtils.GetSafe("alegacyvsquest:bosskill-default-template");
            if (TemplateLangKeys.Length > 0)
            {
                string langKey = TemplateLangKeys[sapi.World.Rand.Next(0, TemplateLangKeys.Length)];
                try
                {
                    string localized = Lang.Get(langKey);
                    if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, langKey, StringComparison.OrdinalIgnoreCase))
                    {
                        template = localized;
                    }
                }
                catch (Exception)
                {
                    // Swallow - Lang.Get may fail for missing keys
                }
            }

            string message = template
                .Replace("{victim}", victimName)
                .Replace("{boss}", bossNameColored)
                .Replace("{{victim}}", victimName)
                .Replace("{{boss}}", bossNameColored);

            string discordTemplate = LocalizationUtils.GetSafe("alegacyvsquest:discord-bosskill-default-template");
            if (string.IsNullOrWhiteSpace(discordTemplate) || string.Equals(discordTemplate, "alegacyvsquest:discord-bosskill-default-template", StringComparison.OrdinalIgnoreCase))
            {
                discordTemplate = template;
            }
            string discordMessage = discordTemplate
                .Replace("{victim}", victim.PlayerName)
                .Replace("{boss}", bossName)
                .Replace("{{victim}}", victim.PlayerName)
                .Replace("{{boss}}", bossName);
            
            GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                sapi,
                ChatFormatUtil.PrefixAlert(message),
                discordMessage,
                Vintagestory.API.Common.EnumChatType.Notification,
                DiscordBroadcastKind.BossDefeated);
        }

        public static void AnnounceBossDefeated(ICoreServerAPI sapi, IServerPlayer killer, Entity bossEntity)
        {
            if (sapi == null || killer == null || bossEntity == null) return;

            string bossName = MobLocalizationUtils.GetMobDisplayName(bossEntity);
            if (string.IsNullOrWhiteSpace(bossName)) bossName = bossEntity.Code?.ToShortString() ?? "?";

            string playerName = ChatFormatUtil.Font(killer.PlayerName, "#ffd75e");
            string bossNameColored = ChatFormatUtil.Font(bossName, "#ff77ff");
            string text = ChatFormatUtil.PrefixAlert(Lang.Get("alegacyvsquest:boss-defeated", playerName, bossNameColored));

            string discordText = LocalizationUtils.GetSafe("alegacyvsquest:discord-boss-defeated");
            if (string.IsNullOrWhiteSpace(discordText) || string.Equals(discordText, "alegacyvsquest:discord-boss-defeated", StringComparison.OrdinalIgnoreCase))
            {
                discordText = Lang.Get("alegacyvsquest:boss-defeated", killer.PlayerName, bossName);
            }
            else
            {
                discordText = discordText.Replace("{0}", killer.PlayerName).Replace("{1}", bossName);
            }
            GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                sapi,
                text,
                discordText,
                Vintagestory.API.Common.EnumChatType.Notification,
                DiscordBroadcastKind.BossDefeated);
        }

        public static void AnnounceBossDefeated(ICoreServerAPI sapi, IReadOnlyList<IServerPlayer> killers, Entity bossEntity)
        {
            if (sapi == null || bossEntity == null || killers == null || killers.Count == 0) return;

            string bossName = MobLocalizationUtils.GetMobDisplayName(bossEntity);
            if (string.IsNullOrWhiteSpace(bossName)) bossName = bossEntity.Code?.ToShortString() ?? "?";

            string bossNameColored = ChatFormatUtil.Font(bossName, "#ff77ff");
            string playerNames = string.Join(", ", killers
                .Where(player => player != null)
                .Select(player => ChatFormatUtil.Font(player.PlayerName, "#ffd75e")));

            if (string.IsNullOrWhiteSpace(playerNames)) return;

            string langKey = killers.Count > 1 ? "alegacyvsquest:boss-defeated-multi" : "alegacyvsquest:boss-defeated";
            string text = ChatFormatUtil.PrefixAlert(Lang.Get(langKey, playerNames, bossNameColored));

            // Create Discord message using the same localization key
            string playerList = string.Join(", ", killers.Where(p => p != null).Select(p => p.PlayerName));
            string discordMultiTemplate = LocalizationUtils.GetSafe("alegacyvsquest:discord-boss-defeated-multi");
            string discordText;
            if (string.IsNullOrWhiteSpace(discordMultiTemplate) || string.Equals(discordMultiTemplate, "alegacyvsquest:discord-boss-defeated-multi", StringComparison.OrdinalIgnoreCase))
            {
                discordText = Lang.Get(langKey, playerList, bossName);
            }
            else
            {
                discordText = discordMultiTemplate.Replace("{0}", playerList).Replace("{1}", bossName);
            }
            GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                sapi,
                text,
                discordText,
                Vintagestory.API.Common.EnumChatType.Notification,
                DiscordBroadcastKind.BossDefeated);
        }
    }
}
