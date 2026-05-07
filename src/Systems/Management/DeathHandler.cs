using System.Collections.Generic;
using VsQuest;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Management
{
    /// <summary>
    /// Service for handling quest-related death events.
    /// Wraps QuestDeathUtil for better organization.
    /// </summary>
    public static class DeathHandler
    {
        /// <summary>
        /// Handle entity death and update quest objectives.
        /// </summary>
        public static void HandleEntityDeath(ICoreServerAPI sapi, List<ActiveQuest> quests, EntityPlayer player, Entity entity)
        {
            QuestDeathUtil.HandleEntityDeath(sapi, quests, player, entity);
        }
    }
}
