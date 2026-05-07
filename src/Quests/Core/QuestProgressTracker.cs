using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles quest progress tracking with thread-safe operations.
    /// Separates tracking logic from quest data storage.
    /// </summary>
    public class QuestProgressTracker : IDisposable
    {
        private readonly List<IObjectiveTracker> _objectiveTrackers = new List<IObjectiveTracker>();
        private readonly ReaderWriterLockSlim _trackersLock = new ReaderWriterLockSlim();
        private readonly ConcurrentDictionary<int, int> _gatherCache = new ConcurrentDictionary<int, int>();
        private readonly ReaderWriterLockSlim _gatherCacheLock = new ReaderWriterLockSlim();
        private long _gatherCacheTimestamp;
        
        private IQuestContext _questContext;
        private readonly string _questId;
        
        private const int GatherCacheValidMs = 5000;
        
        /// <summary>
        /// Gets the quest ID this tracker is associated with.
        /// </summary>
        public string QuestId => _questId;
        
        /// <summary>
        /// Creates a new quest progress tracker for the specified quest.
        /// </summary>
        /// <param name="questId">The quest ID to track.</param>
        public QuestProgressTracker(string questId)
        {
            _questId = questId;
        }
        
        /// <summary>
        /// Initialize trackers from quest objectives. Called when quest is accepted or stage advances.
        /// </summary>
        /// <param name="context">Quest context for lookups.</param>
        /// <param name="currentStageIndex">Current stage index for multi-stage quests.</param>
        public void InitializeTrackers(IQuestContext context, int currentStageIndex)
        {
            _questContext = context;
            _trackersLock.EnterWriteLock();
            try
            {
                _objectiveTrackers.Clear();
                
                var quest = context?.GetQuest(_questId);
                if (quest == null) return;
                
                var stage = quest.GetStage(currentStageIndex);
                
                // Create standard objective trackers
                _objectiveTrackers.AddRange(ObjectiveTrackerFactory.CreateTrackersForQuest(quest, currentStageIndex));
                
                // Create action objective trackers
                if (context?.ActionObjectiveRegistry != null)
                {
                    var actionObjectives = stage?.actionObjectives ?? quest.actionObjectives;
                    if (actionObjectives != null)
                    {
                        _objectiveTrackers.AddRange(ObjectiveTrackerFactory.CreateActionTrackers(actionObjectives, context.ActionObjectiveRegistry));
                    }
                }
            }
            finally
            {
                _trackersLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Get all objective trackers (thread-safe copy).
        /// </summary>
        public IReadOnlyList<IObjectiveTracker> GetTrackers()
        {
            _trackersLock.EnterReadLock();
            try
            {
                return _objectiveTrackers.ToList().AsReadOnly();
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Dispatch event to matching trackers using predicate.
        /// </summary>
        private void DispatchToTrackers<T>(System.Func<IObjectiveTracker, bool> predicate, Action<T> handler, T arg, IPlayer byPlayer, IQuestContext context, string objectiveType)
        {
            if (byPlayer == null) return;
            
            _trackersLock.EnterReadLock();
            try
            {
                if (_objectiveTrackers.Count == 0) return;
                
                var ctx = context ?? _questContext;
                if (ctx == null) return;
                
                var quest = ctx.GetQuest(_questId);
                if (quest == null) return;
                if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, 0, objectiveType)) return;
                
                foreach (var tracker in _objectiveTrackers)
                {
                    if (predicate(tracker))
                        handler(arg);
                }
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Called when an entity is killed. Updates kill objective trackers.
        /// </summary>
        /// <param name="entityCode">The code of the killed entity.</param>
        /// <param name="byPlayer">The player who killed the entity.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnEntityKilled(string entityCode, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(entityCode)) return;
            
            DispatchToTrackers(
                t => t is IKillTracker,
                (code) => 
                {
                    foreach (var tracker in _objectiveTrackers)
                    {
                        if (tracker is IKillTracker killTracker)
                            killTracker.OnEntityKilled(code, byPlayer);
                    }
                },
                entityCode,
                byPlayer,
                context,
                "kill"
            );
        }
        
        /// <summary>
        /// Called when a block is placed. Updates block place objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the placed block.</param>
        /// <param name="position">The position where the block was placed.</param>
        /// <param name="byPlayer">The player who placed the block.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode)) return;
            
            _trackersLock.EnterReadLock();
            try
            {
                if (_objectiveTrackers.Count == 0) return;
                
                var ctx = context ?? _questContext;
                if (ctx == null) return;
                
                var quest = ctx.GetQuest(_questId);
                if (quest == null) return;
                if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, 0, "blockplace")) return;
                
                foreach (var tracker in _objectiveTrackers)
                {
                    if (tracker is IBlockTracker blockTracker && blockTracker.ObjectiveType == "place")
                        blockTracker.OnBlockPlaced(blockCode, position, byPlayer);
                }
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Called when a block is broken. Updates block break objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the broken block.</param>
        /// <param name="position">The position where the block was broken.</param>
        /// <param name="byPlayer">The player who broke the block.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode)) return;
            
            _trackersLock.EnterReadLock();
            try
            {
                if (_objectiveTrackers.Count == 0) return;
                
                var ctx = context ?? _questContext;
                if (ctx == null) return;
                
                var quest = ctx.GetQuest(_questId);
                if (quest == null) return;
                if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, 0, "blockbreak")) return;
                
                foreach (var tracker in _objectiveTrackers)
                {
                    if (tracker is IBlockTracker blockTracker && blockTracker.ObjectiveType == "break")
                        blockTracker.OnBlockBroken(blockCode, position, byPlayer);
                }
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Called when a block is used (interacted with). Updates interact objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the used block.</param>
        /// <param name="position">The position of the block.</param>
        /// <param name="byPlayer">The player who used the block.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode)) return;
            
            _trackersLock.EnterReadLock();
            try
            {
                if (_objectiveTrackers.Count == 0) return;
                
                var ctx = context ?? _questContext;
                if (ctx == null) return;
                
                var quest = ctx.GetQuest(_questId);
                if (quest == null) return;
                if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, ctx.ActionObjectiveRegistry, 0, "interact")) return;
                
                foreach (var tracker in _objectiveTrackers)
                {
                    if (tracker is IBlockTracker blockTracker && blockTracker.ObjectiveType == "interact")
                        blockTracker.OnBlockUsed(blockCode, position, byPlayer);
                }
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Reset all trackers for a new stage.
        /// </summary>
        public void ResetTrackers()
        {
            _trackersLock.EnterWriteLock();
            try
            {
                foreach (var tracker in _objectiveTrackers)
                {
                    tracker.Reset();
                }
                ClearGatherCache();
            }
            finally
            {
                _trackersLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Get progress of all objective trackers (excluding action objectives).
        /// </summary>
        /// <returns>List of progress values for each tracker.</returns>
        public List<int> GetTrackerProgress()
        {
            _trackersLock.EnterReadLock();
            try
            {
                var result = new List<int>();
                foreach (var tracker in _objectiveTrackers)
                {
                    if (tracker is ActionObjectiveTracker)
                        continue;
                    result.Add(tracker.CurrentProgress);
                }
                return result;
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Export all tracker progress for serialization.
        /// </summary>
        public List<int> ExportProgress()
        {
            _trackersLock.EnterReadLock();
            try
            {
                var result = new List<int>();
                foreach (var tracker in _objectiveTrackers)
                {
                    result.Add(tracker.CurrentProgress);
                }
                return result;
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Restore tracker progress from serialized data.
        /// </summary>
        public void RestoreProgress(List<int> progressData)
        {
            if (progressData == null || progressData.Count == 0) return;
            
            _trackersLock.EnterWriteLock();
            try
            {
                int index = 0;
                foreach (var tracker in _objectiveTrackers)
                {
                    if (index >= progressData.Count) break;
                    if (tracker is BaseObjectiveTracker baseTracker)
                    {
                        baseTracker.SetProgress(progressData[index]);
                    }
                    index++;
                }
            }
            finally
            {
                _trackersLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Get progress of all gather objectives.
        /// </summary>
        /// <param name="byPlayer">The player to check inventory for.</param>
        /// <param name="gatherObjectives">List of gather objectives to check.</param>
        /// <returns>List of item counts for each gather objective.</returns>
        public List<int> GetGatherProgress(IPlayer byPlayer, List<Objective> gatherObjectives)
        {
            if (gatherObjectives == null || gatherObjectives.Count == 0)
                return new List<int>();
            
            var result = new List<int>();
            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                result.Add(GetItemsGathered(byPlayer, gatherObjectives[i], i));
            }
            return result;
        }
        
        /// <summary>
        /// Get count of items gathered for a specific objective.
        /// </summary>
        private int GetItemsGathered(IPlayer byPlayer, Objective gatherObjective, int objectiveIndex)
        {
            _gatherCacheLock.EnterUpgradeableReadLock();
            try
            {
                long now = Environment.TickCount64;
                if (now - _gatherCacheTimestamp > GatherCacheValidMs)
                {
                    _gatherCacheLock.EnterWriteLock();
                    try
                    {
                        if (now - _gatherCacheTimestamp > GatherCacheValidMs)
                        {
                            _gatherCache.Clear();
                            _gatherCacheTimestamp = now;
                        }
                    }
                    finally
                    {
                        _gatherCacheLock.ExitWriteLock();
                    }
                }
                
                if (_gatherCache.TryGetValue(objectiveIndex, out int itemsFound)) 
                    return itemsFound;
                
                _gatherCacheLock.EnterWriteLock();
                try
                {
                    itemsFound = CountItemsInInventory(byPlayer, gatherObjective);
                    _gatherCache[objectiveIndex] = itemsFound;
                    return itemsFound;
                }
                finally
                {
                    _gatherCacheLock.ExitWriteLock();
                }
            }
            finally
            {
                _gatherCacheLock.ExitUpgradeableReadLock();
            }
        }
        
        /// <summary>
        /// Count items matching gather objective in player inventory.
        /// </summary>
        private static int CountItemsInInventory(IPlayer byPlayer, Objective gatherObjective)
        {
            int count = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                    continue;
                    
                foreach (var slot in inventory)
                {
                    if (MatchesGatherObjective(slot, gatherObjective))
                        count += slot.Itemstack.StackSize;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Check if an item slot matches a gather objective.
        /// </summary>
        private static bool MatchesGatherObjective(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;
            
            var stack = slot.Itemstack;
            var code = stack.Collectible.Code.Path;
            
            foreach (var candidate in gatherObjective.validCodes)
            {
                // Check base item code
                if (candidate == code || (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1))))
                    return true;
                
                // Check action item ID from attributes
                if (stack.Attributes != null)
                {
                    string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                    if (!string.IsNullOrWhiteSpace(actionItemId) && actionItemId.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Remove required items from player inventory for a gather objective.
        /// </summary>
        /// <param name="byPlayer">The player to remove items from.</param>
        /// <param name="gatherObjective">The gather objective specifying required items.</param>
        public void HandOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            if (byPlayer == null) return;
            
            ClearGatherCache();
            
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                    continue;
                    
                foreach (var slot in inventory)
                {
                    if (MatchesGatherObjective(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound >= gatherObjective.demand) 
                        return;
                }
            }
        }
        
        /// <summary>
        /// Clear the gather cache (thread-safe).
        /// </summary>
        private void ClearGatherCache()
        {
            _gatherCacheLock.EnterWriteLock();
            try
            {
                _gatherCache.Clear();
                _gatherCacheTimestamp = Environment.TickCount64;
            }
            finally
            {
                _gatherCacheLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Check if all objectives are completable.
        /// </summary>
        public bool CheckObjectivesCompletable(IPlayer byPlayer, Quest quest, QuestStage stage, IQuestContext context)
        {
            if (quest == null || byPlayer == null) return false;
            
            var gatherObjectives = stage?.gatherObjectives ?? quest.gatherObjectives;
            
            _trackersLock.EnterReadLock();
            try
            {
                // Check tracker-based objectives
                foreach (var tracker in _objectiveTrackers)
                {
                    if (tracker is ActionObjectiveTracker)
                        continue;
                    
                    if (tracker.CurrentProgress < tracker.RequiredProgress)
                        return false;
                }
            }
            finally
            {
                _trackersLock.ExitReadLock();
            }
            
            // Check gather objectives
            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                int itemsFound = GetItemsGathered(byPlayer, gatherObjectives[i], i);
                if (itemsFound < gatherObjectives[i].demand)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            _trackersLock?.Dispose();
            _gatherCacheLock?.Dispose();
        }
    }
}
