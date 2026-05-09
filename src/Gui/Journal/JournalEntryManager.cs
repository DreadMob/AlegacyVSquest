using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class JournalEntryManager
    {
        private readonly ICoreClientAPI capi;
        private IClientPlayer player => capi.World.Player;

        public List<QuestJournalEntry> AllEntries { get; private set; } = new List<QuestJournalEntry>();
        public List<JournalPage> AllPages { get; } = new List<JournalPage>();
        public List<QuestGiverPage> NoteGiverPages { get; } = new List<QuestGiverPage>();
        public List<QuestPage> NotePages { get; } = new List<QuestPage>();
        
        public Dictionary<string, QuestPage> QuestPagesByLoreCode { get; } = new Dictionary<string, QuestPage>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, QuestPage> NotePagesByLoreCode { get; } = new Dictionary<string, QuestPage>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> PageNumberByPageCode { get; } = new Dictionary<string, int>();

        public JournalEntryManager(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void LoadEntries()
        {
            AllPages.Clear();
            PageNumberByPageCode.Clear();
            NoteGiverPages.Clear();
            NotePages.Clear();
            QuestPagesByLoreCode.Clear();
            NotePagesByLoreCode.Clear();

            if (player?.Entity?.WatchedAttributes == null)
            {
                AllEntries = new List<QuestJournalEntry>();
                return;
            }

            AllEntries = QuestJournalEntry.Load(player.Entity.WatchedAttributes)
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.QuestId))
                .ToList();

            bool entriesChanged = ProcessEntries(AllEntries);

            if (entriesChanged)
            {
                QuestJournalEntry.Save(player.Entity.WatchedAttributes, AllEntries);
                player.Entity.WatchedAttributes.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
            }

            var questEntries = AllEntries.Where(e => !e.IsNote).ToList();
            var noteEntries = AllEntries.Where(e => e.IsNote).ToList();

            BuildQuestGiverPages(questEntries);
            BuildQuestPages(questEntries);
            BuildNoteGiverPages(noteEntries);
            BuildNotePages(noteEntries);

            for (int i = 0; i < AllPages.Count; i++)
            {
                AllPages[i].PageNumber = i;
                PageNumberByPageCode[AllPages[i].PageCode] = i;
            }
        }

        private bool ProcessEntries(List<QuestJournalEntry> entries)
        {
            bool entriesChanged = false;
            var questSystem = capi?.ModLoader?.GetModSystem<QuestSystem>();
            
            foreach (var entry in entries)
            {
                if (entry == null) continue;

                string originalTitle = entry.Title;
                bool titleWasNote = string.Equals(originalTitle, "note", StringComparison.OrdinalIgnoreCase);
                bool titleWasQuest = string.Equals(originalTitle, "quest", StringComparison.OrdinalIgnoreCase);
                bool titleWasOverwrite = string.Equals(originalTitle, "overwrite", StringComparison.OrdinalIgnoreCase);

                if ((titleWasNote || titleWasQuest || titleWasOverwrite)
                    && entry.Chapters != null
                    && entry.Chapters.Count > 0
                    && !string.IsNullOrWhiteSpace(entry.Chapters[0]))
                {
                    string candidateTitle = entry.Chapters[0];
                    if (candidateTitle.Length <= 120 && !candidateTitle.Contains("\n"))
                    {
                        entry.Title = candidateTitle;
                        entriesChanged = true;
                        if (entry.Chapters.Count > 1)
                        {
                            entry.Chapters.RemoveAt(0);
                            entriesChanged = true;
                        }
                    }

                    if (titleWasNote && !entry.IsNote)
                    {
                        entry.IsNote = true;
                        entriesChanged = true;
                    }
                    else if (titleWasQuest && entry.IsNote)
                    {
                        entry.IsNote = false;
                        entriesChanged = true;
                    }
                }

                if (entry.IsNote && questSystem?.QuestRegistry != null)
                {
                    bool hasQuestById = !string.IsNullOrWhiteSpace(entry.QuestId)
                        && questSystem.QuestRegistry.ContainsKey(entry.QuestId);
                    bool hasQuestByLore = !string.IsNullOrWhiteSpace(entry.LoreCode)
                        && questSystem.QuestRegistry.ContainsKey(entry.LoreCode);
                    if (hasQuestById || hasQuestByLore)
                    {
                        entry.IsNote = false;
                        entriesChanged = true;
                    }
                }
            }
            return entriesChanged;
        }

        private void BuildQuestGiverPages(List<QuestJournalEntry> entries)
        {
            var questGivers = entries
                .GroupBy(e => e.QuestId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { QuestId = g.Key, Count = g.Count() })
                .OrderBy(g => g.QuestId, StringComparer.OrdinalIgnoreCase);

            foreach (var giver in questGivers)
            {
                string title = GetQuestGiverTitle(giver.QuestId);
                AllPages.Add(new QuestGiverPage(capi, giver.QuestId, title, giver.Count));
            }
        }

        private void BuildQuestPages(List<QuestJournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                string entryTitle = GetEntryTitle(entry);
                var page = new QuestPage(capi, entry.QuestId, entry.LoreCode, entryTitle);
                AllPages.Add(page);
                if (!string.IsNullOrWhiteSpace(entry?.LoreCode))
                {
                    QuestPagesByLoreCode[entry.LoreCode] = page;
                }
            }
        }

        private void BuildNoteGiverPages(List<QuestJournalEntry> entries)
        {
            var noteGivers = entries
                .GroupBy(e => e.QuestId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { QuestId = g.Key, Count = g.Count() })
                .OrderBy(g => g.QuestId, StringComparer.OrdinalIgnoreCase);

            foreach (var giver in noteGivers)
            {
                string title = GetQuestGiverTitle(giver.QuestId);
                NoteGiverPages.Add(new QuestGiverPage(capi, giver.QuestId, title, giver.Count));
            }
        }

        private void BuildNotePages(List<QuestJournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                string entryTitle = GetEntryTitle(entry);
                var page = new QuestPage(capi, entry.QuestId, entry.LoreCode, entryTitle);
                NotePages.Add(page);
                if (!string.IsNullOrWhiteSpace(entry?.LoreCode))
                {
                    NotePagesByLoreCode[entry.LoreCode] = page;
                }
            }
        }

        public string GetQuestGiverTitle(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return "";
            string title = LocalizationUtils.GetSafe(questId + "-title");
            if (title == questId + "-title")
            {
                int colonIndex = questId.LastIndexOf(':');
                if (colonIndex > 0 && colonIndex < questId.Length - 1)
                {
                    return questId.Substring(colonIndex + 1);
                }
                return questId;
            }
            return title;
        }

        public string GetEntryTitle(QuestJournalEntry entry)
        {
            if (entry == null) return "";
            if (!string.IsNullOrWhiteSpace(entry.Title)) return entry.Title;
            return entry.LoreCode ?? "";
        }

        public QuestPage GetEntryPageForEntry(QuestJournalEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.LoreCode)) return null;

            if (entry.IsNote && NotePagesByLoreCode.TryGetValue(entry.LoreCode, out var notePage))
            {
                return notePage;
            }

            if (QuestPagesByLoreCode.TryGetValue(entry.LoreCode, out var questPage))
            {
                return questPage;
            }

            return null;
        }

        public IEnumerable<QuestPage> GetAllEntryPagesInRecentOrder()
        {
            if (AllEntries == null) yield break;

            for (int i = AllEntries.Count - 1; i >= 0; i--)
            {
                var page = GetEntryPageForEntry(AllEntries[i]);
                if (page != null)
                {
                    yield return page;
                }
            }
        }

        public void Dispose()
        {
            foreach (var page in AllPages)
            {
                page?.Dispose();
            }
        }
    }
}
