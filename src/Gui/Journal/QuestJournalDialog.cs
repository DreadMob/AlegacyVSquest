using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class BrowseHistoryElement
    {
        public JournalPage Page;
        public string FilterContext;
        public float PosY;
    }

    public class QuestJournalDialog : GuiDialog
    {
        private const double ListHeight = 500.0;
        private const double ListWidth = 500.0;

        private List<IJournalPage> shownPages = new List<IJournalPage>();
        private JournalEntryManager entryManager;
        private Stack<BrowseHistoryElement> browseHistory = new Stack<BrowseHistoryElement>();

        private string currentSearchText;
        private string currentCategoryCode;
        private string currentQuestGiverFilter;

        private GuiComposer overviewGui;
        private GuiComposer detailViewGui;
        private JournalTab[] tabs;

        public override string ToggleKeyCombinationCode => null;
        public override double DrawOrder => 0.2;
        public override bool PrefersUngrabbedMouse => true;

        public QuestJournalDialog(ICoreClientAPI capi) : base(capi)
        {
            entryManager = new JournalEntryManager(capi);
            entryManager.LoadEntries();
            InitOverviewGui();
        }

        public void Refresh()
        {
            entryManager.LoadEntries();
            InitOverviewGui();
            FilterItems();
        }

        private void LoadEntries()
        {
            // Empty - handled by entryManager
        }

        private void BuildQuestGiverPages(List<QuestJournalEntry> entries)
        {
            // Empty - handled by entryManager
        }

        private void BuildQuestPages(List<QuestJournalEntry> entries)
        {
            // Empty - handled by entryManager
        }

        private void BuildNoteGiverPages(List<QuestJournalEntry> entries)
        {
            // Empty - handled by entryManager
        }

        private void BuildNotePages(List<QuestJournalEntry> entries)
        {
            // Empty - handled by entryManager
        }

        private string GetQuestGiverTitle(string questId)
        {
            return entryManager.GetQuestGiverTitle(questId);
        }

        private string GetEntryTitle(QuestJournalEntry entry)
        {
            return entryManager.GetEntryTitle(entry);
        }

        private bool IsNoteEntry(QuestJournalEntry entry)
        {
            return entry?.IsNote ?? false;
        }

        private QuestPage GetEntryPageForEntry(QuestJournalEntry entry)
        {
            return entryManager.GetEntryPageForEntry(entry);
        }

        private IEnumerable<QuestPage> GetAllEntryPagesInRecentOrder()
        {
            return entryManager.GetAllEntryPagesInRecentOrder();
        }

        private JournalTab[] GenerateTabs(out int currentTabIndex)
        {
            currentTabIndex = 0;
            var tabList = new List<JournalTab>
            {
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-all"), CategoryCode = "all" },
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-quests"), CategoryCode = "quests" },
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-notes"), CategoryCode = "notes" }
            };

            for (int i = 0; i < tabList.Count; i++)
            {
                if (tabList[i].CategoryCode == currentCategoryCode)
                {
                    currentTabIndex = i;
                    break;
                }
            }

            return tabList.ToArray();
        }

        private void InitOverviewGui()
        {
            ElementBounds searchFieldBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding - 2.0, 45.0, 300.0, 30.0);
            ElementBounds stackListBounds = ElementBounds.Fixed(0.0, 0.0, ListWidth, ListHeight).FixedUnder(searchFieldBounds, 5.0);
            ElementBounds clipBounds = stackListBounds.ForkBoundingParent();
            ElementBounds insetBounds = stackListBounds.FlatCopy().FixedGrow(6.0).WithFixedOffset(-3.0, -3.0);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3.0 + stackListBounds.fixedWidth + 7.0).WithFixedWidth(20.0);

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 18.0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(2.0, 0.0);

            ElementBounds backButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(-6.0, 3.0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, stackListBounds, scrollbarBounds, closeButtonBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.None)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPosition(0.0, 70.0);

            ElementBounds tabBounds = ElementBounds.Fixed(-200.0, 35.0, 200.0, 545.0);

            tabs = GenerateTabs(out int curTab);

            overviewGui = capi.Gui.CreateCompo("questjournal-overview", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("alegacyvsquest:journal-title"), OnTitleBarClose)
                .AddVerticalTabs(tabs, tabBounds, OnTabClicked, "verticalTabs")
                .AddTextInput(searchFieldBounds, FilterItemsBySearchText, CairoFont.WhiteSmallishText(), "searchField")
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                .AddInset(insetBounds, 3)
                .AddInteractiveElement(new JournalFlatList(capi, stackListBounds, OnLeftClickListElement, shownPages), "stacklist")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueOverviewPage, scrollbarBounds, "scrollbar")
                .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds, EnumButtonStyle.Normal, "backButton")
                .AddSmallButton(Lang.Get("alegacyvsquest:button-close-journal"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose();

            UpdateOverviewScrollbar();

            overviewGui.GetTextInput("searchField").SetPlaceHolderText(Lang.Get("alegacyvsquest:search-placeholder"));
            overviewGui.GetVerticalTab("verticalTabs").SetValue(curTab, triggerHandler: false);
            overviewGui.FocusElement(overviewGui.GetTextInput("searchField").TabIndex);

            if (string.IsNullOrEmpty(currentCategoryCode))
            {
                currentCategoryCode = "all";
            }
        }

        private void InitDetailGui()
        {
            ElementBounds textBounds = ElementBounds.Fixed(11.5, 45.0, ListWidth - 3.5, 30.0 + ListHeight + 17.0);
            ElementBounds clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6.0).WithFixedOffset(-3.0, -3.0);
            ElementBounds scrollbarBounds = clipBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7.0, -6.0, 0.0, 6.0).WithFixedWidth(20.0);

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(-11.0, 1.0);

            ElementBounds backButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(4.0, 1.0);

            ElementBounds overviewButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(0.0, 1.0);

            ElementBounds bgBounds = insetBounds.ForkBoundingParent(5.0, 40.0, 36.0, 52.0)
                .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2.0);
            bgBounds.WithChildren(insetBounds, textBounds, scrollbarBounds, backButtonBounds, closeButtonBounds);

            ElementBounds dialogBounds = bgBounds.ForkBoundingParent()
                .WithAlignment(EnumDialogArea.None)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPosition(0.0, 70.0);

            ElementBounds tabBounds = ElementBounds.Fixed(-200.0, 35.0, 200.0, 545.0);

            BrowseHistoryElement curPage = browseHistory.Peek();
            float posY = curPage.PosY;

            detailViewGui?.Dispose();
            detailViewGui = capi.Gui.CreateCompo("questjournal-detail", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("alegacyvsquest:journal-title"), OnTitleBarClose)
                .AddVerticalTabs(GenerateTabs(out int curTab), tabBounds, OnDetailViewTabClicked, "verticalTabs")
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                .AddInset(insetBounds, 3);

            curPage.Page.ComposePage(detailViewGui, textBounds, OpenDetailPageFor);
            GuiElement lastAddedElement = detailViewGui.LastAddedElement;

            detailViewGui.EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueDetailPage, scrollbarBounds, "scrollbar")
                .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds)
                .AddSmallButton(Lang.Get("alegacyvsquest:button-overview"), OnButtonOverview, overviewButtonBounds)
                .AddSmallButton(Lang.Get("general-close"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose();

            detailViewGui.GetScrollbar("scrollbar").SetHeights((float)ListHeight, (float)lastAddedElement.Bounds.fixedHeight);
            detailViewGui.GetScrollbar("scrollbar").CurrentYPosition = posY;
            OnNewScrollbarValueDetailPage(posY);
            detailViewGui.GetVerticalTab("verticalTabs").SetValue(curTab, triggerHandler: false);
        }

        private void UpdateOverviewScrollbar()
        {
            var stacklist = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (stacklist != null)
            {
                stacklist.CalcTotalHeight();
                overviewGui.GetScrollbar("scrollbar").SetHeights((float)ListHeight, (float)stacklist.insideBounds.fixedHeight);
            }
        }

        private void OnTabClicked(int index, GuiTab tab)
        {
            var journalTab = tab as JournalTab;
            currentCategoryCode = journalTab?.CategoryCode ?? "all";
            currentQuestGiverFilter = null;
            browseHistory.Clear();
            FilterItems();
        }

        private void OnDetailViewTabClicked(int index, GuiTab tab)
        {
            browseHistory.Clear();
            OnTabClicked(index, tab);
        }

        private void OnLeftClickListElement(int index)
        {
            if (index < 0 || index >= shownPages.Count) return;

            var page = shownPages[index] as JournalPage;
            if (page == null) return;

            if (page is QuestGiverPage giverPage)
            {
                currentQuestGiverFilter = giverPage.QuestGiverId;
                browseHistory.Push(new BrowseHistoryElement { Page = giverPage, FilterContext = currentQuestGiverFilter });
                FilterItems();
                return;
            }

            if (page is QuestPage questPage)
            {
                var entry = entryManager.AllEntries.FirstOrDefault(e =>
                    string.Equals(e.LoreCode, questPage.QuestId, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    var entryPage = new QuestEntryPage(capi, entry.QuestId, entryManager.GetEntryTitle(entry), entry.Chapters);
                    browseHistory.Push(new BrowseHistoryElement { Page = entryPage });
                    InitDetailGui();
                }
                return;
            }
        }

        private bool OpenDetailPageFor(string pageCode)
        {
            return true;
        }

        private void FilterItemsBySearchText(string text)
        {
            if (currentSearchText != text)
            {
                currentSearchText = text;
                FilterItems();
            }
        }

        private void FilterItems()
        {
            shownPages.Clear();
            string searchLower = currentSearchText?.ToLowerInvariant()?.Trim() ?? "";

            if (currentCategoryCode == "notes")
            {
                if (string.IsNullOrEmpty(currentQuestGiverFilter))
                {
                    foreach (var page in entryManager.NoteGiverPages)
                    {
                        if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                        {
                            shownPages.Add(page);
                            continue;
                        }

                        bool hasMatchingNote = entryManager.NotePages.Any(notePage =>
                            string.Equals(notePage.QuestGiverId, page.QuestGiverId, StringComparison.OrdinalIgnoreCase)
                            && notePage.GetTextMatchWeight(searchLower) > 0);

                        if (hasMatchingNote)
                        {
                            shownPages.Add(page);
                        }
                    }
                }
                else
                {
                    foreach (var page in entryManager.NotePages)
                    {
                        if (string.Equals(page.QuestGiverId, currentQuestGiverFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                            {
                                shownPages.Add(page);
                            }
                        }
                    }
                }
            }
            else if (currentCategoryCode == "all")
            {
                foreach (var page in entryManager.GetAllEntryPagesInRecentOrder())
                {
                    if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                    {
                        shownPages.Add(page);
                    }
                }
            }
            else if (currentCategoryCode == "quests")
            {
                if (string.IsNullOrEmpty(currentQuestGiverFilter))
                {
                    foreach (var page in entryManager.AllPages.OfType<QuestGiverPage>())
                    {
                        if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                        {
                            shownPages.Add(page);
                            continue;
                        }

                        bool hasMatchingQuest = entryManager.AllPages.OfType<QuestPage>().Any(questPage =>
                            string.Equals(questPage.QuestGiverId, page.QuestGiverId, StringComparison.OrdinalIgnoreCase)
                            && questPage.GetTextMatchWeight(searchLower) > 0);

                        if (hasMatchingQuest)
                        {
                            shownPages.Add(page);
                        }
                    }
                }
                else
                {
                    foreach (var page in entryManager.AllPages.OfType<QuestPage>())
                    {
                        if (string.Equals(page.QuestGiverId, currentQuestGiverFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                            {
                                shownPages.Add(page);
                            }
                        }
                    }
                }
            }

            var stacklist = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (stacklist != null)
            {
                stacklist.Elements = shownPages;
                stacklist.CalcTotalHeight();
            }

            UpdateOverviewScrollbar();
        }

        private void OnNewScrollbarValueOverviewPage(float value)
        {
            var flatList = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (flatList != null)
            {
                flatList.insideBounds.fixedY = 3f - value;
                flatList.insideBounds.CalcWorldBounds();
            }
        }

        private void OnNewScrollbarValueDetailPage(float value)
        {
            var richtext = detailViewGui?.GetRichtext("richtext");
            if (richtext != null)
            {
                richtext.Bounds.fixedY = 3f - value;
                richtext.Bounds.CalcWorldBounds();
            }
            if (browseHistory.Count > 0)
            {
                browseHistory.Peek().PosY = detailViewGui?.GetScrollbar("scrollbar")?.CurrentYPosition ?? 0f;
            }
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        private bool OnButtonBack()
        {
            if (browseHistory.Count == 0)
            {
                return true;
            }

            browseHistory.Pop();

            if (browseHistory.Count > 0)
            {
                var prev = browseHistory.Peek();
                if (prev.Page is QuestGiverPage)
                {
                    currentQuestGiverFilter = prev.FilterContext;
                    FilterItems();
                }
                else
                {
                    InitDetailGui();
                }
            }
            else
            {
                currentQuestGiverFilter = null;
                FilterItems();
            }

            return true;
        }

        private bool OnButtonOverview()
        {
            browseHistory.Clear();
            currentQuestGiverFilter = null;
            FilterItems();
            return true;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (browseHistory.Count == 0 || browseHistory.Peek().Page is QuestGiverPage)
            {
                SingleComposer = overviewGui;
            }
            else
            {
                SingleComposer = detailViewGui;
            }

            if (SingleComposer == overviewGui)
            {
                var backButton = overviewGui.GetButton("backButton");
                if (backButton != null)
                {
                    backButton.Enabled = browseHistory.Count > 0;
                }
            }

            base.OnRenderGUI(deltaTime);
        }

        public override void OnGuiOpened()
        {
            InitOverviewGui();
            FilterItems();
            base.OnGuiOpened();
        }

        public override void OnGuiClosed()
        {
            browseHistory.Clear();
            currentQuestGiverFilter = null;
            var searchField = overviewGui?.GetTextInput("searchField");
            if (searchField != null)
            {
                searchField.SetValue("");
            }
            currentSearchText = "";
            base.OnGuiClosed();
        }

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override void Dispose()
        {
            entryManager?.Dispose();
            overviewGui?.Dispose();
            detailViewGui?.Dispose();
        }
    }
}
