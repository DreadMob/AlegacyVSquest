using System;

using System.Collections.Generic;

using System.Reflection;

using Vintagestory.API.Datastructures;



using Vintagestory.API.Common;



using Vintagestory.API.Client;

using Vintagestory.API.Config;



namespace VsQuest

{

    public class QuestSelectGui : GuiDialog

    {

        public override string ToggleKeyCombinationCode => null;



        private const string DropDownKey = "questdropdown";



        private bool recomposeQueued;



        private long questGiverId;

        private string selectedAvailableQuestId;

        private ActiveQuest selectedActiveQuest;

        private string selectedActiveQuestKey;



        private List<string> availableQuestIds;

        private List<ActiveQuest> activeQuests;

        private IClientPlayer player;

        private string noAvailableQuestDescLangKey;

        private string noAvailableQuestCooldownDescLangKey;

        private int noAvailableQuestCooldownDaysLeft;

        private int noAvailableQuestRotationDaysLeft;

        private string reputationNpcId;

        private string reputationFactionId;

        private int reputationNpcValue;

        private int reputationFactionValue;

        private string reputationNpcRankLangKey;

        private string reputationFactionRankLangKey;

        private string reputationNpcTitleLangKey;

        private string reputationFactionTitleLangKey;

        private bool reputationNpcHasRewards;

        private bool reputationFactionHasRewards;

        private int reputationNpcRewardsCount;

        private int reputationFactionRewardsCount;

        private List<QuestCompletionRewardStatus> completionRewards;

        private List<ReputationRankRewardStatus> reputationNpcRankRewards;

        private List<ReputationRankRewardStatus> reputationFactionRankRewards;

        // Trial shop state
        private bool hasTrialShop;
        private int trialShopShards;
        private int trialShopReputation;
        private string trialShopRankName;
        private string trialModifierName;
        private List<string> trialChallengeNames;
        private List<TrialShopItemData> trialShopItems;

        private Dictionary<string, string> lastRewardStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);



        private int curTab = 0;

        private bool closeGuiAfterAcceptingAndCompleting;



        public QuestSelectGui(ICoreClientAPI capi, QuestInfoMessage message, QuestConfig questConfig) : base(capi)

        {

            player = capi.World.Player;

            closeGuiAfterAcceptingAndCompleting = questConfig?.CloseGuiAfterAcceptingAndCompleting ?? false;

            ApplyData(message);

            RequestRecompose();

        }



        private static string ActiveQuestKey(ActiveQuest quest)

        {

            if (quest == null) return null;

            return $"{quest.questGiverId}:{quest.questId}";

        }



        private void RequestRecompose()

        {

            if (recomposeQueued) return;

            recomposeQueued = true;



            capi.Event.EnqueueMainThreadTask(() =>

            {

                recomposeQueued = false;

                recompose();

            }, "alegacyvsquest-recompose");

        }



        private void CloseOpenedDropDownDeferred()

        {

            capi.Event.EnqueueMainThreadTask(() =>

            {

                CloseOpenedDropDown();

            }, "alegacyvsquest-closedropdown");

        }



        private void ApplyData(long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests, string noAvailableQuestDescLangKey, string noAvailableQuestCooldownDescLangKey, int noAvailableQuestCooldownDaysLeft, int noAvailableQuestRotationDaysLeft)

        {

            this.questGiverId = questGiverId;

            this.availableQuestIds = availableQuestIds;

            this.activeQuests = activeQuests;

            this.noAvailableQuestDescLangKey = noAvailableQuestDescLangKey;

            this.noAvailableQuestCooldownDescLangKey = noAvailableQuestCooldownDescLangKey;

            this.noAvailableQuestCooldownDaysLeft = noAvailableQuestCooldownDaysLeft;

            this.noAvailableQuestRotationDaysLeft = noAvailableQuestRotationDaysLeft;



            if (activeQuests != null && activeQuests.Count > 0)

            {

                if (!string.IsNullOrEmpty(selectedActiveQuestKey))

                {

                    selectedActiveQuest = activeQuests.Find(q => ActiveQuestKey(q) == selectedActiveQuestKey);

                }



                if (selectedActiveQuest == null)

                {

                    selectedActiveQuest = activeQuests[0];

                }



                selectedActiveQuestKey = ActiveQuestKey(selectedActiveQuest);

            }

            else

            {

                selectedActiveQuest = null;

                selectedActiveQuestKey = null;

            }



            // Preserve the currently selected tab when updating data.

            // Only switch tabs if the current tab has no content.

            bool hasAvailable = availableQuestIds != null && availableQuestIds.Count > 0;

            bool hasActive = activeQuests != null && activeQuests.Count > 0;

            bool hasReputation = (completionRewards != null && completionRewards.Count > 0)

                || !string.IsNullOrWhiteSpace(reputationNpcId)

                || !string.IsNullOrWhiteSpace(reputationFactionId);



            if (curTab == 0 && !hasAvailable && hasActive)

            {

                curTab = 1;

            }

            else if (curTab == 1 && !hasActive && hasAvailable)

            {

                curTab = 0;

            }

            else if (curTab == 2 && !hasReputation)

            {

                curTab = hasAvailable ? 0 : (hasActive ? 1 : 0);

            }

            else if (curTab != 0 && curTab != 1 && curTab != 2)

            {

                // Initial state fallback

                curTab = hasAvailable ? 0 : (hasActive ? 1 : (hasReputation ? 2 : 0));

            }

        }



