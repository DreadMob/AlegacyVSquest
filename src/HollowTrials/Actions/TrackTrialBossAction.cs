using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Action item action: "Echo of the Void".
    /// Creates a sustained particle trail (30 seconds) leading toward the nearest active trial boss
    /// the player has a quest for. Trail refreshes every 5 seconds.
    /// Costs 1 HP, 3 minute cooldown (cooldown starts after the trail ends).
    /// </summary>
    public class TrackTrialBossAction : IQuestAction
    {
        private const float HpCost = 1f;
        private const int TrailDurationSec = 30;
        private const int RefreshIntervalSec = 5;
        private const int CooldownMinutes = 3;
        private const float ParticleVisibilityRange = 20f;

        private const string AttrCooldownUntilMs = "alegacyvsquest:trialtracker:cooldownUntilMs";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player?.Entity == null) return;

            // Cooldown check
            long nowMs = sapi.World.ElapsedMilliseconds;
            long cooldownUntilMs = player.Entity.WatchedAttributes.GetLong(AttrCooldownUntilMs, 0);
            if (cooldownUntilMs > nowMs)
            {
                long remainingMs = cooldownUntilMs - nowMs;
                int remainingMin = (int)Math.Ceiling(remainingMs / 60000.0);
                string msg = LocalizationUtils.GetSafe("albase:trial-tracker-cooldown");
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup,
                    $"{msg} ({remainingMin} мин.)", EnumChatType.Notification);
                return;
            }

            // HP check
            float currentHp = player.Entity.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("currenthealth", 0) ?? 0;
            if (currentHp <= 1f)
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup,
                    LocalizationUtils.GetSafe("albase:trial-tracker-no-hp"), EnumChatType.Notification);
                return;
            }

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            string targetTrialKey = FindNearestQuestTrialKey(trialSystem, player, sapi);
            if (string.IsNullOrWhiteSpace(targetTrialKey))
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup,
                    LocalizationUtils.GetSafe("albase:trial-tracker-no-target"), EnumChatType.Notification);
                return;
            }

            // Take 1 HP
            player.Entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury
            }, HpCost);

            // Set cooldown (starts when trail ends)
            long endMs = nowMs + TrailDurationSec * 1000L + CooldownMinutes * 60L * 1000L;
            player.Entity.WatchedAttributes.SetLong(AttrCooldownUntilMs, endMs);
            player.Entity.WatchedAttributes.MarkPathDirty(AttrCooldownUntilMs);

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup,
                LocalizationUtils.GetSafe("albase:trial-tracker-activated"), EnumChatType.Notification);

            // Schedule periodic trail spawns
            ScheduleTrailRefresh(sapi, player, trialSystem, targetTrialKey, 0);
        }

        private void ScheduleTrailRefresh(ICoreServerAPI sapi, IServerPlayer player,
            HollowTrialSystem trialSystem, string trialKey, int elapsedSec)
        {
            if (elapsedSec >= TrailDurationSec) return;

            // Spawn trail right now
            SpawnTrailNow(sapi, player, trialSystem, trialKey);

            // Schedule next refresh
            sapi.Event.RegisterCallback(_ =>
            {
                if (player?.Entity == null || !player.Entity.Alive) return;
                if (player.ConnectionState != EnumClientState.Playing) return;
                ScheduleTrailRefresh(sapi, player, trialSystem, trialKey, elapsedSec + RefreshIntervalSec);
            }, RefreshIntervalSec * 1000);
        }

        private void SpawnTrailNow(ICoreServerAPI sapi, IServerPlayer player,
            HollowTrialSystem trialSystem, string trialKey)
        {
            var cfg = trialSystem.FindConfig(trialKey);
            if (cfg == null) return;

            var bossEntity = trialSystem.GetTrackedEntity(trialKey);
            Vec3d targetPos;
            if (bossEntity != null && bossEntity.Alive)
            {
                targetPos = bossEntity.Pos.XYZ;
            }
            else
            {
                targetPos = trialSystem.GetAnchorPosition(trialKey);
                if (targetPos == null) return;
            }

            SpawnTrailParticles(sapi, player, targetPos);
        }

        private string FindNearestQuestTrialKey(HollowTrialSystem trialSystem, IServerPlayer player, ICoreServerAPI sapi)
        {
            var activeKeys = trialSystem.GetActiveTrialKeys();
            if (activeKeys == null || activeKeys.Count == 0) return null;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return null;

            string playerUid = player.PlayerUID;
            var playerQuests = questSystem.GetPlayerQuests(playerUid);

            string nearestKey = null;
            double nearestDistSq = double.MaxValue;
            var playerPos = player.Entity.Pos.XYZ;

            foreach (var trialKey in activeKeys)
            {
                var cfg = trialSystem.FindConfig(trialKey);
                if (cfg == null) continue;

                bool hasQuest = false;
                if (playerQuests != null)
                {
                    foreach (var aq in playerQuests)
                    {
                        // Check if player has a quest matching any tier of this boss
                        foreach (var tierKvp in cfg.tiers)
                        {
                            if (string.Equals(aq.questId, tierKvp.Value?.questId, StringComparison.OrdinalIgnoreCase))
                            {
                                hasQuest = true;
                                break;
                            }
                        }
                        if (hasQuest) break;
                    }
                }
                if (!hasQuest) continue;

                Vec3d bossPos;
                var bossEntity = trialSystem.GetTrackedEntity(trialKey);
                if (bossEntity != null && bossEntity.Alive)
                {
                    bossPos = bossEntity.Pos.XYZ;
                }
                else
                {
                    bossPos = trialSystem.GetAnchorPosition(trialKey);
                    if (bossPos == null) continue;
                }

                double distSq = playerPos.SquareDistanceTo(bossPos);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestKey = trialKey;
                }
            }

            return nearestKey;
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

            // Spawn trail up to ParticleVisibilityRange (20 blocks)
            double trailLength = Math.Min(ParticleVisibilityRange, dist);

            for (int i = 1; i <= (int)trailLength; i++)
            {
                double px = playerPos.X + ndx * i;
                double py = playerPos.Y + 1.5;
                double pz = playerPos.Z + ndz * i;

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    minQuantity: 2, maxQuantity: 4,
                    color: ColorUtil.ToRgba(220, 167, 139, 250),
                    minPos: new Vec3d(px - 0.15, py - 0.15, pz - 0.15),
                    maxPos: new Vec3d(px + 0.15, py + 0.15, pz + 0.15),
                    minVelocity: new Vec3f((float)ndx * 0.05f, 0.05f, (float)ndz * 0.05f),
                    maxVelocity: new Vec3f((float)ndx * 0.15f, 0.2f, (float)ndz * 0.15f),
                    lifeLength: 2.0f,
                    gravityEffect: -0.005f,
                    minSize: 0.18f,
                    maxSize: 0.28f
                ));
            }
        }
    }
}
