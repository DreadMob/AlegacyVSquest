using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddJournalEntryQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "addjournalentry";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            // Supported formats:
            //   addjournalentry <groupId> <loreCode> <title> [overwrite] <chapter...>
            //   addjournalentry <loreCode> <title> [overwrite] <chapter...>
            //   addjournalentry <loreCode> <chapter...>

            string groupId, loreCode, title;
            List<string> chapters;
            int argIndex;

            if (args.Length >= 3 && args[1].Contains(':'))
            {
                // New format: groupId, loreCode, title, [overwrite], chapters...
                groupId = args[0];
                loreCode = args[1];
                title = args[2];
                argIndex = 3;
            }
            else if (args.Length >= 3 && !args[1].Contains(':'))
            {
                // Legacy: loreCode, title, chapters...
                groupId = loreCode = args[0];
                title = args[1];
                argIndex = 2;
            }
            else
            {
                // Minimal: loreCode, chapters...
                groupId = loreCode = args[0];
                title = loreCode;
                argIndex = 1;
            }

            // Check for "overwrite" flag
            bool overwrite = false;
            if (argIndex < args.Length && string.Equals(args[argIndex], "overwrite", StringComparison.OrdinalIgnoreCase))
            {
                overwrite = true;
                argIndex++;
            }

            chapters = new List<string>();
            for (int i = argIndex; i < args.Length; i++)
            {
                string text = args[i];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Resolve localization key if it looks like one
                    string resolved = LocalizationUtils.GetSafe(text);
                    if (!string.IsNullOrWhiteSpace(resolved) && !string.Equals(resolved, text, StringComparison.OrdinalIgnoreCase))
                    {
                        chapters.Add(resolved);
                    }
                    else
                    {
                        chapters.Add(text);
                    }
                }
            }

            if (chapters.Count == 0) return;

            // Resolve title if it's a language key
            string resolvedTitle = LocalizationUtils.GetSafe(title);
            if (!string.IsNullOrWhiteSpace(resolvedTitle) && !string.Equals(resolvedTitle, title, StringComparison.OrdinalIgnoreCase))
            {
                title = resolvedTitle;
            }

            var wa = player.Entity.WatchedAttributes;
            var entries = QuestJournalEntry.Load(wa);

            // Find existing entry by loreCode for this group
            var existing = entries.FirstOrDefault(e =>
                string.Equals(e.LoreCode, loreCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.QuestId, groupId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (overwrite)
                {
                    existing.Title = title;
                    existing.Chapters = new List<string>(chapters);
                }
                else
                {
                    existing.Title = title;
                    foreach (var ch in chapters)
                    {
                        if (!existing.Chapters.Contains(ch, StringComparer.Ordinal))
                        {
                            existing.Chapters.Add(ch);
                        }
                    }
                }
            }
            else
            {
                entries.Add(new QuestJournalEntry
                {
                    QuestId = groupId,
                    LoreCode = loreCode,
                    Title = title,
                    Chapters = new List<string>(chapters)
                });
            }

            QuestJournalEntry.Save(wa, entries);
            wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("alegacyvsquest:journal-updated"), EnumChatType.Notification);

            try
            {
                sapi.World.PlaySoundFor(new AssetLocation("sounds/effect/writing"), player);
            }
            catch (Exception e)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Could not play sound 'sounds/effect/writing' for journal update: {e.Message}");
            }
        }
    }
}
