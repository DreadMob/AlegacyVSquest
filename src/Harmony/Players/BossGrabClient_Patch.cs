using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony.Players
{
    /// <summary>
    /// Event-driven BossGrab handler. No ticking - reacts only when attribute changes.
    /// </summary>
    [HarmonyPatch(typeof(AnimationManager), "OnReceivedServerAnimations")]
    public class AnimationManager_OnReceivedServerAnimations_Patch
    {
        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";
        private const string BossGrabActiveKey = "alegacyvsquest:bossgrab:active";
        
        // Track active grab per player entity ID
        private static readonly System.Collections.Generic.Dictionary<long, long> ActiveGrabs = new();
        
        // Cached reflection field
        private static readonly FieldInfo EntityField = AccessTools.Field(typeof(AnimationManager), "entity");

        public static void Postfix(AnimationManager __instance)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityAgent_OnGameTick_Unified)) return;
            
            // Get entity via reflection (protected field)
            var entity = EntityField?.GetValue(__instance) as EntityPlayer;
            if (entity == null) return;
            
            if (entity.World?.Side != EnumAppSide.Client) return;
            if (entity.WatchedAttributes == null) return;

            long entityId = entity.EntityId;
            long untilMs = entity.WatchedAttributes.GetLong(BossGrabNoSneakUntilKey, 0);
            bool isActive = entity.WatchedAttributes.GetBool(BossGrabActiveKey, false);
            
            // No active grab - clean up if was tracked
            if (!isActive || untilMs <= 0)
            {
                if (ActiveGrabs.Remove(entityId))
                {
                    // Reset sneak control
                    entity.Controls.Sneak = false;
                }
                return;
            }

            // Check if already handling this grab
            if (ActiveGrabs.TryGetValue(entityId, out var existingUntil) && existingUntil == untilMs)
            {
                return; // Already handling this grab instance
            }

            // New grab started - handle it
            ActiveGrabs[entityId] = untilMs;
            HandleGrab(entity, untilMs);
        }

        private static void HandleGrab(EntityPlayer player, long untilMs)
        {
            long nowMs = 0;
            try { nowMs = player.World.ElapsedMilliseconds; } catch (Exception) { /* Swallow - world may be null */ }
            
            if (nowMs <= 0) return;
            
            int remainingMs = (int)(untilMs - nowMs);
            if (remainingMs <= 0) return;

            // Apply immediately
            player.Controls.Sneak = false;

            // Schedule restore using client-side callback
            if (player.World.Api is ICoreClientAPI capi)
            {
                capi.Event.RegisterCallback((dt) =>
                {
                    // Only restore if still tracked (not overwritten by new grab)
                    if (ActiveGrabs.TryGetValue(player.EntityId, out var trackedUntil) && trackedUntil == untilMs)
                    {
                        ActiveGrabs.Remove(player.EntityId);
                    }
                }, remainingMs);
            }
        }
    }
}