        private void ApplyData(QuestInfoMessage message)

        {

            if (message == null) return;



            var prevStatuses = new Dictionary<string, string>(lastRewardStatuses, StringComparer.OrdinalIgnoreCase);

            var domainActiveQuests = new List<ActiveQuest>(message?.activeQuests?.Count ?? 0);
            if (message?.activeQuests != null)
            {
                for (int i = 0; i < message.activeQuests.Count; i++)
                {
                    var dto = message.activeQuests[i];
                    if (dto == null) continue;
                    domainActiveQuests.Add(dto.ToDomain());
                }
            }

            ApplyData(message.questGiverId, message.availableQestIds, domainActiveQuests, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft, message.noAvailableQuestRotationDaysLeft);
            reputationNpcId = message.reputationNpcId;

            reputationFactionId = message.reputationFactionId;

            reputationNpcValue = message.reputationNpcValue;

            reputationFactionValue = message.reputationFactionValue;

            reputationNpcRankLangKey = message.reputationNpcRankLangKey;

            reputationFactionRankLangKey = message.reputationFactionRankLangKey;

            reputationNpcTitleLangKey = message.reputationNpcTitleLangKey;

            reputationFactionTitleLangKey = message.reputationFactionTitleLangKey;

            reputationNpcHasRewards = message.reputationNpcHasRewards;

            reputationFactionHasRewards = message.reputationFactionHasRewards;

            reputationNpcRewardsCount = message.reputationNpcRewardsCount;

            reputationFactionRewardsCount = message.reputationFactionRewardsCount;

            completionRewards = message.completionRewards ?? new List<QuestCompletionRewardStatus>();

            reputationNpcRankRewards = message.reputationNpcRankRewards ?? new List<ReputationRankRewardStatus>();

            reputationFactionRankRewards = message.reputationFactionRankRewards ?? new List<ReputationRankRewardStatus>();

            // Trial shop
            hasTrialShop = message.hasTrialShop;
            trialShopShards = message.trialShopShards;
            trialShopReputation = message.trialShopReputation;
            trialShopRankName = message.trialShopRankName;
            trialShopItems = message.trialShopItems;
            trialModifierName = message.trialModifierName;
            trialChallengeNames = message.trialChallengeNames;

            lastRewardStatuses = BuildRewardStatuses();

            if (HasAnyRewardTransitionedToClaimed(prevStatuses, lastRewardStatuses))

            {

                capi?.Gui?.PlaySound("player/coin1");

            }

        }



        private Dictionary<string, string> BuildRewardStatuses()

        {

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);



            if (completionRewards != null)

            {

                for (int i = 0; i < completionRewards.Count; i++)

                {

                    var r = completionRewards[i];

                    if (r == null || string.IsNullOrWhiteSpace(r.id)) continue;

                    map[r.id] = r.status;

                }

            }



            var rankRewards = !string.IsNullOrWhiteSpace(reputationNpcId)

                ? (reputationNpcRankRewards ?? new List<ReputationRankRewardStatus>())

                : (reputationFactionRankRewards ?? new List<ReputationRankRewardStatus>());



            if (rankRewards != null)

            {

                for (int i = 0; i < rankRewards.Count; i++)

                {

                    var rr = rankRewards[i];

                    if (rr == null) continue;

                    map["rank:" + rr.min] = rr.status;

                }

            }



