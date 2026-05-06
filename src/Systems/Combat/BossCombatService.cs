using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossCombatService
    {
        private double bossKillCreditMinShareCeil = 0.5;
        private double bossKillCreditMinShareFloor = 0.08;
        private float bossKillHealFraction = 0.17f;

        public void ApplyConfig(AlegacyVsQuestConfig cfg)
        {
            var combat = cfg?.BossCombat;
            if (combat != null)
            {
                bossKillCreditMinShareCeil = combat.BossKillCreditMinShareCeil;
                bossKillCreditMinShareFloor = combat.BossKillCreditMinShareFloor;
                bossKillHealFraction = combat.BossKillHealFraction;

                if (bossKillCreditMinShareCeil < 0) bossKillCreditMinShareCeil = 0;
                if (bossKillCreditMinShareFloor < 0) bossKillCreditMinShareFloor = 0;
                if (bossKillCreditMinShareFloor > bossKillCreditMinShareCeil) bossKillCreditMinShareFloor = bossKillCreditMinShareCeil;

                if (bossKillHealFraction < 0f) bossKillHealFraction = 0f;
            }
        }

        public bool IsBossEntity(Entity entity) => entity.IsQuestBoss();

        public bool IsFinalBossStage(Entity entity) => entity.IsFinalBossStage();

        public void TryHealBossOnKill(Entity boss)
        {
            if (boss == null || !boss.Alive) return;

            if (!boss.TryGetHealth(out ITreeAttribute healthTree, out float currentHealth, out float maxHealth)) return;
            if (maxHealth <= 0f) return;

            float add = maxHealth * bossKillHealFraction;
            if (add <= 0f) return;

            float next = Math.Min(currentHealth + add, maxHealth);
            healthTree.SetFloat("currenthealth", next);
            boss.WatchedAttributes.MarkPathDirty("health");
        }

        public double GetBossKillCreditMinShare(int attackersWithDamage)
        {
            if (attackersWithDamage <= 1)
            {
                return bossKillCreditMinShareCeil;
            }

            double share = bossKillCreditMinShareCeil - (attackersWithDamage - 1) * 0.05;
            return Math.Max(bossKillCreditMinShareFloor, share);
        }

        public List<string> GetCreditedPlayerUids(Entity entity, double totalDmg, double maxHp)
        {
            var creditedPlayers = new List<string>();
            var wa = entity.WatchedAttributes;
            var dmgTree = wa.GetTreeAttribute(EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey);
            var attackers = wa.GetStringArray(EntityBehaviorBossCombatMarker.BossCombatAttackersKey, new string[0]) ?? new string[0];

            int attackersWithDamage = 0;
            foreach (var uid in attackers)
            {
                if (dmgTree.GetDouble(uid) > 0) attackersWithDamage++;
            }

            double minShare = GetBossKillCreditMinShare(attackersWithDamage);

            foreach (var uid in attackers)
            {
                double dmg = dmgTree.GetDouble(uid);
                if (dmg / maxHp >= minShare || dmg / totalDmg >= minShare)
                {
                    creditedPlayers.Add(uid);
                }
            }

            return creditedPlayers;
        }

        public void AnnounceBossDeath(ICoreServerAPI sapi, Entity entity, List<IServerPlayer> creditedPlayers, DamageSource damageSource)
        {
            if (!IsBossEntity(entity) || !IsFinalBossStage(entity)) return;

            if (creditedPlayers.Count > 0)
            {
                BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, creditedPlayers, entity);
            }
            else if (damageSource?.SourceEntity is EntityPlayer announcePlayer)
            {
                if (announcePlayer.Player is IServerPlayer serverPlayer)
                {
                    BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, serverPlayer, entity);
                }
            }
        }
    }
}
