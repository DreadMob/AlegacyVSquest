using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Base class for objective trackers with common functionality.
    /// Specific trackers should inherit and override relevant event handlers.
    /// </summary>
    public abstract class BaseObjectiveTracker : IObjectiveTracker
    {
        protected int count;
        protected readonly int demand;
        protected readonly List<string> validCodes;
        
        public abstract string ObjectiveType { get; }

        public virtual bool IsComplete => count >= demand;
        public virtual int CurrentProgress => count;
        public int RequiredProgress => demand;
        
        protected BaseObjectiveTracker(int demand, List<string> validCodes)
        {
            this.demand = demand;
            this.validCodes = validCodes ?? new List<string>();
            this.count = 0;
        }
        
        /// <summary>
        /// Check if a code matches any of the valid codes (supports wildcards)
        /// </summary>
        protected bool CodeMatches(string code)
        {
            if (validCodes == null || validCodes.Count == 0) return true;
            
            foreach (var candidate in validCodes)
            {
                if (LocalizationUtils.MobCodeMatches(candidate, code))
                    return true;
                
                if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                    return true;
            }
            return false;
        }
        
        public virtual void OnEntityKilled(string entityCode, IPlayer byPlayer) { }
        public virtual void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer) { }
        public virtual void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer) { }
        public virtual void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer) { }
        public virtual void OnInventoryChanged(IPlayer player) { }
        
        public virtual void Reset()
        {
            count = 0;
        }
        
        public virtual List<int> GetProgress()
        {
            return new List<int> { count, demand };
        }
    }
}