            return map;

        }



        private static bool HasAnyRewardTransitionedToClaimed(Dictionary<string, string> previous, Dictionary<string, string> current)

        {

            if (current == null || current.Count == 0) return false;



            foreach (var kvp in current)

            {

                string cur = kvp.Value;

                if (!string.Equals(cur, "claimed", StringComparison.OrdinalIgnoreCase)) continue;



                if (!previous.TryGetValue(kvp.Key, out string prev))

                {

                    continue;

                }



                if (!string.Equals(prev, "claimed", StringComparison.OrdinalIgnoreCase))

                {

                    return true;

                }

            }



            return false;

        }



        private void recompose()

        {

            CloseOpenedDropDown();

            var prevComposer = SingleComposer;

            if (prevComposer != null)

            {

                prevComposer.Dispose();

                Composers.Remove("single");

            }



            const int questsWidth = 400;

            const int reputationWidth = 520;

            int mainWidth = (curTab == 2 || curTab == 3) ? reputationWidth : questsWidth;



            var tabsList = new List<GuiTab>

            {

                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-available-quests"), DataInt = 0 },

                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-active-quests"), DataInt = 1 }

            };



            if ((completionRewards != null && completionRewards.Count > 0)

                || !string.IsNullOrWhiteSpace(reputationNpcId)

                || !string.IsNullOrWhiteSpace(reputationFactionId))

            {

                tabsList.Add(new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-reputation"), DataInt = 2 });

            }

            if (hasTrialShop)

            {

                tabsList.Add(new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-trial-shop"), DataInt = 3 });

            }



            GuiTab[] tabs = tabsList.ToArray();



            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);

            ElementBounds questTextBounds = ElementBounds.Fixed(0, 60, mainWidth, 500);

            ElementBounds scrollbarBounds = questTextBounds.CopyOffsetedSibling(questTextBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(questTextBounds.fixedHeight);

            ElementBounds clippingBounds = ElementBounds.Fixed(0, 60, mainWidth, 500);



            int halfButtonWidth = Math.Max(100, (mainWidth - 30) / 2);

            ElementBounds bottomLeftButtonBounds = ElementBounds.Fixed(10, 570, halfButtonWidth, 20);

            ElementBounds bottomRightButtonBounds = ElementBounds.Fixed(20 + halfButtonWidth, 570, halfButtonWidth, 20);



            bgBounds.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo("QuestSelectDialog-", dialogBounds)

                            .AddShadedDialogBG(bgBounds)

                            .AddDialogTitleBar(Lang.Get("alegacyvsquest:quest-select-title"), () => TryClose())

                            .AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 200), OnTabClicked, "tabs")

                            .BeginChildElements(bgBounds);



            // GuiElementVerticalTabs constructor forces tabs[0].Active = true.

            // Force the correct active tab for visual highlight.

            SingleComposer.GetVerticalTab("tabs").SetValue(curTab, false);



            if (curTab == 0)

            {

                if (availableQuestIds != null && availableQuestIds.Count > 0)

                {

                    if (string.IsNullOrWhiteSpace(selectedAvailableQuestId)

                        || !availableQuestIds.Contains(selectedAvailableQuestId))

                    {

                        selectedAvailableQuestId = availableQuestIds[0];

                    }



                    int selectedIndex = Math.Max(0, availableQuestIds.IndexOf(selectedAvailableQuestId));



                    SingleComposer

                        .AddDropDown(

                            availableQuestIds.ToArray(),

                            availableQuestIds.ConvertAll(id => LocalizationUtils.GetSafe(id + "-title")).ToArray(),

                            selectedIndex,

                            onAvailableQuestSelectionChanged,

                            ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, mainWidth, 30),

                            DropDownKey)

                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)

                        .AddButton(Lang.Get("alegacyvsquest:button-accept"), acceptQuest, bottomRightButtonBounds)

                        .BeginClip(clippingBounds)

                            .AddRichtext(questText(selectedAvailableQuestId), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")

                        .EndClip()

                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");

                }

                else

                {

                    bool hasCooldownText = !string.IsNullOrWhiteSpace(noAvailableQuestCooldownDescLangKey)

                        && (noAvailableQuestCooldownDaysLeft > 0 || noAvailableQuestRotationDaysLeft > 0);



                    string text;

                    if (hasCooldownText)

                    {

                        // Some templates accept 2 args (cooldown + rotation), others accept only 1.

                        // Try the 2-arg format first and fall back to 1-arg on formatting errors.

                        try

                        {

                            text = LocalizationUtils.GetSafe(noAvailableQuestCooldownDescLangKey, noAvailableQuestCooldownDaysLeft, noAvailableQuestRotationDaysLeft);

                        }

                        catch

                        {

                            // Most quest givers only need a single value.

                            // Prefer cooldown if present; otherwise fall back to rotation.

                            int value = noAvailableQuestCooldownDaysLeft > 0 ? noAvailableQuestCooldownDaysLeft : noAvailableQuestRotationDaysLeft;

                            text = LocalizationUtils.GetSafe(noAvailableQuestCooldownDescLangKey, value);

                        }

                    }

                    else

                    {

                        text = !string.IsNullOrWhiteSpace(noAvailableQuestDescLangKey)

                            ? LocalizationUtils.GetSafe(noAvailableQuestDescLangKey)

                            : Lang.Get("alegacyvsquest:no-quest-available-desc");

                    }



                    SingleComposer

                        .AddStaticText(text, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, mainWidth, 500))

                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));

                }

            }

            else if (curTab == 1)

            {

                if (activeQuests != null && activeQuests.Count > 0)

                {

                    if (selectedActiveQuest == null)

                    {

                        selectedActiveQuest = activeQuests[0];

                        selectedActiveQuestKey = ActiveQuestKey(selectedActiveQuest);

                    }



                    bool hasQuiz = HasQuizConfig(selectedActiveQuest.questId);



                    string[] activeQuestKeys = activeQuests.ConvertAll(q => ActiveQuestKey(q)).ToArray();

                    string[] activeQuestTitles = activeQuests.ConvertAll(q => LocalizationUtils.GetSafe(q.questId + "-title")).ToArray();

                    int selected = Math.Max(0, Array.IndexOf(activeQuestKeys, selectedActiveQuestKey));



                    SingleComposer

                        .AddDropDown(activeQuestKeys, activeQuestTitles, selected, onActiveQuestSelectionChanged,

                            ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, mainWidth, 30), DropDownKey)

                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)

                        .AddIf(selectedActiveQuest.ClientState.IsCompletableOnClient)

                            .AddButton(Lang.Get("alegacyvsquest:button-complete"), completeQuest, bottomRightButtonBounds)

                        .EndIf()

                        .AddIf(!selectedActiveQuest.ClientState.IsCompletableOnClient && selectedActiveQuest.ClientState.IsCurrentStageCompleteOnClient)

                            .AddButton(Lang.Get("alegacyvsquest:button-continue"), completeQuest, bottomRightButtonBounds)

                        .EndIf()

                        .AddIf(!selectedActiveQuest.ClientState.IsCompletableOnClient && !selectedActiveQuest.ClientState.IsCurrentStageCompleteOnClient && hasQuiz)

                            .AddButton(Lang.Get("alegacyvsquest:button-open-quiz"), () => OpenQuiz(selectedActiveQuest.questId), bottomRightButtonBounds)

                        .EndIf()

                        .BeginClip(clippingBounds)

                            .AddRichtext(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")

                        .EndClip()

                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");

                }

                else

                {

                    SingleComposer

                        .AddStaticText(Lang.Get("alegacyvsquest:no-quest-active-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, mainWidth, 500))

                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));

                }

            }

            else if (curTab == 2)

            {

                int repValue = !string.IsNullOrWhiteSpace(reputationNpcId) ? reputationNpcValue : reputationFactionValue;



                var nodes = BuildRewardNodes();

                var levelBounds = ElementBounds.Fixed(0, 20, mainWidth, 25);

                var mapBounds = ElementBounds.Fixed(0, 60, mainWidth, 500);

                mapBounds.WithParent(bgBounds);



                string levelText = ReputationUiHelper.GetReputationHeaderText(reputationNpcId, repValue, reputationNpcRankLangKey, reputationFactionRankLangKey);



                var closeCenteredBounds = ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20);



                SingleComposer

                    .AddStaticText(levelText, CairoFont.WhiteSmallishText(), levelBounds)

                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, closeCenteredBounds)

                    .AddInteractiveElement(new ReputationTreeElement(capi, mapBounds, nodes, OnRewardNodeClicked), "reputationtree");

            }
            else if (curTab == 3 && hasTrialShop)
            {
                // Trial Shop tab — uses ReputationTreeElement for icon-based display
                string repLabel = LocalizationUtils.GetSafe("albase:trial-reputation-label");
                string currLabel = LocalizationUtils.GetSafe("albase:trial-currency-name");
                string headerText = $"{trialShopRankName ?? ""}  |  {repLabel}: {trialShopReputation}  |  {currLabel}: {trialShopShards}";
                if (!string.IsNullOrWhiteSpace(trialModifierName))
                {
                    headerText += $"\n{LocalizationUtils.GetSafe("albase:trial-modifier-gui-label")}: {trialModifierName}";
                }

                var levelBounds = ElementBounds.Fixed(0, 20, mainWidth, 25);
                var mapBounds = ElementBounds.Fixed(0, 60, mainWidth, 500);
                mapBounds.WithParent(bgBounds);
                var closeCenteredBounds2 = ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20);

                var shopNodes = BuildTrialShopNodes();

                SingleComposer
                    .AddStaticText(headerText, CairoFont.WhiteSmallishText(), levelBounds)
                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, closeCenteredBounds2)
                    .AddInteractiveElement(new ReputationTreeElement(capi, mapBounds, shopNodes, OnTrialShopNodeClicked), "shopnodes");
            }

            SingleComposer.EndChildElements().Compose();



            var questTextElement = SingleComposer.GetRichtext("questtext");

            if (questTextElement != null)

            {

                SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)questTextBounds.fixedHeight, (float)questTextBounds.fixedHeight);

                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)questTextElement.TotalHeight + 200);

                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

                OnNewScrollbarvalue(0);

            }

        }



        private List<ReputationTreeNode> BuildTrialShopNodes()
        {
            var nodes = new List<ReputationTreeNode>();
            if (trialShopItems == null) return nodes;

            for (int i = 0; i < trialShopItems.Count; i++)
            {
                var item = trialShopItems[i];
                if (item == null) continue;

                string name = !string.IsNullOrWhiteSpace(item.NameKey)
                    ? LocalizationUtils.GetSafe(item.NameKey)
                    : item.ItemCode;

                bool canBuy = !item.IsLocked && trialShopShards >= item.Cost;
                bool soldOut = item.MaxPurchases > 0 && item.PurchasesMade >= item.MaxPurchases;

                ReputationNodeStatus status;
                string reqText;

                if (item.IsLocked)
                {
                    status = ReputationNodeStatus.Locked;
                    reqText = $"{item.Cost}◆ | {LocalizationUtils.GetSafe("albase:trial-warden-shop-locked")}";
                }
                else if (soldOut)
                {
                    status = ReputationNodeStatus.Claimed;
                    reqText = $"{item.Cost}◆ | ✓";
                }
                else if (canBuy)
                {
                    status = ReputationNodeStatus.Available;
                    reqText = $"{item.Cost}◆\n{Lang.Get("alegacyvsquest:reputation-lmb-claim")}";
                }
                else
                {
                    status = ReputationNodeStatus.Locked;
                    reqText = $"{item.Cost}◆ | Недостаточно осколков";
                }

                // Resolve icon item code for display
                string iconCode = item.ItemCode;
                if (iconCode.StartsWith("case:", StringComparison.OrdinalIgnoreCase))
                {
                    iconCode = "game:gear-temporal"; // Use temporal gear icon for cases
                }

                nodes.Add(new ReputationTreeNode
                {
                    Id = "shop:" + i,
                    Title = name,
                    RequirementText = reqText,
                    X = 0, // Will be laid out by grid
                    Y = 0,
                    Status = status,
                    IconItemCode = iconCode
                });
            }

            ApplyReputationGridLayout(nodes);
            return nodes;
        }

        private void OnTrialShopNodeClicked(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;
            if (!nodeId.StartsWith("shop:", StringComparison.OrdinalIgnoreCase)) return;

            string indexStr = nodeId.Substring(5);
            if (!int.TryParse(indexStr, out int index)) return;
            if (trialShopItems == null || index < 0 || index >= trialShopItems.Count) return;

            var item = trialShopItems[index];
            if (item == null || item.IsLocked) return;
            if (item.MaxPurchases > 0 && item.PurchasesMade >= item.MaxPurchases) return;
            if (trialShopShards < item.Cost) return;

            // Send buy request via trial shop channel
            capi.Network.GetChannel(TrialShopNetworkHandler.ChannelName).SendPacket(new BuyTrialShopItemMessage
            {
                ItemCode = item.ItemCode,
                Cost = item.Cost
            });

            // Refresh the quest giver UI
            if (questGiverId > 0)
            {
                capi.Network.GetChannel("alegacyvsquest").SendPacket(new DialogTriggerMessage
                {
                    EntityId = questGiverId,
                    Trigger = "openquests"
                });
            }
        }


        private void OnNewScrollbarvalue(float value)

        {

            var textArea = SingleComposer.GetRichtext("questtext");

            if (textArea == null) return;



            textArea.Bounds.fixedY = -value;

            textArea.Bounds.CalcWorldBounds();

        }



        private void OnTabClicked(int id, GuiTab tab)

        {

            CloseOpenedDropDown();

            curTab = id;

            RequestRecompose();

        }



        private List<ReputationTreeNode> BuildRewardNodes()

        {

            var nodes = new List<ReputationTreeNode>();

            if (completionRewards == null) completionRewards = new List<QuestCompletionRewardStatus>();



            // Add rank-based rewards (e.g. innkeeper uranium at 2000 rep) as additional nodes.

            var rankRewards = !string.IsNullOrWhiteSpace(reputationNpcId)

                ? (reputationNpcRankRewards ?? new List<ReputationRankRewardStatus>())

                : (reputationFactionRankRewards ?? new List<ReputationRankRewardStatus>());



            if (rankRewards != null)

            {

                // Filter out null entries first so layout spacing is deterministic.

                rankRewards.RemoveAll(r => r == null);

                if (rankRewards.Count == 0) return nodes;



                rankRewards.Sort((a, b) => (a?.min ?? 0).CompareTo(b?.min ?? 0));

                for (int i = 0; i < rankRewards.Count; i++)

                {

                    var rr = rankRewards[i];



                    var status = rr.status == "claimed"

                        ? ReputationNodeStatus.Claimed

                        : (rr.status == "available" ? ReputationNodeStatus.Available : ReputationNodeStatus.Locked);



                    // Position is assigned later by ApplyReputationGridLayout(nodes).

                    float x = 0.5f;

                    float y = 0.5f;



                    string title = ReputationUiHelper.GetRankRewardTitle(capi, reputationNpcId, rr);



                    string req = LocalizationUtils.GetSafe("alegacyvsquest:reputation-value-template", rr.min);

                    if (!string.IsNullOrWhiteSpace(rr.rankLangKey))

                    {

                        if (string.IsNullOrWhiteSpace(title) || title == rr.min.ToString())

                        {

                            title = LocalizationUtils.GetSafe(rr.rankLangKey);

                        }

                    }



                    if (rr.status == "claimed")

                    {

                        string line = Lang.Get("alegacyvsquest:reputation-received");

                        req = string.IsNullOrWhiteSpace(req) ? line : (req + "\n" + line);

                    }

                    else if (rr.status == "available")

                    {

                        string line = Lang.Get("alegacyvsquest:reputation-lmb-claim");

                        req = string.IsNullOrWhiteSpace(req) ? line : (req + "\n" + line);

                    }



                    nodes.Add(new ReputationTreeNode

                    {

                        Id = "rank:" + rr.min,

                        Title = title,

                        RequirementText = req,

                        X = x,

                        Y = y,

                        Status = status,

                        IconItemCode = rr.iconItemCode

                    });

                }

            }



            // Quest completion rewards (e.g. Eternal Hunt) should appear after rank-based rewards.

            if (completionRewards.Count > 1)

            {

                completionRewards.Sort((a, b) =>

                {

                    if (a == null && b == null) return 0;

                    if (a == null) return 1;

                    if (b == null) return -1;



                    int y = a.y.CompareTo(b.y);

                    if (y != 0) return y;



                    int x = a.x.CompareTo(b.x);

                    if (x != 0) return x;



                    return string.CompareOrdinal(a.id, b.id);

                });

            }



            for (int i = 0; i < completionRewards.Count; i++)

            {

                var reward = completionRewards[i];

                if (reward == null || string.IsNullOrWhiteSpace(reward.id)) continue;



                string reqText = reward.requirementText;

                if (reward.status == "claimed")

                {

                    string line = Lang.Get("alegacyvsquest:reputation-received");

                    reqText = string.IsNullOrWhiteSpace(reqText) ? line : (reqText + "\n" + line);

                }

                else if (reward.status == "available")

                {

                    string line = Lang.Get("alegacyvsquest:reputation-lmb-claim");

                    reqText = string.IsNullOrWhiteSpace(reqText) ? line : (reqText + "\n" + line);

                }



                var status = reward.status == "claimed"

                    ? ReputationNodeStatus.Claimed

                    : (reward.status == "available" ? ReputationNodeStatus.Available : ReputationNodeStatus.Locked);



                nodes.Add(new ReputationTreeNode

                {

                    Id = reward.id,

                    Title = reward.title,

                    RequirementText = reqText,

                    X = reward.x,

                    Y = reward.y,

                    Status = status,

                    IconItemCode = reward.iconItemCode

                });

            }



            ApplyReputationGridLayout(nodes);

            return nodes;

        }



        private void ApplyReputationGridLayout(List<ReputationTreeNode> nodes)

        {

            if (nodes == null || nodes.Count == 0) return;



            const int columns = 3;

            const int maxRows = 4;



            float xStart = 0.2f;

            float xEnd = 0.8f;

            float yStart = 0.18f;

            float yStep = 0.24f;



            float xStep = columns <= 1 ? 0f : (xEnd - xStart) / (columns - 1);



            for (int i = 0; i < nodes.Count; i++)

            {

                var node = nodes[i];

                if (node == null) continue;



                int row = i / columns;

                int col = i % columns;



                if (row >= maxRows) row = maxRows - 1;



                node.X = columns <= 1 ? 0.5f : (xStart + xStep * col);

                node.Y = yStart + yStep * row;

                if (node.Y > 0.95f) node.Y = 0.95f;

            }

        }



        private void OnRewardNodeClicked(string rewardId)

        {

            if (string.IsNullOrWhiteSpace(rewardId)) return;



            // Rank reward nodes: claim pending reputation rewards for this quest giver.

            if (rewardId.StartsWith("rank:", StringComparison.OrdinalIgnoreCase))

            {

                string scope = !string.IsNullOrWhiteSpace(reputationNpcId) ? "npc" : "faction";

                capi.Network.GetChannel("alegacyvsquest").SendPacket(new ClaimReputationRewardsMessage

                {

                    questGiverId = questGiverId,

                    scope = scope

                });

                return;

            }



            var status = completionRewards?.Find(r => r != null && r.id == rewardId);

            if (status == null || status.status != "available") return;



            var message = new ClaimQuestCompletionRewardMessage

            {

                rewardId = rewardId,

                questGiverId = questGiverId

            };

            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);

        }



        private string questText(string questId)

        {

            string text = LocalizationUtils.GetSafe(questId + "-desc");

            string extra = BuildLandClaimExtraText(questId);

            if (!string.IsNullOrWhiteSpace(extra))

            {

                if (string.IsNullOrWhiteSpace(text))

                {

                    text = extra;

                }

                else

                {

                    text = text + "\n\n" + extra;

                }

            }



            return text;

        }



        private string activeQuestText(ActiveQuest quest)

        {

            string baseText = quest.ClientState?.ProgressText ?? string.Empty;

            // Append trial challenges if available
            if (trialChallengeNames != null && trialChallengeNames.Count > 0)
            {
                string challengeHeader = LocalizationUtils.GetSafe("albase:trial-challenges-header");
                var sb = new System.Text.StringBuilder(baseText);
                sb.AppendLine();
                sb.AppendLine($"<font color=\"#A78BFA\">{challengeHeader}</font>");
                foreach (var name in trialChallengeNames)
                {
                    sb.AppendLine($"<font color=\"#C4B5FD\">  \u2022 {name}</font>");
                }
                return sb.ToString();
            }

            return baseText;

        }



        private string BuildLandClaimExtraText(string questId)

        {

            if (!IsLandClaimQuest(questId)) return null;



            ITreeAttribute wa = player?.Entity?.WatchedAttributes;

            if (wa == null) return null;



            int allowance = wa.GetInt("landclaimallowance", 0);

            int maxAreas = wa.GetInt("landclaimmaxareas", 0);



            string headerKey = BuildLandClaimLangKey(questId, "landclaim-extra-header");

            string allowanceKey = BuildLandClaimLangKey(questId, "landclaim-extra-allowance");

            string areasKey = BuildLandClaimLangKey(questId, "landclaim-extra-areas");



            string header = LocalizationUtils.GetSafe(headerKey);

            string allowanceText = LocalizationUtils.GetSafe(allowanceKey, allowance);

            string areasText = LocalizationUtils.GetSafe(areasKey, maxAreas);



            return string.Join("\n", new[] { header, allowanceText, areasText });

        }



        private static string BuildLandClaimLangKey(string questId, string suffix)

        {

            string domain = GetQuestDomain(questId);

            return string.IsNullOrWhiteSpace(domain) ? suffix : $"{domain}:{suffix}";

        }



        private static string GetQuestDomain(string questId)

        {

            if (string.IsNullOrWhiteSpace(questId)) return null;



            int colonIndex = questId.IndexOf(':');

            if (colonIndex <= 0) return null;



            return questId.Substring(0, colonIndex);

        }



        private bool IsLandClaimQuest(string questId)

        {

            if (string.IsNullOrWhiteSpace(questId)) return false;



            var questSystem = capi?.ModLoader?.GetModSystem<QuestSystem>();

            if (questSystem?.QuestRegistry == null) return false;



            if (!questSystem.QuestRegistry.TryGetValue(questId, out var questDef) || questDef == null)

            {

                return false;

            }



            return HasLandClaimActions(questDef);

        }



        private static bool HasLandClaimActions(Quest questDef)

        {

            if (questDef == null) return false;



            return HasLandClaimAction(questDef.onAcceptedActions)

                || HasLandClaimAction(questDef.actionRewards);

        }



        private static bool HasLandClaimAction(List<ActionWithArgs> actions)

        {

            if (actions == null) return false;



            foreach (var action in actions)

            {

                if (action == null || string.IsNullOrWhiteSpace(action.id)) continue;

                if (string.Equals(action.id, "landclaimallowance", StringComparison.OrdinalIgnoreCase)

                    || string.Equals(action.id, "landclaimmaxareas", StringComparison.OrdinalIgnoreCase))

                {

                    return true;

                }

            }



            return false;

        }



        private bool HasQuizConfig(string questId)

        {

            if (string.IsNullOrWhiteSpace(questId) || capi?.Assets == null) return false;

            int colonIndex = questId.IndexOf(':');

            if (colonIndex <= 0 || colonIndex >= questId.Length - 1) return false;

            string domain = questId.Substring(0, colonIndex);

            string path = questId.Substring(colonIndex + 1);

            var asset = capi.Assets.TryGet(new AssetLocation(domain, $"config/quizzes/{path}.json"));

            return asset != null;

        }



        private bool OpenQuiz(string questId)

        {

            capi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName).SendPacket(new OpenQuizMessage

            {

                QuizId = questId,

                Reset = false

            });

            TryClose();

            return true;

        }



        private void RequestQuestInfoRefresh()

        {

            if (questGiverId <= 0) return;



            capi.Network.GetChannel("alegacyvsquest").SendPacket(new DialogTriggerMessage

            {

                EntityId = questGiverId,

                Trigger = "openquests"

            });

        }



        private bool acceptQuest()

        {

            var message = new QuestAcceptedMessage()

            {

                questGiverId = questGiverId,

                questId = selectedAvailableQuestId

            };

            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);



            if (HasQuizConfig(selectedAvailableQuestId))

            {

                TryClose();

                return true;

            }



            if (IsLandClaimQuest(selectedAvailableQuestId))

            {

                curTab = 1;

                selectedActiveQuest = null;

                selectedActiveQuestKey = null;

                availableQuestIds?.Remove(selectedAvailableQuestId);

                RequestRecompose();

                RequestQuestInfoRefresh();

                return true;

            }



            if (closeGuiAfterAcceptingAndCompleting)

            {

                TryClose();

            }

            else

            {

                availableQuestIds?.Remove(selectedAvailableQuestId);

                RequestRecompose();

            }

            return true;

        }



        private bool completeQuest()

        {

            var message = new QuestCompletedMessage()

            {

                questGiverId = questGiverId,

                questId = selectedActiveQuest.questId

            };

            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);

            if (closeGuiAfterAcceptingAndCompleting)

            {

                TryClose();

            }

            else

            {

                activeQuests.RemoveAll(quest => quest != null && selectedActiveQuest != null && quest.questId == selectedActiveQuest.questId && quest.questGiverId == selectedActiveQuest.questGiverId);

                RequestRecompose();

            }

            return true;

        }



        private void onAvailableQuestSelectionChanged(string questId, bool selected)

        {

            if (selected)

            {

                selectedAvailableQuestId = questId;

                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());

                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);



                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

                OnNewScrollbarvalue(0);



                capi.Event.EnqueueMainThreadTask(() =>

                {

                    CloseOpenedDropDown();

                    RequestRecompose();

                }, "alegacyvsquest-availablequestchanged");

            }

        }



        private void onActiveQuestSelectionChanged(string questId, bool selected)

        {

            if (selected)

            {

                selectedActiveQuestKey = questId;

                selectedActiveQuest = activeQuests.Find(quest => ActiveQuestKey(quest) == selectedActiveQuestKey);



                if (selectedActiveQuest == null)

                {

                    return;

                }



                SingleComposer.GetRichtext("questtext").SetNewText(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText());

                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);

                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

                OnNewScrollbarvalue(0);



                capi.Event.EnqueueMainThreadTask(() =>

                {

                    CloseOpenedDropDown();

                    RequestRecompose();

                }, "alegacyvsquest-activequestchanged");

            }

        }



        public void UpdateFromMessage(QuestInfoMessage message)

        {

            if (message == null) return;



            CloseOpenedDropDown();

            ApplyData(message);



            RequestRecompose();

        }



        private void CloseOpenedDropDown()

        {

            var dropdown = SingleComposer?.GetDropDown(DropDownKey);

            if (dropdown?.listMenu?.IsOpened == true)

            {

                try

                {

                    MethodInfo closeMethod = dropdown.listMenu.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    closeMethod?.Invoke(dropdown.listMenu, null);

                }

                catch

                {

                }



                dropdown.listMenu.OnFocusLost();

                dropdown.OnFocusLost();

                SingleComposer?.UnfocusOwnElements();

            }

        }



        public override void OnMouseDown(MouseEvent args)

        {

            var dropdown = SingleComposer?.GetDropDown(DropDownKey);

            bool clickInsideDropdown = dropdown != null && dropdown.IsPositionInside(args.X, args.Y);

            bool clickInsideListMenu = dropdown?.listMenu?.IsOpened == true && dropdown.listMenu.Bounds?.PointInside(args.X, args.Y) == true;



            if (dropdown?.listMenu?.IsOpened == true && !clickInsideDropdown && !clickInsideListMenu)

            {

                capi.Event.EnqueueMainThreadTask(CloseOpenedDropDown, "alegacyvsquest-close-dropdown-deferred");

            }



            base.OnMouseDown(args);

        }



        public override void OnGuiClosed()

        {

            CloseOpenedDropDown();

            base.OnGuiClosed();

        }

    }

}
