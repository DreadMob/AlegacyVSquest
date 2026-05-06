using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public abstract class EntityBehaviorBossCombatMarkerBase : EntityBehaviorBossBase
    {
        protected abstract string AttackersKey { get; }
        protected abstract string DamageByPlayerKey { get; }
        protected abstract string LastDamageMsKey { get; }
        protected virtual bool TrackCombatTime => true;

        public EntityBehaviorBossCombatMarkerBase(Entity entity) : base(entity)
        {
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            EntityPlayer causePlayer = damageSource?.GetCauseEntity() as EntityPlayer;
            bool byPlayerDamage = causePlayer != null;

            var wa = entity.WatchedAttributes;
            if (wa == null) return;

            wa.SetLong(LastDamageMsKey, entity.World.ElapsedMilliseconds);
            wa.MarkPathDirty(LastDamageMsKey);

            if (byPlayerDamage && !string.IsNullOrWhiteSpace(causePlayer.PlayerUID))
            {
                UpdateAttackers(wa, causePlayer.PlayerUID);
                UpdateDamageDealt(wa, causePlayer.PlayerUID, damage);
            }

            if (TrackCombatTime && byPlayerDamage)
            {
                UpdateGlobalCombatTime(wa);
            }
        }

        private void UpdateAttackers(SyncedTreeAttribute wa, string playerUid)
        {
            var existing = wa.GetStringArray(AttackersKey) ?? new string[0];
            bool found = false;
            foreach (var uid in existing)
            {
                if (uid == playerUid)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var merged = new string[existing.Length + 1];
                for (int i = 0; i < existing.Length; i++) merged[i] = existing[i];
                merged[existing.Length] = playerUid;
                wa.SetStringArray(AttackersKey, merged);
                wa.MarkPathDirty(AttackersKey);
            }
        }

        private void UpdateDamageDealt(SyncedTreeAttribute wa, string playerUid, float damage)
        {
            var tree = wa.GetTreeAttribute(DamageByPlayerKey);
            if (tree == null)
            {
                tree = new TreeAttribute();
                wa.SetAttribute(DamageByPlayerKey, tree);
            }

            double prev = tree.GetDouble(playerUid, 0);
            tree.SetDouble(playerUid, prev + damage);
            wa.MarkPathDirty(DamageByPlayerKey);
        }

        private void UpdateGlobalCombatTime(SyncedTreeAttribute wa)
        {
            var calendar = entity.World?.Calendar;
            if (calendar != null)
            {
                wa.SetDouble(BossHuntSystem.LastBossDamageTotalHoursKey, calendar.TotalHours);
                wa.MarkPathDirty(BossHuntSystem.LastBossDamageTotalHoursKey);
            }
        }
    }
}
