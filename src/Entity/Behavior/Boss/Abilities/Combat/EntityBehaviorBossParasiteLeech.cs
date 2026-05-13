using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossParasiteLeech : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossparasiteleech:lastStartMs";
        private const string DebuffUntilKey = "alegacyvsquest:bossparasiteleech:until";
        private const string DebuffStatKey = "alegacyvsquest:bossparasiteleech:stat";

        private class Stage : BossAbilityStage
        {
            public int windupMs;
            public string windupAnimation;

            public int durationMs;
            public int tickIntervalMs;
            public float damagePerTick;
            public int damageTier;
            public string damageType;
            public float healBossPerTick;
            public float healBossRelPerTick;

            public float victimWalkSpeedDelta;
            public float victimHealingDelta;
            public float victimHungerRateDelta;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                windupMs = json["windupMs"].AsInt(350);
                windupAnimation = json["windupAnimation"].AsString(null);
                durationMs = json["durationMs"].AsInt(12000);
                tickIntervalMs = json["tickIntervalMs"].AsInt(1000);
                damagePerTick = json["damagePerTick"].AsFloat(5f);
                damageTier = json["damageTier"].AsInt(0);
                damageType = json["damageType"].AsString("Acid");
                healBossPerTick = json["healBossPerTick"].AsFloat(0f);
                healBossRelPerTick = json["healBossRelPerTick"].AsFloat(0.01f);
                victimWalkSpeedDelta = json["victimWalkSpeedDelta"].AsFloat(-0.35f);
                victimHealingDelta = json["victimHealingDelta"].AsFloat(-0.5f);
                victimHungerRateDelta = json["victimHungerRateDelta"].AsFloat(1.5f);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(24f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);

                // Validation
                if (windupMs < 0) windupMs = 0;
                if (durationMs <= 0) durationMs = 500;
                if (tickIntervalMs < 50) tickIntervalMs = 50;
                if (damagePerTick < 0f) damagePerTick = 0f;
                if (healBossPerTick < 0f) healBossPerTick = 0f;
                if (healBossRelPerTick < 0f) healBossRelPerTick = 0f;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private long callbackId;
        private int activeStageIndex = -1;
        private EntityPlayer target;
        private long nextTickAtMs;
        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        public EntityBehaviorBossParasiteLeech(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossparasiteleech";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not Stage stage) return;
            StartParasiteLeech(stage, stageIndex, target);
        }

        protected override void StopAbility()
        {
            CancelPending();
            StopDebuff();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (IsAbilityActive)
            {
                TickDebuff();
            }
            return IsAbilityActive;
        }

        private void StartParasiteLeech(Stage stage, int stageIndex, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);
            activeStageIndex = stageIndex;

            BossBehaviorUtils.StopAiAndFreeze(entity);
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref callbackId);
            callbackId = Sapi.Event.RegisterCallback(_ =>
            {
                callbackId = 0;
                StartDebuff(stage, stageIndex, target);
            }, delay);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopAbility();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopAbility();
            base.OnEntityDespawn(despawn);
        }

        private void StartDebuff(Stage stage, int stageIndex, EntityPlayer targetPlayer)
        {
            if (Sapi == null || entity == null || stage == null || targetPlayer == null) return;

            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            target = targetPlayer;

            // Parasite attach visual
            ParticleUtils.SpawnEntityAura(Sapi, targetPlayer, ParticleUtils.Colors.Poison, 15, 0.4f, 0.6f);

            target.WatchedAttributes.SetLong(DebuffUntilKey, Sapi.World.ElapsedMilliseconds + Math.Max(250, stage.durationMs));
            target.WatchedAttributes.MarkPathDirty(DebuffUntilKey);

            ApplyDebuffStats(stage, target);
            TryStartLoopSound(stage);

            nextTickAtMs = Sapi.World.ElapsedMilliseconds;
        }

        private void TickDebuff()
        {
            if (Sapi == null || entity == null) return;
            if (target == null || !target.Alive)
            {
                StopDebuff();
                return;
            }

            if (target.Pos.Dimension != entity.Pos.Dimension)
            {
                StopDebuff();
                return;
            }

            if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
            {
                StopDebuff();
                return;
            }

            long until = target.WatchedAttributes.GetLong(DebuffUntilKey, 0);

            if (until <= 0 || Sapi.World.ElapsedMilliseconds >= until)
            {
                StopDebuff();
                return;
            }

            var stage = stages[activeStageIndex];

            ApplyDebuffStats(stage, target);

            long now = Sapi.World.ElapsedMilliseconds;
            if (now < nextTickAtMs) return;

            nextTickAtMs = now + Math.Max(250, stage.tickIntervalMs);

            if (stage.damagePerTick > 0f)
            {
                DealDamage(stage);
            }

            if (stage.healBossPerTick > 0f || stage.healBossRelPerTick > 0f)
            {
                HealBoss(stage);
            }
        }

        private void StopDebuff()
        {
            SetAbilityActive(false);
            loopSoundPlayer.Stop();

            if (target != null)
            {
                target.WatchedAttributes.SetLong(DebuffUntilKey, 0);
                target.WatchedAttributes.MarkPathDirty(DebuffUntilKey);

                ClearDebuffStats(target);
            }

            target = null;
            activeStageIndex = -1;
            nextTickAtMs = 0;
        }

        private void ApplyDebuffStats(Stage stage, EntityPlayer player)
        {
            if (stage == null || player?.Stats == null) return;

            player.Stats.Set("walkspeed", DebuffStatKey, stage.victimWalkSpeedDelta, true);
            player.Stats.Set("healingeffectivness", DebuffStatKey, stage.victimHealingDelta, true);
            player.Stats.Set("hungerrate", DebuffStatKey, stage.victimHungerRateDelta, true);
            BossBehaviorUtils.UpdatePlayerWalkSpeed(player);
        }

        private void ClearDebuffStats(EntityPlayer player)
        {
            if (player?.Stats == null) return;

            player.Stats.Set("walkspeed", DebuffStatKey, 0f, true);
            player.Stats.Set("healingeffectivness", DebuffStatKey, 0f, true);
            player.Stats.Set("hungerrate", DebuffStatKey, 0f, true);
            BossBehaviorUtils.UpdatePlayerWalkSpeed(player);
        }

        private void DealDamage(Stage stage)
        {
            if (stage == null || target == null) return;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            if (!string.IsNullOrWhiteSpace(stage.damageType) && Enum.TryParse(stage.damageType, ignoreCase: true, out EnumDamageType parsed))
            {
                dmgType = parsed;
            }

            target.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = dmgType,
                DamageTier = stage.damageTier,
                KnockbackStrength = 0f
            }, stage.damagePerTick);
        }

        private void HealBoss(Stage stage)
        {
            if (stage == null) return;
            if (!BossBehaviorUtils.TryGetHealth(entity, out var healthTree, out float curHealth, out float maxHealth)) return;

            float abs = stage.healBossPerTick > 0f ? stage.healBossPerTick : 0f;
            float rel = stage.healBossRelPerTick > 0f ? stage.healBossRelPerTick * maxHealth : 0f;
            float heal = abs + rel;
            if (heal <= 0f) return;

            float newHealth = Math.Min(maxHealth, curHealth + heal);
            healthTree.SetFloat("currenthealth", newHealth);
            entity.WatchedAttributes.MarkPathDirty("health");
        }

        private void TryStartLoopSound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(Sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }

        private void CancelPending()
        {
            UnregisterCallbackSafe(ref callbackId);
        }
    }
}
