using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Block objective types
    /// </summary>
    public enum BlockObjectiveType
    {
        Place,
        Break,
        Interact
    }
    
    /// <summary>
    /// Tracks block-related objectives (place, break, interact).
    /// Supports position-locked objectives where specific coordinates must be used.
    /// </summary>
    public class BlockObjectiveTracker : BaseObjectiveTracker
    {
        private readonly BlockObjectiveType objectiveType;
        private readonly List<string> positions;
        private readonly HashSet<string> completedPositions;
        private readonly bool positionLocked;
        
        public override string ObjectiveType => objectiveType.ToString().ToLower();
        
        public BlockObjectiveTracker(
            BlockObjectiveType type,
            int demand,
            List<string> validCodes,
            List<string> positions = null)
            : base(demand, validCodes)
        {
            this.objectiveType = type;
            this.positions = positions ?? new List<string>();
            this.completedPositions = new HashSet<string>();
            this.positionLocked = positions != null && positions.Count > 0;
        }
        
        public override void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (objectiveType != BlockObjectiveType.Place) return;
            HandleBlockEvent(blockCode, position, byPlayer);
        }
        
        public override void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (objectiveType != BlockObjectiveType.Break) return;
            HandleBlockEvent(blockCode, position, byPlayer);
        }
        
        public override void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (objectiveType != BlockObjectiveType.Interact) return;
            HandleBlockEvent(blockCode, position, byPlayer);
        }
        
        private void HandleBlockEvent(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (IsComplete) return;
            
            // Check code matches
            if (!CodeMatches(blockCode)) return;
            
            if (positionLocked)
            {
                // For position-locked objectives, track which positions have been completed
                var posStr = string.Join(",", position);
                
                if (!positions.Contains(posStr)) return;
                if (completedPositions.Contains(posStr)) return;
                
                completedPositions.Add(posStr);
                count = completedPositions.Count;
            }
            else
            {
                // Simple count-based objective
                count++;
            }
        }
        
        public override void Reset()
        {
            base.Reset();
            completedPositions.Clear();
        }
        
        public override List<int> GetProgress()
        {
            if (positionLocked)
            {
                // For position-locked, return completed positions count and total positions
                return new List<int> { completedPositions.Count, positions.Count };
            }
            return base.GetProgress();
        }
    }
}
