using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class AddVanillaJournalEntryQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 2;
        protected override string ActionName => "addvanillajournalentry";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Supported format:
            // - Legacy: addvanillajournalentry <loreCode> <chapter...>
            string loreCode = args[0];
            string title = loreCode;
            int chapterStartIndex = 1;

            var incomingChapters = new List<JournalChapter>();
            for (int i = chapterStartIndex; i < args.Length; i++)
            {
                var txt = LocalizationUtils.GetSafe(args[i]);
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    incomingChapters.Add(new JournalChapter() { Text = txt });
                }
            }

            if (incomingChapters.Count == 0) return;
            if (player?.Entity?.WatchedAttributes == null) return;

            var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            if (modJournal == null) return;

            var t = modJournal.GetType();
            var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
            var channelField = t.GetField("serverChannel", BindingFlags.Instance | BindingFlags.NonPublic);

            Dictionary<string, Journal> journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
            IServerNetworkChannel serverChannel = channelField?.GetValue(modJournal) as IServerNetworkChannel;

            if (journals == null || serverChannel == null)
            {
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                if (journals == null)
                {
                    var jf = fields.FirstOrDefault(f => typeof(Dictionary<string, Journal>).IsAssignableFrom(f.FieldType));
                    journals = jf?.GetValue(modJournal) as Dictionary<string, Journal>;
                }

                if (serverChannel == null)
                {
                    var cf = fields.FirstOrDefault(f => typeof(IServerNetworkChannel).IsAssignableFrom(f.FieldType));
                    serverChannel = cf?.GetValue(modJournal) as IServerNetworkChannel;
                }
            }

            if (journals == null || serverChannel == null) return;
            if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return;

            JournalEntry entry = journal.Entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, loreCode, StringComparison.OrdinalIgnoreCase));
            bool changed = false;

            if (entry == null)
            {
                entry = new JournalEntry
                {
                    EntryId = journal.Entries.Count,
                    LoreCode = loreCode,
                    Title = title,
                    Chapters = new List<JournalChapter>()
                };
                journal.Entries.Add(entry);
                changed = true;
            }
            else
            {
                int entryIndex = journal.Entries.IndexOf(entry);
                if (entryIndex >= 0 && entry.EntryId != entryIndex)
                {
                    entry.EntryId = entryIndex;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(title) && !string.Equals(entry.Title, title, StringComparison.Ordinal))
            {
                entry.Title = title;
                changed = true;
            }

            if (entry.Chapters == null)
            {
                entry.Chapters = new List<JournalChapter>();
                changed = true;
            }

            var existingTexts = new HashSet<string>(entry.Chapters.Where(c => !string.IsNullOrWhiteSpace(c?.Text)).Select(c => c.Text), StringComparer.Ordinal);
            foreach (var ch in incomingChapters)
            {
                if (ch == null || string.IsNullOrWhiteSpace(ch.Text)) continue;
                if (existingTexts.Contains(ch.Text)) continue;

                ch.EntryId = entry.EntryId;
                entry.Chapters.Add(ch);
                existingTexts.Add(ch.Text);
                changed = true;
            }

            if (!changed) return;

            serverChannel.SendPacket(journal, player);
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
