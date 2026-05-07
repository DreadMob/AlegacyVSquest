using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Wraps ActionObjectiveBase implementations in the IObjectiveTracker interface.
    /// Delegates completion checking to the underlying action objective.
    /// </summary>
    public class ActionObjectiveTracker : IObjectiveTracker
    {
        private readonly ActionObjectiveBase implementation;
        private readonly string[] args;
        private int lastCurrent;
        private int lastRequired;
        private bool lastIsComplete;
        
        public string ObjectiveType => implementation?.GetType().Name?.Replace("Objective", "").ToLower() ?? "action";
        
        public bool IsComplete => lastIsComplete;
        public int CurrentProgress => lastCurrent;
        public int RequiredProgress => lastRequired;
        
        public ActionObjectiveTracker(ActionObjectiveBase implementation, string[] args)
        {
            this.implementation = implementation;
            this.args = args ?? new string[0];
            lastCurrent = 0;
            lastRequired = 1;
            lastIsComplete = false;
        }
        
        /// <summary>
        /// Check if this action objective is completable for the player
        /// </summary>
        public bool IsCompletable(IPlayer player)
        {
            lastIsComplete = implementation?.IsCompletable(player, args) ?? false;
            var sapi = player.Entity.Api as ICoreServerAPI;
            sapi?.Logger.Debug($"[ActionObjectiveTracker] {implementation?.GetType().Name} IsCompletable: {lastIsComplete}");
            return lastIsComplete;
        }
        
        /// <summary>
        /// Get progress from the underlying action objective
        /// </summary>
        public List<int> GetProgress(IPlayer player)
        {
            var progress = implementation?.GetProgress(player, args) ?? new List<int> { 0, 1 };

            if (progress != null && progress.Count >= 2)
            {
                lastCurrent = progress[0];
                lastRequired = progress[1];
            }

            // Keep completion in sync with the implementation.
            lastIsComplete = implementation?.IsCompletable(player, args) ?? false;

            return progress;
        }
        
        // Action objectives typically don't respond to events
        public void OnEntityKilled(string entityCode, IPlayer byPlayer) { }
        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer) { }
        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer) { }
        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer) { }
        public void OnInventoryChanged(IPlayer player) { }
        public void Reset()
        {
            lastCurrent = 0;
            lastRequired = 1;
            lastIsComplete = false;
        }
        
        public List<int> GetProgress()
        {
            return new List<int> { lastCurrent, lastRequired };
        }
    }
}
