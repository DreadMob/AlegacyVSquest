using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        
        // New objective tracker system (replaces separate EventTracker lists)
        private List<IObjectiveTracker> objectiveTrackers = new List<IObjectiveTracker>();
        
        // Quest context for lookups - injected via InitializeTrackers
        private IQuestContext questContext;

        // Legacy gather cache for performance
        private Dictionary<int, int> gatherCache = new Dictionary<int, int>();
        private long gatherCacheTimestamp = 0;
        
        // TODO: Remove legacy tracker fields once all quest packs are migrated to the new IObjectiveTracker system.
        // These are still used for ProtoBuf serialization and quest completion checking (CheckObjectivesCompletable, completeQuest).
        [Obsolete("Legacy tracker – use IObjectiveTracker via GetTrackers() instead.")]
        [ProtoMember(100)]
        public List<EventTracker> killTrackers { get; set; } = new List<EventTracker>();
        [Obsolete("Legacy tracker – use IObjectiveTracker via GetTrackers() instead.")]
        [ProtoMember(101)]
        public List<EventTracker> blockPlaceTrackers { get; set; } = new List<EventTracker>();
        [Obsolete("Legacy tracker – use IObjectiveTracker via GetTrackers() instead.")]
        [ProtoMember(102)]
        public List<EventTracker> blockBreakTrackers { get; set; } = new List<EventTracker>();
        [Obsolete("Legacy tracker – use IObjectiveTracker via GetTrackers() instead.")]
        [ProtoMember(103)]
        public List<EventTracker> interactTrackers { get; set; } = new List<EventTracker>();
        
        public bool IsCompletableOnClient { get; set; }
        public bool IsCurrentStageCompleteOnClient { get; set; }
        public string ProgressText { get; set; }

        // Stage system properties
        public int currentStageIndex { get; set; } = 0;
        public List<int> completedStageIndices { get; set; } = new List<int>();

        private const int LastInteractDebounceMs = 100;

        private class LastInteractCache
        {
            public long LastWriteMs;
            public int X;
            public int Y;
            public int Z;
            public int Dim;
        }

        private static readonly SimpleLRUCache<string, LastInteractCache> lastInteractCacheByPlayerUid = new SimpleLRUCache<string, LastInteractCache>(1000, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initialize trackers from quest objectives. Called when quest is accepted or stage advances.
        /// </summary>
        public void InitializeTrackers(IQuestContext context)
        {
            this.questContext = context;
            objectiveTrackers.Clear();
            
            var quest = context?.GetQuest(questId);
            if (quest == null) return;
            
            var stage = quest.GetStage(currentStageIndex);
            
            // Create standard objective trackers
            objectiveTrackers.AddRange(ObjectiveTrackerFactory.CreateTrackersForQuest(quest, currentStageIndex));
            
            // Create action objective trackers
            if (context?.ActionObjectiveRegistry != null)
            {
                var actionObjectives = stage?.actionObjectives ?? quest.actionObjectives;
                if (actionObjectives != null)
                {
                    objectiveTrackers.AddRange(ObjectiveTrackerFactory.CreateActionTrackers(actionObjectives, context.ActionObjectiveRegistry));
                }
            }
            
            // Sync to legacy trackers for serialization compatibility
            SyncToLegacyTrackers(quest);
        }
        
        /// <summary>
        /// Sync new tracker state to legacy EventTrackers for ProtoBuf serialization.
        /// TODO: Remove once all quest packs are migrated and serialization no longer needs EventTracker format.
        /// </summary>
        #pragma warning disable CS0618 // Suppress Obsolete warnings for legacy tracker access
        private void SyncToLegacyTrackers(Quest quest)
        {
            if (quest == null) return;
            
            var stage = quest.GetStage(currentStageIndex);
            
            killTrackers.Clear();
            blockPlaceTrackers.Clear();
            blockBreakTrackers.Clear();
            interactTrackers.Clear();
            
            // Map new trackers to legacy format
            int trackerIndex = 0;
            
            var killObjectives = stage?.killObjectives ?? quest.killObjectives;
            for (int i = 0; i < killObjectives.Count && trackerIndex < objectiveTrackers.Count; i++, trackerIndex++)
            {
                if (objectiveTrackers[trackerIndex] is KillObjectiveTracker kt)
                {
                    killTrackers.Add(new EventTracker { count = kt.CurrentProgress, relevantCodes = killObjectives[i].validCodes });
                }
            }
            
            var blockPlaceObjectives = stage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;
            for (int i = 0; i < blockPlaceObjectives.Count && trackerIndex < objectiveTrackers.Count; i++, trackerIndex++)
            {
                if (objectiveTrackers[trackerIndex] is BlockObjectiveTracker bt && bt.ObjectiveType == "place")
                {
                    blockPlaceTrackers.Add(new EventTracker { count = bt.CurrentProgress, relevantCodes = blockPlaceObjectives[i].validCodes });
                }
            }
            
            var blockBreakObjectives = stage?.blockBreakObjectives ?? quest.blockBreakObjectives;
            for (int i = 0; i < blockBreakObjectives.Count && trackerIndex < objectiveTrackers.Count; i++, trackerIndex++)
            {
                if (objectiveTrackers[trackerIndex] is BlockObjectiveTracker bt && bt.ObjectiveType == "break")
                {
                    blockBreakTrackers.Add(new EventTracker { count = bt.CurrentProgress, relevantCodes = blockBreakObjectives[i].validCodes });
                }
            }
            
            var interactObjectives = stage?.interactObjectives ?? quest.interactObjectives;
            for (int i = 0; i < interactObjectives.Count && trackerIndex < objectiveTrackers.Count; i++, trackerIndex++)
            {
                if (objectiveTrackers[trackerIndex] is BlockObjectiveTracker bt && bt.ObjectiveType == "interact")
                {
                    interactTrackers.Add(new EventTracker { count = bt.CurrentProgress, relevantCodes = interactObjectives[i].validCodes });
                }
            }
        }
        #pragma warning restore CS0618

        /// <summary>
        /// Get all objective trackers
        /// </summary>
        public IReadOnlyList<IObjectiveTracker> GetTrackers() => objectiveTrackers;
        
        private const int GatherCacheValidMs = 5000; // Legacy constant for GatherObjectiveTracker

        private bool TryGetQuestAndStage(IPlayer byPlayer, out QuestSystem questSystem, out Quest quest, out QuestStage stage)
        {
            questSystem = QuestSystemCache.GetFromEntity(byPlayer?.Entity);
            quest = null;
            stage = null;

            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out quest) || quest == null) return false;

            stage = quest.GetStage(currentStageIndex);
            return true;
        }

        public void OnEntityKilled(string entityCode, IPlayer byPlayer, IQuestContext context)
        {
            if (objectiveTrackers.Count == 0) return;
            
            var ctx = context ?? questContext;
            if (ctx == null) return;
            
            var quest = ctx.GetQuest(questId);
            if (quest == null) return;
            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, currentStageIndex, "kill")) return;

            foreach (var tracker in objectiveTrackers)
            {
                if (tracker is KillObjectiveTracker)
                    tracker.OnEntityKilled(entityCode, byPlayer);
            }
            
            SyncToLegacyTrackers(quest);
        }

        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (objectiveTrackers.Count == 0) return;

            var ctx = context ?? questContext;
            if (ctx == null) return;
            
            var quest = ctx.GetQuest(questId);
            if (quest == null) return;
            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, currentStageIndex, "blockplace")) return;

            foreach (var tracker in objectiveTrackers)
            {
                if (tracker is BlockObjectiveTracker bt && bt.ObjectiveType == "place")
                    tracker.OnBlockPlaced(blockCode, position, byPlayer);
            }
            
            SyncToLegacyTrackers(quest);
        }

        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (objectiveTrackers.Count == 0) return;

            var ctx = context ?? questContext;
            if (ctx == null) return;
            
            var quest = ctx.GetQuest(questId);
            if (quest == null) return;
            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, currentStageIndex, "blockbreak")) return;

            foreach (var tracker in objectiveTrackers)
            {
                if (tracker is BlockObjectiveTracker bt && bt.ObjectiveType == "break")
                    tracker.OnBlockBroken(blockCode, position, byPlayer);
            }
            
            SyncToLegacyTrackers(quest);
        }

        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi, IQuestContext context)
        {
            var ctx = context ?? questContext;
            if (ctx == null) return;
            
            var quest = ctx.GetQuest(questId);
            if (quest == null) return;

            // Debounce last interact position tracking
            if (byPlayer?.Entity?.WatchedAttributes != null && position != null && position.Length == 3)
            {
                var wa = byPlayer.Entity.WatchedAttributes;
                int x = position[0];
                int y = position[1];
                int z = position[2];
                int dim = byPlayer.Entity?.Pos?.Dimension ?? 0;

                bool shouldWrite = true;
                try
                {
                    string uid = byPlayer?.PlayerUID;
                    if (!string.IsNullOrWhiteSpace(uid))
                    {
                        if (!lastInteractCacheByPlayerUid.TryGetValue(uid, out var cache) || cache == null)
                        {
                            cache = new LastInteractCache();
                            cache.X = int.MinValue;
                            cache.Y = int.MinValue;
                            cache.Z = int.MinValue;
                            cache.Dim = int.MinValue;
                            lastInteractCacheByPlayerUid.Add(uid, cache);
                        }

                        long now = Environment.TickCount64;
                        if ((now - cache.LastWriteMs) < LastInteractDebounceMs
                            && cache.X == x && cache.Y == y && cache.Z == z && cache.Dim == dim)
                        {
                            shouldWrite = false;
                        }
                        else
                        {
                            cache.LastWriteMs = now;
                            cache.X = x;
                            cache.Y = y;
                            cache.Z = z;
                            cache.Dim = dim;
                        }
                    }
                }
                catch
                {
                    shouldWrite = true;
                }

                if (shouldWrite)
                {
                    if (wa.GetInt("alegacyvsquest:lastinteract:x", int.MinValue) != x)
                        wa.SetInt("alegacyvsquest:lastinteract:x", x);
                    if (wa.GetInt("alegacyvsquest:lastinteract:y", int.MinValue) != y)
                        wa.SetInt("alegacyvsquest:lastinteract:y", y);
                    if (wa.GetInt("alegacyvsquest:lastinteract:z", int.MinValue) != z)
                        wa.SetInt("alegacyvsquest:lastinteract:z", z);
                    if (wa.GetInt("alegacyvsquest:lastinteract:dim", int.MinValue) != dim)
                        wa.SetInt("alegacyvsquest:lastinteract:dim", dim);
                }
            }

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, currentStageIndex, "interact")) return;

            // Dispatch to all block trackers (interact type)
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker is BlockObjectiveTracker bt && bt.ObjectiveType == "interact")
                    tracker.OnBlockUsed(blockCode, position, byPlayer);
            }
            
            // Handle action rewards for interact objectives
            var currentStage = quest.GetStage(currentStageIndex);
            var interactObjectives = currentStage?.interactObjectives ?? quest.interactObjectives;
            var serverPlayer = byPlayer as IServerPlayer;
            
            if (serverPlayer != null)
            {
                QuestInteractAtUtil.TryHandleInteractAtObjectives(quest, this, serverPlayer, position, sapi);
            }
            
            for (int i = 0; i < interactObjectives.Count; i++)
            {
                var objective = interactObjectives[i];
                if (!QuestObjectiveMatchUtil.InteractObjectiveMatches(objective, blockCode, position)) continue;

                if (serverPlayer != null)
                {
                    var message = new QuestAcceptedMessage { questGiverId = questGiverId, questId = questId };
                    foreach (var actionReward in objective.actionRewards)
                    {
                        var action = ctx.GetAction(actionReward.id);
                        if (action != null)
                            action.Execute(sapi, message, serverPlayer, actionReward.args);
                    }
                }
            }
            
            SyncToLegacyTrackers(quest);
        }

        /// <summary>
        /// Gets the current stage for this quest. Returns null if quest not found.
        /// </summary>
        public QuestStage GetCurrentStage(Quest quest)
        {
            if (quest == null) return null;
            return quest.GetStage(currentStageIndex);
        }

        private static void EnsureTrackerListSize(List<EventTracker> list, int size)
        {
            if (list == null) return;

            while (list.Count < size)
            {
                list.Add(new EventTracker());
            }
        }

        /// <summary>
        /// Checks if the current stage is completable (not the entire quest)
        /// </summary>
        public bool IsCurrentStageCompletable(IPlayer byPlayer, Quest quest)
        {
            var stage = GetCurrentStage(quest);
            if (stage == null) return false;

            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.ActionObjectiveRegistry == null) return false;

            var activeActionObjectives = stage.actionObjectives?.ConvertAll<ActionObjectiveBase>(
                objective => questSystem.ActionObjectiveRegistry.TryGetValue(objective.id, out var impl) ? impl : null
            ) ?? new List<ActionObjectiveBase>();

            return CheckObjectivesCompletable(byPlayer, quest, stage, activeActionObjectives);
        }

        /// <summary>
        /// Checks if the entire quest is completable (all stages complete)
        /// </summary>
        public bool IsCompletable(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return false;

            // If quest has stages, check if we're on the final stage and it's complete
            if (quest.HasStages)
            {
                // Quest is completable only when on the last stage and that stage is complete
                if (currentStageIndex < quest.stages.Count - 1)
                {
                    return false;
                }
                return IsCurrentStageCompletable(byPlayer, quest);
            }

            // Legacy quest (no stages) - check objectives directly
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            return CheckObjectivesCompletable(byPlayer, quest, null, activeActionObjectives);
        }

        /// <summary>
        /// Common logic for checking if objectives are completable. Works with both stages and legacy quests.
        /// </summary>
        #pragma warning disable CS0618 // Legacy tracker access
        private bool CheckObjectivesCompletable(IPlayer byPlayer, Quest quest, QuestStage stage, List<ActionObjectiveBase> activeActionObjectives)
        {
            bool completable = true;

            var blockPlaceObjectives = stage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;
            var blockBreakObjectives = stage?.blockBreakObjectives ?? quest.blockBreakObjectives;
            var interactObjectives = stage?.interactObjectives ?? quest.interactObjectives;
            var killObjectives = stage?.killObjectives ?? quest.killObjectives;
            var gatherObjectives = stage?.gatherObjectives ?? quest.gatherObjectives;
            var actionObjectiveDefs = stage?.actionObjectives ?? quest.actionObjectives;

            // Ensure trackers exist for objectives
            EnsureTrackerListSize(blockPlaceTrackers, blockPlaceObjectives.Count);
            EnsureTrackerListSize(blockBreakTrackers, blockBreakObjectives.Count);
            EnsureTrackerListSize(killTrackers, killObjectives.Count);
            EnsureTrackerListSize(interactTrackers, interactObjectives.Count);

            for (int i = 0; i < blockPlaceObjectives.Count; i++)
            {
                if (blockPlaceObjectives[i].positions != null && blockPlaceObjectives[i].positions.Count > 0)
                {
                    completable &= blockPlaceObjectives[i].positions.Count <= blockPlaceTrackers[i].placedPositions.Count;
                }
                else
                {
                    completable &= blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
                }
            }
            for (int i = 0; i < blockBreakObjectives.Count; i++)
            {
                completable &= blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < interactObjectives.Count; i++)
            {
                if (interactObjectives[i].positions != null && interactObjectives[i].positions.Count > 0)
                {
                    int demand = interactObjectives[i].demand > 0 ? interactObjectives[i].demand : interactObjectives[i].positions.Count;
                    completable &= demand <= interactTrackers[i].count;
                }
                else
                {
                    completable &= interactObjectives[i].demand <= interactTrackers[i].count;
                }
            }
            for (int i = 0; i < killObjectives.Count; i++)
            {
                completable &= killObjectives[i].demand <= killTrackers[i].count;
            }
            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                var gatherObjective = gatherObjectives[i];
                int itemsFound = itemsGathered(byPlayer, gatherObjective, i);
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                // Skip gate objectives - they restrict progress timing, not quest completion
                string aoId = actionObjectiveDefs != null && i < actionObjectiveDefs.Count ? actionObjectiveDefs[i]?.id : null;
                if (aoId == "timeofday" || aoId == "landgate") continue;

                var impl = activeActionObjectives[i];
                if (impl == null) continue;

                var defArgs = actionObjectiveDefs != null && i < actionObjectiveDefs.Count ? actionObjectiveDefs[i].args : null;
                completable &= impl.IsCompletable(byPlayer, defArgs);
            }
            return completable;
        }
        #pragma warning restore CS0618

        /// <summary>
        /// Advances to the next stage. Returns true if advanced, false if already on final stage.
        /// </summary>
        public bool AdvanceStage(Quest quest)
        {
            if (quest == null) return false;

            // Mark current stage as completed
            if (!completedStageIndices.Contains(currentStageIndex))
            {
                completedStageIndices.Add(currentStageIndex);
            }

            // Check if there's a next stage
            if (currentStageIndex >= quest.StageCount - 1)
            {
                return false; // Already on final stage
            }

            // Advance to next stage
            currentStageIndex++;

            // Reset trackers for new stage
            foreach (var tracker in objectiveTrackers)
            {
                tracker.Reset();
            }
            gatherCache.Clear();

            return true;
        }

        /// <summary>
        /// Returns true if all stages are complete (quest can be turned in)
        /// </summary>
        public bool AreAllStagesComplete(IPlayer byPlayer, Quest quest)
        {
            if (quest == null) return false;
            if (!quest.HasStages) return IsCompletable(byPlayer);

            // On final stage and it's complete
            return currentStageIndex >= quest.stages.Count - 1 && IsCurrentStageCompletable(byPlayer, quest);
        }

        #pragma warning disable CS0618 // Legacy tracker access
        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;
            var blockPlaceObjectives = currentStage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;

            foreach (var gatherObjective in gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
            for (int i = 0; i < blockPlaceObjectives.Count; i++)
            {
                if (blockPlaceObjectives[i].removeAfterFinished && i < blockPlaceTrackers.Count)
                {
                    int maxRemovals = 100;
                    int removed = 0;
                    foreach (var posStr in blockPlaceTrackers[i].placedPositions)
                    {
                        if (++removed > maxRemovals) break;
                        if (!QuestObjectiveMatchUtil.TryParsePosCached(posStr, out int x, out int y, out int z)) continue;
                        var ba = byPlayer.Entity.World.BlockAccessor;
                        var pos = new Vintagestory.API.MathTools.BlockPos(x, y, z, 0);
                        ba.RemoveBlockEntity(pos);
                        ba.SetBlock(0, pos);
                    }
                }
            }
        }
        #pragma warning restore CS0618

        public List<int> trackerProgress()
        {
            var result = new List<int>();
            foreach (var tracker in objectiveTrackers)
            {
                if (tracker is ActionObjectiveTracker)
                    continue;
                result.Add(tracker.CurrentProgress);
            }
            return result;
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            if (!TryGetQuestAndStage(byPlayer, out _, out var quest, out var currentStage)) return new List<int>();

            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;

            var result = new List<int>();
            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                result.Add(itemsGathered(byPlayer, gatherObjectives[i], i));
            }
            return result;
        }

        public List<int> GetProgress(IPlayer byPlayer)
        {
            if (!TryGetQuestAndStage(byPlayer, out var questSystem, out var quest, out var currentStage)) return new List<int>();

            var actionObjectives = currentStage?.actionObjectives ?? quest.actionObjectives;

            var activeActionObjectives = actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);

            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;

            var result = new List<int>();

            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                result.Add(itemsGathered(byPlayer, gatherObjectives[i], i));
            }

            result.AddRange(trackerProgress());

            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].GetProgress(byPlayer, actionObjectives[i].args));
            }

            return result;
        }

        private int itemsGathered(IPlayer byPlayer, Objective gatherObjective, int objectiveIndex)
        {
            long now = Environment.TickCount64;
            if (now - gatherCacheTimestamp > GatherCacheValidMs)
            {
                gatherCache.Clear();
                gatherCacheTimestamp = now;
            }

            int itemsFound;
            if (gatherCache.TryGetValue(objectiveIndex, out itemsFound)) return itemsFound;
            itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            }
            gatherCache[objectiveIndex] = itemsFound;
            return itemsFound;
        }

        private bool gatherObjectiveMatches(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;

            var stack = slot.Itemstack;
            var code = stack.Collectible.Code.Path;

            foreach (var candidate in gatherObjective.validCodes)
            {
                // Check base item code
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }

                // Check action item ID from attributes
                if (stack.Attributes != null)
                {
                    string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                    if (!string.IsNullOrWhiteSpace(actionItemId) && actionItemId.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public List<string> relevantCodes { get; set; } = new List<string>();
        public int count { get; set; }
        public List<string> placedPositions { get; set; } = new List<string>();
    }
}