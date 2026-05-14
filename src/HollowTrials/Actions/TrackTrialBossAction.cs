using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Action item action: "Эхо Пустоты" (Echo of the Void).
    /// When used, creates a trail of purple particles leading toward the nearest active trial boss
    /// that the player has a quest for. Costs 1 HP, 3 minute cooldown (handled by action item config).
    /// </summary>
    public class TrackTrialBossAction : IQuestAction
    {
        private const float HpCost = 1f;

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player?.Entity == null) return;

            float currentHp = player.Entity.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("currenthealth", 0) ?? 0;
            if (currentHp <= 1f)
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("albase:trial-tracker-no-hp"), EnumChatType.Notification);
                return;
            }

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            var targetPos = FindNearestQuestBossPosition(trialSystem, player, sapi);
            if (targetPos == null)
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("albase:trial-tracker-no-target"), EnumChatType.Notification);
                return;
            }

            player.Entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury
            }, HpCost);

            SpawnTrailParticles(sapi, player, targetPos);

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("albase:trial-tracker-activated"), EnumChatType.Notification);
        }

        private Vec3d FindNearestQuestBossPosition(HollowTrialSystem trialSystem, IServerPlayer player, ICoreServerAPI sapi)
        {
            var activeKeys = trialSystem.GetActiveTrialKeys();
            if (activeKeys == null || activeKeys.Count == 0) return null;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return null;

            string playerUid = player.PlayerUID;
            var playerQuests = questSystem.GetPlayerQuests(playerUid);

            Vec3d nearest = null;
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
                        if (string.Equals(aq.questId, cfg.questId, StringComparison.OrdinalIgnoreCase))
                        {
                            hasQuest = true;
                            break;
                        }
                    }
                }

                if (!hasQuest) continue;

                var bossEntity = trialSystem.GetTrackedEntity(trialKey);
                Vec3d bossPos;

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
                    nearest = bossPos;
                }
            }

            return nearest;
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

            double trailLength = Math.Min(20, dist);

            for (int i = 1; i <= (int)trailLength; i += 2)
            {
                double px = playerPos.X + ndx * i;
                double py = playerPos.Y + 1.5;
                double pz = playerPos.Z + ndz * i;

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    2, 4, ColorUtil.ToRgba(180, 160, 60, 220),
                    new Vec3d(px - 0.2, py - 0.2, pz - 0.2),
                    new Vec3d(px + 0.2, py + 0.2, pz + 0.2),
                    new Vec3f((float)ndx * 0.1f, 0.05f, (float)ndz * 0.1f),
                    new Vec3f((float)ndx * 0.2f, 0.15f, (float)ndz * 0.2f),
                    2.5f, -0.01f, 0.15f, 0.3f));
            }
        }
    }
}
