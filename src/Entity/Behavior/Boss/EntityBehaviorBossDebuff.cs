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
    /// Universal boss debuff ability. Supports multiple debuff types:
    /// - "blind": reduces viewDistance to near-zero
    /// - "disarm": sets alegacyvsquest:disarmed flag (checked by damage system)
    /// - "silence": sets alegacyvsquest:silenced flag (checked by item use system)
    /// Configurable via JSON "debuffType" field.
    /// </summary>
    public class EntityBehaviorBossDebuff : BossAbilityBase
    {
        private const string LastDebuffKey = "alegacyvsquest:bossdebuff:lastMs";
        protected override string CooldownKey => LastDebuffKey;

        private class Stage : BossAbilityStage
        {
            public string debuffType; // "blind", "disarm", "silence"
            public float durationSec;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                debuffType = json["debuffType"].AsString("blind");
                durationSec = json["durationSec"].AsFloat(3f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();

        public EntityBehaviorBossDebuff(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossdebuff";

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            if (target == null) return;

            MarkCooldownStart();

            int durationMs = Math.Max(2000, (int)(stage.durationSec * 1000));

            switch (stage.debuffType.ToLowerInvariant())
            {
                case "blind":
                    ApplyBlind(target, durationMs);
                    break;
                case "disarm":
                    ApplyDisarm(target, durationMs);
                    break;
                case "silence":
                    ApplySilence(target, durationMs);
                    break;
            }

            TryPlaySound(stage.sound, stage.soundRange);
        }

        private void ApplyBlind(EntityPlayer target, int durationMs)
        {
            // Reduce view distance drastically
            target.Stats.Set("rangedWeaponsAcc", "bossblind", -10f, false);
            // Dark particles around player head
            if (Sapi != null)
            {
                ParticleUtils.SpawnEntityAura(Sapi, target, ParticleUtils.Colors.Black, 15, 0.6f, 0.8f);
                entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), target.Pos.X, target.Pos.Y, target.Pos.Z, null, true, 48, 0.4f);
            }

            // Set flag for client-side darkness effect
            target.WatchedAttributes.SetBool("alegacyvsquest:blinded", true);
            target.WatchedAttributes.MarkPathDirty("alegacyvsquest:blinded");

            RegisterCallbackTracked(_ =>
            {
                target?.Stats?.Remove("rangedWeaponsAcc", "bossblind");
                if (target?.WatchedAttributes != null)
                {
                    target.WatchedAttributes.SetBool("alegacyvsquest:blinded", false);
                    target.WatchedAttributes.MarkPathDirty("alegacyvsquest:blinded");
                }
            }, durationMs);
        }

        private void ApplyDisarm(EntityPlayer target, int durationMs)
        {
            target.WatchedAttributes.SetBool("alegacyvsquest:disarmed", true);
            target.WatchedAttributes.MarkPathDirty("alegacyvsquest:disarmed");

            // Red particles on hands
            if (Sapi != null)
            {
                ParticleUtils.SpawnEntityAura(Sapi, target, ParticleUtils.Colors.Blood, 10, 0.4f, 0.5f);
                entity.World.PlaySoundAt(new AssetLocation("game:sounds/block/metalhit"), target.Pos.X, target.Pos.Y, target.Pos.Z, null, true, 40, 0.5f);
            }

            RegisterCallbackTracked(_ =>
            {
                if (target?.WatchedAttributes != null)
                {
                    target.WatchedAttributes.SetBool("alegacyvsquest:disarmed", false);
                    target.WatchedAttributes.MarkPathDirty("alegacyvsquest:disarmed");
                }
            }, durationMs);
        }

        private void ApplySilence(EntityPlayer target, int durationMs)
        {
            target.WatchedAttributes.SetBool("alegacyvsquest:silenced", true);
            target.WatchedAttributes.MarkPathDirty("alegacyvsquest:silenced");

            // Purple particles
            if (Sapi != null)
            {
                ParticleUtils.SpawnEntityAura(Sapi, target, ParticleUtils.Colors.Arcane, 10, 0.5f, 0.6f);
                entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-idle"), target.Pos.X, target.Pos.Y, target.Pos.Z, null, true, 40, 0.4f);
            }

            RegisterCallbackTracked(_ =>
            {
                if (target?.WatchedAttributes != null)
                {
                    target.WatchedAttributes.SetBool("alegacyvsquest:silenced", false);
                    target.WatchedAttributes.MarkPathDirty("alegacyvsquest:silenced");
                }
            }, durationMs);
        }

        protected override void StopAbility() { }
    }
}
