using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    /// <summary>
    /// Action item action: "Echo of the Void".
    /// Works like bosshunt tracker: instant use, shows distance on screen via ShowDiscoveryMessage,
    /// plays translocate sounds, spawns large purple particle trail (30s).
    /// Costs 1 HP, 3 minute cooldown.
    /// </summary>
    public class TrackTrialBossAction : IQuestAction
    {
        private const float HpCost = 1f;
        private const int TrailDurationSec = 30;
        private const int RefreshIntervalSec = 5;
        private const int CooldownMinutes = 2;
        private const float ParticleVisibilityRange = 20f;

        private const string AttrCooldownUntilMs = "alegacyvsquest:trialtracker:cooldownUntilHours";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player?.Entity == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            // Cooldown check
            double cooldownUntilHours = player.Entity.WatchedAttributes.GetDouble(AttrCooldownUntilMs, 0);
            if (cooldownUntilHours > nowHours)
            {
                double remainingHours = cooldownUntilHours - nowHours;
                int remainingMin = (int)Math.Ceiling(remainingHours * 60.0);
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = LocalizationUtils.GetSafe("albase:trial-tracker-cooldown", remainingMin)
                }, player);
                return;
            }

            // HP check
            var healthBh = player.Entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh != null && healthBh.Health <= HpCost + 0.5f)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = LocalizationUtils.GetSafe("albase:trial-tracker-no-hp")
                }, player);
                return;
            }

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            // Find target: live boss first, then nearest anchor
            Vec3d targetPos = FindTargetPosition(trialSystem, player, sapi);
            if (targetPos == null)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = LocalizationUtils.GetSafe("albase:trial-tracker-no-target")
                }, player);
                return;
            }

            // Take HP
            player.Entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury,
                DamageTier = 0,
                KnockbackStrength = 0f,
                IgnoreInvFrames = true
            }, HpCost);

            // Set cooldown (starts after activation, trail runs concurrently)
            double endHours = nowHours + CooldownMinutes / 60.0;
            player.Entity.WatchedAttributes.SetDouble(AttrCooldownUntilMs, endHours);
            player.Entity.WatchedAttributes.MarkPathDirty(AttrCooldownUntilMs);

            // Calculate distance and show on screen
            double dist = player.Entity.Pos.XYZ.DistanceTo(targetPos);
            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
            {
                Notification = LocalizationUtils.GetSafe("albase:trial-tracker-activated", (int)dist)
            }, player);

            // Schedule periodic trail spawns (pass targetPos directly)
            ScheduleTrailRefreshToPos(sapi, player, trialSystem, targetPos, 0);
        }

        /// <summary>
        /// Find target position: if a live boss of matching tier exists, target it.
        /// Otherwise target nearest anchor of matching tier.
        /// </summary>
        private Vec3d FindTargetPosition(HollowTrialSystem trialSystem, IServerPlayer player, ICoreServerAPI sapi)
        {
            var playerPos = player.Entity.Pos.XYZ;
            int playerTier = GetPlayerQuestTier(player, sapi);

            // First: check if any trial boss of matching tier is alive — target it
            var allConfigs = trialSystem.GetAllConfigs();
            if (allConfigs != null)
            {
                Vec3d nearestBoss = null;
                double nearestDistSq = double.MaxValue;

                foreach (var cfg in allConfigs)
                {
                    if (cfg == null) continue;
                    var bossEntity = trialSystem.GetTrackedEntity(cfg.trialKey);
                    if (bossEntity == null || !bossEntity.Alive) continue;

                    // Check tier matches
                    int bossTier = bossEntity.WatchedAttributes.GetInt("alegacyvsquest:trial:spawnTier", 0);
                    if (playerTier > 0 && bossTier != playerTier) continue;

                    double distSq = playerPos.SquareDistanceTo(bossEntity.Pos.XYZ);
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestBoss = bossEntity.Pos.XYZ;
                    }
                }

                if (nearestBoss != null) return nearestBoss;
            }

            // Fallback: nearest anchor of matching tier
            return trialSystem.FindNearestAnchorPositionByTier(playerPos, playerTier);
        }

        /// <summary>
        /// Get the tier of the player's active trial quest (1, 2, or 3). Returns 0 if none.
        /// </summary>
        private int GetPlayerQuestTier(IServerPlayer player, ICoreServerAPI sapi)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return 0;

            var playerQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (playerQuests == null) return 0;

            foreach (var aq in playerQuests)
            {
                if (aq?.questId == null) continue;
                if (aq.questId.Contains("trial-tier3")) return 3;
                if (aq.questId.Contains("trial-tier2")) return 2;
                if (aq.questId.Contains("trial-tier1")) return 1;
            }
            return 0;
        }

        private void ScheduleTrailRefreshToPos(ICoreServerAPI sapi, IServerPlayer player,
            HollowTrialSystem trialSystem, Vec3d targetPos, int elapsedSec)
        {
            if (elapsedSec >= TrailDurationSec) return;

            SpawnTrailParticles(sapi, player, targetPos);

            // Ambient trail sound
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-idle"),
                player.Entity, player, false, 12f, 0.3f);

            sapi.Event.RegisterCallback(_ =>
            {
                if (player?.Entity == null || !player.Entity.Alive) return;
                if (player.ConnectionState != EnumClientState.Playing) return;

                // Re-resolve target (boss may have spawned or moved)
                Vec3d newTarget = FindTargetPosition(trialSystem, player, sapi);
                if (newTarget == null) return;

                ScheduleTrailRefreshToPos(sapi, player, trialSystem, newTarget, elapsedSec + RefreshIntervalSec);
            }, RefreshIntervalSec * 1000);
        }

        private void SpawnTrailParticles(ICoreServerAPI sapi, IServerPlayer player, Vec3d targetPos)
        {
            var playerPos = player.Entity.Pos.XYZ;

            double dx = targetPos.X - playerPos.X;
            double dz = targetPos.Z - playerPos.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);

            if (dist < 1) return;

            double ndx = dx / dist;
            double ndz = dz / dist;

            double trailLength = Math.Min(ParticleVisibilityRange, dist);

            for (int i = 1; i <= (int)trailLength; i++)
            {
                double px = playerPos.X + ndx * i;
                double py = playerPos.Y + 1.5;
                double pz = playerPos.Z + ndz * i;

                // Large purple/violet particles
                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    minQuantity: 3, maxQuantity: 6,
                    color: ColorUtil.ToRgba(230, 120, 40, 200),
                    minPos: new Vec3d(px - 0.3, py - 0.3, pz - 0.3),
                    maxPos: new Vec3d(px + 0.3, py + 0.3, pz + 0.3),
                    minVelocity: new Vec3f((float)ndx * 0.08f, 0.08f, (float)ndz * 0.08f),
                    maxVelocity: new Vec3f((float)ndx * 0.2f, 0.3f, (float)ndz * 0.2f),
                    lifeLength: 3.0f,
                    gravityEffect: -0.01f,
                    minSize: 0.35f,
                    maxSize: 0.55f
                ));

                // Secondary glow particles
                if (i % 2 == 0)
                {
                    sapi.World.SpawnParticles(new SimpleParticleProperties(
                        minQuantity: 1, maxQuantity: 3,
                        color: ColorUtil.ToRgba(200, 180, 80, 255),
                        minPos: new Vec3d(px - 0.15, py + 0.2, pz - 0.15),
                        maxPos: new Vec3d(px + 0.15, py + 0.5, pz + 0.15),
                        minVelocity: new Vec3f(0, 0.1f, 0),
                        maxVelocity: new Vec3f(0, 0.25f, 0),
                        lifeLength: 2.5f,
                        gravityEffect: -0.02f,
                        minSize: 0.15f,
                        maxSize: 0.25f
                    ));
                }
            }
        }
    }
}
