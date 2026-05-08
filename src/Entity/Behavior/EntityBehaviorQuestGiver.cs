using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Entity behavior that allows an NPC to offer quests to players.
    /// Coordinates quest selection, eligibility checking, and message building using dedicated services.
    /// </summary>
    public class EntityBehaviorQuestGiver : EntityBehavior
    {
        private string[] quests;
        private string[] alwaysQuests;
        private string[] rotationPool;
        private string[] excludeQuests;
        private string[] excludeQuestPrefixes;
        private bool selectRandom;
        private int selectRandomCount;
        private int rotationDays;
        private int rotationCount;
        private bool ignorePredecessors;
        private bool allQuests;
        private bool singleQuestAtATime;
        private int chainCooldownDays;
        private int maxAvailableQuests;
        private string[] priorityQuests;
        private string noAvailableQuestDescLangKey;
        private string noAvailableQuestCooldownDescLangKey;
        private bool bossHuntActiveOnly;
        private string reputationNpcId;
        private string reputationFactionId;

        // Services (created on demand)
        private QuestSelectionService _selectionService;
        private QuestEligibilityChecker _eligibilityChecker;
        private QuestGiverMessageBuilder _messageBuilder;

        public static string ChainCooldownLastCompletedKey(long questGiverEntityId) => QuestGiverConstants.ChainCooldownKey(questGiverEntityId);

        public string ReputationNpcId => reputationNpcId;
        public string ReputationFactionId => reputationFactionId;

        public EntityBehaviorQuestGiver(Entity entity) : base(entity)
        {
        }

        private QuestSelectionService SelectionService
        {
            get
            {
                if (_selectionService == null)
                {
                    _selectionService = new QuestSelectionService(
                        quests, alwaysQuests, rotationPool, excludeQuests, excludeQuestPrefixes,
                        selectRandom, selectRandomCount, rotationDays, rotationCount,
                        allQuests, bossHuntActiveOnly, entity.EntityId);
                }
                return _selectionService;
            }
        }

        private QuestEligibilityChecker GetEligibilityChecker(ICoreServerAPI sapi)
        {
            return _eligibilityChecker ??= new QuestEligibilityChecker(sapi);
        }

        private QuestGiverMessageBuilder GetMessageBuilder(ICoreServerAPI sapi)
        {
            return _messageBuilder ??= new QuestGiverMessageBuilder(sapi);
        }

        public bool IsQuestCurrentlyRelevant(ICoreServerAPI sapi, string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return false;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            return SelectionService.IsQuestCurrentlyRelevant(sapi, questId, questSystem.QuestRegistry);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            selectRandom = attributes["selectrandom"].AsBool();
            selectRandomCount = attributes["selectrandomcount"].AsInt(1);
            rotationDays = attributes["rotationdays"].AsInt(0);
            rotationCount = attributes["rotationcount"].AsInt(1);
            ignorePredecessors = attributes["ignorepredecessors"].AsBool(false);
            allQuests = attributes["allquests"].AsBool(false);
            singleQuestAtATime = attributes["singlequestatatime"].AsBool(false);
            chainCooldownDays = attributes["chaincooldowndays"].AsInt(0);
            maxAvailableQuests = attributes["maxavailablequests"].AsInt(0);
            priorityQuests = attributes["priorityquests"].AsArray<string>() ?? Array.Empty<string>();
            bossHuntActiveOnly = attributes["bosshuntactiveonly"].AsBool(false);

            quests = attributes["quests"].AsArray<string>() ?? Array.Empty<string>();
            alwaysQuests = attributes["alwaysquests"].AsArray<string>() ?? Array.Empty<string>();
            rotationPool = attributes["rotationpool"].AsArray<string>();
            excludeQuests = attributes["excludequests"].AsArray<string>() ?? Array.Empty<string>();
            excludeQuestPrefixes = attributes["excludequestprefixes"].AsArray<string>() ?? Array.Empty<string>();
            noAvailableQuestDescLangKey = attributes["noAvailableQuestDescLangKey"].AsString(null);
            noAvailableQuestCooldownDescLangKey = attributes["noAvailableQuestCooldownDescLangKey"].AsString(null);
            reputationNpcId = attributes["reputationnpc"].AsString(null);
            reputationFactionId = attributes["reputationfaction"].AsString(null);

            if (selectRandom)
            {
                int seed = unchecked((int)entity.EntityId);
                var questList = new List<string>(quests);
                var resultList = new List<string>();
                for (int i = 0; i < Math.Min(selectRandomCount, quests.Length); i++)
                {
                    seed = (seed * 5 + 7) % questList.Count;
                    resultList.Add(questList[seed]);
                    questList.RemoveAt(seed);
                }
                quests = resultList.ToArray();
            }
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            var bh = entity.GetBehavior<EntityBehaviorConversable>();
            if (bh != null)
            {
                bh.OnControllerCreated += (controller) =>
                {
                    controller.DialogTriggers += Dialog_DialogTriggers;
                };
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (damageSource != null)
            {
                damageSource.KnockbackStrength = 0f;
            }

            damage = 0f;
        }

        private int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (value == QuestGiverConstants.DialogTriggerOpenQuests && triggeringEntity.Api is ICoreServerAPI sapi)
            {
                var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
                behaviorConversable.Dialog?.TryClose();
                
                SendQuestInfoMessageToClient(sapi, triggeringEntity as EntityPlayer);
                return 0;
            }

            if (value == "openrerolldialog" && triggeringEntity.Api is ICoreServerAPI sapiReroll)
            {
                var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
                behaviorConversable.Dialog?.TryClose();

                var serverPlayer = (triggeringEntity as EntityPlayer)?.Player as IServerPlayer;
                if (serverPlayer == null) return 0;

                // Execute the openrerolldialog action
                var questSystem = sapiReroll.ModLoader.GetModSystem<QuestSystem>();
                var message = new QuestAcceptedMessage { questId = "dialog-action" };
                
                if (questSystem.ActionRegistry.TryGetValue("openrerolldialog", out var action))
                {
                    action.Execute(sapiReroll, message, serverPlayer, new string[0]);
                }
                return 0;
            }

            if (value == QuestGiverConstants.DialogTriggerOpenServerInfo && triggeringEntity.Api is ICoreServerAPI sapi2)
            {
                var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
                behaviorConversable.Dialog?.TryClose();

                var serverPlayer = (triggeringEntity as EntityPlayer)?.Player as IServerPlayer;
                if (serverPlayer == null) return 0;

                sapi2.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName)
                    .SendPacket(new ShowServerInfoMessage(), serverPlayer);
                return 0;
            }

            return -1;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (entity.Alive
                && entity.Api is ICoreServerAPI sapi
                && byEntity is EntityPlayer player
                && mode == EnumInteractMode.Interact
                && player.Controls.Sneak
                && !entity.HasBehavior<EntityBehaviorConversable>())
            {
                SendQuestInfoMessageToClient(sapi, player);
            }
        }

        public void SendQuestInfoMessageToClient(ICoreServerAPI sapi, EntityPlayer player, bool silentUpdate = false)
        {
            if (player == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            var allActiveQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            
            var allQuestIds = SelectionService.BuildAllQuestIds(questSystem.QuestRegistry);

            var eligibilityChecker = GetEligibilityChecker(sapi);
            var completedQuests = eligibilityChecker.GetCompletedQuestIds(player.Player);

            var activeQuests = allActiveQuests
                .Where(activeQuest => allQuestIds.Contains(activeQuest.questId))
                .Select(aq =>
                {
                    var quest = questSystem.QuestRegistry.TryGetValue(aq.questId, out var q) ? q : null;
                    aq.EnsureInitialized(player.Player);
                    aq.ClientState.IsCompletableOnClient = aq.IsCompletable(player.Player);
                    aq.ClientState.IsCurrentStageCompleteOnClient = quest?.HasStages == true && aq.IsCurrentStageCompletable(player.Player, quest);
                    aq.ClientState.ProgressText = Systems.Management.ProgressTextFormatter.GetActiveQuestText(sapi, player.Player, aq);
                    return ActiveQuestDto.FromDomain(aq);
                })
                .ToList();

            var serverPlayer = player.Player as IServerPlayer;
            var messageBuilder = GetMessageBuilder(sapi);

            var availableQuestIds = new List<string>();
            int? minCooldownDaysLeft = null;
            int rotationDaysLeft = SelectionService.GetRotationDaysLeft(sapi);

            // Optional chain cooldown: after completing any quest for this questgiver,
            // block offering any new quests for N days (independent of per-quest cooldown).
            if (chainCooldownDays > 0 && player?.WatchedAttributes != null)
            {
                var chainResult = eligibilityChecker.CheckChainCooldown(entity.EntityId, chainCooldownDays, serverPlayer);
                if (chainResult.isOnCooldown)
                {
                    var msgChainCd = messageBuilder.CreateBaseMessage(
                        entity.EntityId, availableQuestIds, activeQuests,
                        noAvailableQuestDescLangKey, noAvailableQuestCooldownDescLangKey,
                        chainResult.daysLeft, rotationDaysLeft);
                    msgChainCd.silentUpdate = silentUpdate;
                    messageBuilder.PopulateReputationInfo(msgChainCd, serverPlayer, reputationNpcId, reputationFactionId);
                    messageBuilder.SendMessage(msgChainCd, serverPlayer);
                    return;
                }
            }

            // If the player already has any active quest from this questgiver's quest set,
            // do not offer additional quests until the active one is completed.
            // (Innkeeper design: at most one quest in progress at a time.)
            if (singleQuestAtATime && activeQuests != null && activeQuests.Count > 0)
            {
                var msgActive = messageBuilder.CreateBaseMessage(
                    entity.EntityId, availableQuestIds, activeQuests,
                    noAvailableQuestDescLangKey, noAvailableQuestCooldownDescLangKey,
                    minCooldownDaysLeft ?? 0, rotationDaysLeft);
                msgActive.silentUpdate = silentUpdate;
                messageBuilder.PopulateReputationInfo(msgActive, serverPlayer, reputationNpcId, reputationFactionId);
                messageBuilder.SendMessage(msgActive, serverPlayer);
                return;
            }

            var selection = SelectionService.GetCurrentQuestSelection(sapi);

            // Ensure priority quests are evaluated first (e.g. final quests).
            if (priorityQuests != null && priorityQuests.Length > 0)
            {
                var ordered = new List<string>(selection.Count + priorityQuests.Length);
                for (int i = 0; i < priorityQuests.Length; i++)
                {
                    var q = priorityQuests[i];
                    if (string.IsNullOrWhiteSpace(q)) continue;
                    if (SelectionService.IsExcluded(q)) continue;
                    if (!ordered.Contains(q)) ordered.Add(q);
                }

                for (int i = 0; i < selection.Count; i++)
                {
                    var q = selection[i];
                    if (string.IsNullOrWhiteSpace(q)) continue;
                    if (!ordered.Contains(q)) ordered.Add(q);
                }

                selection = ordered;
            }

            bool priorityLocked = false;
            foreach (var questId in selection)
            {
                if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null)
                {
                    sapi.Logger.Warning($"[alegacyvsquest] Quest '{questId}' referenced by questgiver {entity.EntityId} not found in QuestRegistry. Skipping.");
                    continue;
                }

                var activeQuestIds = new HashSet<string>(allActiveQuests.Select(aq => aq.questId), StringComparer.OrdinalIgnoreCase);
                var result = eligibilityChecker.CheckEligibility(quest, serverPlayer, ignorePredecessors, activeQuestIds, completedQuests);

                int offerLimit = maxAvailableQuests > 0 ? maxAvailableQuests : SelectionService.GetOfferLimit();

                if (result.isEligible && !result.isOnCooldown)
                {
                    // If a priority quest becomes available, it should be the only offered quest.
                    if (!priorityLocked && priorityQuests != null && priorityQuests.Length > 0 && Array.IndexOf(priorityQuests, questId) >= 0)
                    {
                        availableQuestIds.Clear();
                        availableQuestIds.Add(questId);
                        priorityLocked = true;
                        break;
                    }

                    if (availableQuestIds.Count < offerLimit)
                    {
                        availableQuestIds.Add(questId);
                    }
                }
                else if (result.isEligible && result.isOnCooldown)
                {
                    if (!minCooldownDaysLeft.HasValue || result.cooldownDaysLeft < minCooldownDaysLeft.Value)
                    {
                        minCooldownDaysLeft = result.cooldownDaysLeft;
                    }
                }
            }

            int cooldownDaysLeft = (availableQuestIds.Count == 0 && minCooldownDaysLeft.HasValue) ? minCooldownDaysLeft.Value : 0;
            var message = messageBuilder.CreateBaseMessage(
                entity.EntityId, availableQuestIds, activeQuests,
                noAvailableQuestDescLangKey, noAvailableQuestCooldownDescLangKey,
                cooldownDaysLeft, rotationDaysLeft);
            message.silentUpdate = silentUpdate;
            messageBuilder.PopulateReputationInfo(message, serverPlayer, reputationNpcId, reputationFactionId);
            messageBuilder.SendMessage(message, serverPlayer);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (entity.Alive && !entity.HasBehavior<EntityBehaviorConversable>())
            {
                return new WorldInteraction[] {
                    new WorldInteraction(){
                        ActionLangCode = QuestGiverConstants.AccessQuestsLangKey,
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }
            else { return base.GetInteractionHelp(world, es, player, ref handled); }
        }

        public override string PropertyName() => "questgiver";
    }
}
