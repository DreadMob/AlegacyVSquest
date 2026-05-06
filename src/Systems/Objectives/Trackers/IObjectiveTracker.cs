using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Interface for quest objective trackers.
    /// Implementations handle specific objective types (kill, gather, block, etc.)
    /// </summary>
    public interface IObjectiveTracker
    {
        /// <summary>
        /// Type identifier for this objective (e.g., "kill", "gather", "blockplace")
        /// </summary>
        string ObjectiveType { get; }
        
        /// <summary>
        /// Whether the objective is complete
        /// </summary>
        bool IsComplete { get; }
        
        /// <summary>
        /// Current progress count
        /// </summary>
        int CurrentProgress { get; }
        
        /// <summary>
        /// Required progress to complete
        /// </summary>
        int RequiredProgress { get; }
        
        /// <summary>
        /// Called when an entity is killed
        /// </summary>
        void OnEntityKilled(string entityCode, IPlayer byPlayer);
        
        /// <summary>
        /// Called when a block is placed
        /// </summary>
        void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer);
        
        /// <summary>
        /// Called when a block is broken
        /// </summary>
        void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer);
        
        /// <summary>
        /// Called when a block is used (interacted with)
        /// </summary>
        void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer);
        
        /// <summary>
        /// Called when player inventory changes (for gather objectives)
        /// </summary>
        void OnInventoryChanged(IPlayer player);
        
        /// <summary>
        /// Reset progress (called on stage advancement)
        /// </summary>
        void Reset();
        
        /// <summary>
        /// Get progress as list of integers for serialization/display
        /// </summary>
        List<int> GetProgress();
    }
}
