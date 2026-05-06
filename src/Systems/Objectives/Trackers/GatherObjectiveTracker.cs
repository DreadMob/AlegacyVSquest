using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Tracks gather objectives for quests.
    /// Counts items in player inventory matching the objective criteria.
    /// </summary>
    public class GatherObjectiveTracker : BaseObjectiveTracker
    {
        private int cachedCount = -1;
        private long cacheTimestamp = 0;
        private const int CacheValidMs = 5000; // 5 seconds
        
        public override string ObjectiveType => "gather";
        
        public GatherObjectiveTracker(int demand, List<string> validCodes) 
            : base(demand, validCodes)
        {
        }
        
        public override bool IsComplete 
        { 
            get 
            { 
                // Gather uses live inventory count, not stored count
                return false; // Actual check done via GetCurrentCount
            } 
        }
        
        public override int CurrentProgress 
        { 
            get 
            {
                // Return cached value if valid
                long now = System.Environment.TickCount64;
                if (cachedCount >= 0 && (now - cacheTimestamp) < CacheValidMs)
                {
                    return cachedCount;
                }
                return 0;
            }
        }
        
        /// <summary>
        /// Get current count from player inventory with caching
        /// </summary>
        public int GetCurrentCount(IPlayer player)
        {
            long now = System.Environment.TickCount64;
            if (cachedCount >= 0 && (now - cacheTimestamp) < CacheValidMs)
            {
                return cachedCount;
            }
            
            if (player?.InventoryManager?.Inventories == null)
            {
                cachedCount = 0;
                cacheTimestamp = now;
                return 0;
            }
            
            int count = 0;
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                    continue;
                
                foreach (var slot in inventory)
                {
                    if (slot?.Itemstack == null) continue;
                    if (ItemMatches(slot.Itemstack))
                    {
                        count += slot.Itemstack.StackSize;
                    }
                }
            }
            
            cachedCount = count;
            cacheTimestamp = now;
            return count;
        }
        
        /// <summary>
        /// Check if item matches gather criteria (code or action item ID)
        /// </summary>
        private bool ItemMatches(ItemStack stack)
        {
            var code = stack.Collectible?.Code?.Path;
            if (string.IsNullOrWhiteSpace(code)) return false;
            
            foreach (var candidate in validCodes)
            {
                // Check base item code
                if (candidate == code || (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1))))
                    return true;
                
                // Check action item ID from attributes
                if (stack.Attributes != null)
                {
                    string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                    if (!string.IsNullOrWhiteSpace(actionItemId) && 
                        actionItemId.Equals(candidate, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        
        public override void OnInventoryChanged(IPlayer player)
        {
            // Invalidate cache when inventory changes
            cachedCount = -1;
        }
        
        public override List<int> GetProgress()
        {
            // Return 0, demand - actual count retrieved via GetCurrentCount
            return new List<int> { 0, demand };
        }
        
        public override void Reset()
        {
            base.Reset();
            cachedCount = -1;
            cacheTimestamp = 0;
        }
    }
}
