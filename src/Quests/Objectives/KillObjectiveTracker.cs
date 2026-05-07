using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Tracks kill objectives for quests.
    /// Increments counter when matching entities are killed.
    /// </summary>
    public class KillObjectiveTracker : BaseObjectiveTracker
    {
        private readonly string completeSound;
        private readonly float completePitch;
        private readonly float completeVolume;
        
        public override string ObjectiveType => "kill";
        
        public KillObjectiveTracker(
            int demand, 
            List<string> validCodes,
            string completeSound = null,
            float completePitch = 1f,
            float completeVolume = 1f) 
            : base(demand, validCodes)
        {
            this.completeSound = completeSound;
            this.completePitch = completePitch;
            this.completeVolume = completeVolume;
        }
        
        public override void OnEntityKilled(string entityCode, IPlayer byPlayer)
        {
            if (IsComplete) return;
            if (!CodeMatches(entityCode)) return;
            
            count++;
            
            // Play completion sound if just reached target
            if (IsComplete && !string.IsNullOrWhiteSpace(completeSound) && byPlayer is IServerPlayer serverPlayer)
            {
                var sapi = serverPlayer.Entity?.Api as ICoreServerAPI;
                sapi?.World?.PlaySoundFor(
                    new AssetLocation(completeSound), 
                    serverPlayer, 
                    completePitch, 
                    32f, 
                    completeVolume);
            }
        }
    }
}
