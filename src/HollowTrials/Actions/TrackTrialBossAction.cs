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
        private const int CooldownMinutes = 3;
        private const float ParticleVisibilityRange = 20f;

        private const string AttrCooldownUntilMs = "alegacyvsquest:trialtracker:cooldownUntilMs";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player?.Entity == null) return;

            long nowMs = sapi.World.ElapsedMilliseconds;

            // Cooldown check
            long cooldownUntilMs = player.Entity.WatchedAttributes.GetLong(AttrCooldownUntilMs, 0);
            if (cooldownUntilMs > nowMs)
            {
                long remainingMs = cooldownUntilMs - nowMs;
                int remainingMin = (int)Math.Ceiling(remainingMs / 60000.0);
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("albase:trial-tracker-cooldown", remainingMin)
                }, player);
                return;
            }

            // HP check
            var healthBh = player.Entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh != null && healthBh.Health <= HpCost + 0.5f)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("albase:trial-tracker-no-hp")
                }, player);
                return;
            }

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            string targetTrialKey = FindNearestQuestTrialKey(trialSystem, player, sapi);
            if (string.IsNullOrWhiteSpace(targetTrialKey))
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("albase:trial-tracker-no-target")
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

            // Play activation sound
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"),
                player.Entity, player, false, 32f, 0.8f);

            // Set cooldown
            long endMs = nowMs + TrailDurationSec * 1000L + CooldownMinutes * 60L * 1000L;
            player.Entity.WatchedAttributes.SetLong(AttrCooldownUntilMs, endMs);
            player.Entity.WatchedAttributes.MarkPathDirty(AttrCooldownUntilMs);

            // Calculate distance and show on screen
            Vec3d bossPos = GetBossPosition(trialSystem, targetTrialKey);
            if (bossPos != null)
            {
                double dist = player.Entity.Pos.XYZ.DistanceTo(bossPos);
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("albase:trial-tracker-activated", (int)dist)
                }, player);
            }

            // Schedule periodic trail spawns
            ScheduleTrailRefresh(sapi, player, trialSystem, targetTrialKey, 0);
        }

        private Vec3d GetBossPosition(HollowTrialSystem trialSystem, string trialKey)
        {
            var bossEntity = trialSystem.GetTrackedEntity(trialKey);
            if (bossEntity != null && bossEntity.Alive)
            {
                return bossEntity.Pos.XYZ;
            }
            return trialSystem.GetAnchorPosition(trialKey);
        }

        private void ScheduleTrailRefresh(ICoreServerAPI sapi, IServerPlayer player,
            HollowTrialSystem trialSystem, string trialKey, int elapsedSec)
        {
            if (elapsedSec >= TrailDurationSec) return;

            SpawnTrailNow(sapi, player, trialSystem, trialKey);

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
            Vec3d targetPos = GetBossPosition(trialSystem, trialKey);
            if (targetPos == null) return;

            SpawnTrailParticles(sapi, player, targetPos);

            // Ambient trail sound
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-idle"),
                player.Entity, player, false, 12f, 0.3f);
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

                Vec3d bossPos = GetBossPosition(trialSystem, trialKey);
                if (bossPos == null) continue;

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
