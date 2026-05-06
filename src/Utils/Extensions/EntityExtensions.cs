using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsQuest
{
    /// <summary>
    /// Extension methods for Entity-related helpers extracted from BossBehaviorUtils.
    /// These operate on Entity/EntityPlayer instances and provide health queries,
    /// AI control, watched-attribute helpers, and rotation locking.
    /// </summary>
    public static class EntityExtensions
    {
        // --- Boss identification (original methods) ---

        public static bool IsQuestBoss(this Entity entity)
        {
            if (entity == null) return false;

            return entity.HasBehavior<EntityBehaviorBossHuntCombatMarker>()
                || entity.HasBehavior<EntityBehaviorBossCombatMarker>()
                || entity.HasBehavior<EntityBehaviorBossRespawn>()
                || entity.HasBehavior<EntityBehaviorBossDespair>()
                || entity.HasBehavior<EntityBehaviorQuestBoss>();
        }

        public static bool IsFinalBossStage(this Entity entity)
        {
            if (entity == null) return false;
            var rebirth2 = entity.GetBehavior<EntityBehaviorBossRebirth2>();
            return rebirth2 == null || rebirth2.IsFinalStage;
        }

        // --- Health queries (moved from BossBehaviorUtils) ---

        public static bool TryGetHealthFraction(this Entity entity, out float fraction)
        {
            fraction = 1f;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            float curHealth = healthTree.GetFloat("currenthealth", 0f);
            if (maxHealth <= 0f || curHealth <= 0f) return false;

            fraction = curHealth / maxHealth;
            return true;
        }

        public static bool TryGetHealth(this Entity entity, out ITreeAttribute healthTree, out float currentHealth, out float maxHealth)
        {
            healthTree = null;
            currentHealth = 0f;
            maxHealth = 0f;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            currentHealth = healthTree.GetFloat("currenthealth", 0f);
            return maxHealth > 0f && currentHealth > 0f;
        }

        // --- AI / movement control (moved from BossBehaviorUtils) ---

        public static void StopAiAndFreeze(this Entity entity)
        {
            var taskAi = entity?.GetBehavior<EntityBehaviorTaskAI>();
            taskAi?.TaskManager?.StopTasks();

            entity?.Pos?.Motion?.Set(0, 0, 0);
            if (entity is EntityAgent agent)
            {
                agent.Controls.StopAllMovement();
            }
        }

        public static void UnfreezeEntity(this Entity entity)
        {
            // Nothing to do - the entity's TaskAI will resume automatically when tasks are started again
        }

        // --- Watched attribute helpers (moved from BossBehaviorUtils) ---

        public static void SetWatchedBoolDirty(this Entity entity, string key, bool value)
        {
            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            bool prev = wa.GetBool(key, false);
            if (prev == value) return;

            wa.SetBool(key, value);
            wa.MarkPathDirty(key);
        }

        // --- Rotation lock (moved from BossBehaviorUtils) ---

        public static void ApplyRotationLock(this Entity entity, ref bool yawLocked, ref float lockedYaw)
        {
            if (entity == null) return;

            if (!yawLocked)
            {
                lockedYaw = entity.Pos.Yaw;
                yawLocked = true;
            }

            entity.Pos.Yaw = lockedYaw;
            entity.Pos.Yaw = lockedYaw;
            if (entity is EntityAgent agent)
            {
                agent.BodyYaw = lockedYaw;
            }
        }

        // --- Player-specific extensions (moved from BossBehaviorUtils) ---

        /// <summary>
        /// Updates player walkSpeed only if it has changed significantly.
        /// Use this to reduce network sync spam from frequent walkSpeed updates.
        /// </summary>
        /// <param name="player">The player entity</param>
        /// <param name="epsilon">Minimum change threshold (default 0.001)</param>
        /// <returns>True if walkSpeed was updated</returns>
        public static bool UpdatePlayerWalkSpeed(this EntityPlayer player, float epsilon = 0.001f)
        {
            if (player?.Stats == null) return false;

            float targetSpeed = player.Stats.GetBlended("walkspeed");
            if (float.IsNaN(targetSpeed)) targetSpeed = 0f;

            if (Math.Abs(player.walkSpeed - targetSpeed) > epsilon)
            {
                player.walkSpeed = targetSpeed;
                return true;
            }

            return false;
        }
    }
}
