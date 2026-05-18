using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Boss Mimic Attack: boss enters a mirror state reflecting damage back to attackers.
    /// Visual: ice-blue/crystal particles. Boss still takes reduced damage during mirror.
    /// </summary>
    public class EntityBehaviorBossMimicAttack : BossAbilityBase
    {
        private const string LastMimicKey = "alegacyvsquest:bossmimicattack:lastMs";
        protected override string CooldownKey => LastMimicKey;

        private class Stage : BossAbilityStage
        {
            public float mirrorDurationSec;
            public float reflectPercent; // 0.8 = 80% reflected
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                mirrorDurationSec = json["mirrorDurationSec"].AsFloat(3f);
                reflectPercent = json["reflectPercent"].AsFloat(0.8f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();
        private bool mirrorActive;
        private long mirrorStartMs;
        private int activeStageIndex;

        public EntityBehaviorBossMimicAttack(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossmimicattack";

        protected override bool UsePeriodicTick() => true;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!mirrorActive || damage <= 0 || Sapi == null) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            var stage = stages[activeStageIndex];

            // Find attacker
            var attacker = (damageSource?.SourceEntity ?? damageSource?.CauseEntity) as EntityPlayer;
            if (attacker == null || !attacker.Alive) return;

            // Reflect damage back
            float reflected = damage * stage.reflectPercent;
            if (reflected > 0.5f)
            {
                attacker.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = EnumDamageType.Injury
                }, reflected);

                // Visual: line from boss to attacker
                ParticleUtils.SpawnLine(Sapi, entity.Pos.XYZ.AddCopy(0, 1.5, 0),
                    attacker.Pos.XYZ.AddCopy(0, 1, 0), ParticleUtils.Colors.IceBright, 8, 0.4f);

                entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/reverbhit"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 40, 0.3f);
            }

            // Boss takes reduced damage
            damage *= (1f - stage.reflectPercent);
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;
            if (stages.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            if (mirrorActive)
            {
                if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
                {
                    DeactivateMirror();
                    return;
                }

                var stage = stages[activeStageIndex];
                double elapsed = (nowMs - mirrorStartMs) / 1000.0;

                if (elapsed >= stage.mirrorDurationSec)
                {
                    DeactivateMirror();
                    return;
                }

                // Crystal/mirror particles while active
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.IceBright, 6, 0.5f, 0.7f);
                return;
            }

            // Try to activate mirror
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stg) return;
            if (!CooldownSystem.IsCooldownReady(CooldownKey, stg.cooldownSeconds)) return;

            // Activate
            mirrorActive = true;
            mirrorStartMs = nowMs;
            activeStageIndex = stageIndex;
            MarkCooldownStart();

            entity.WatchedAttributes.SetBool("alegacyvsquest:mirroring", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:mirroring");

            ParticleUtils.SpawnAuraSphere(Sapi, entity.Pos.XYZ, 2f, ParticleUtils.Colors.IceBright, 20, 0.6f);
            TryPlaySound(stg.sound, stg.soundRange);
        }

        private void DeactivateMirror()
        {
            mirrorActive = false;
            entity.WatchedAttributes.SetBool("alegacyvsquest:mirroring", false);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:mirroring");
        }

        protected override void StopAbility()
        {
            DeactivateMirror();
        }
    }
}
