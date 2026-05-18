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
    /// Phase Shift: boss teleports to a random position within shiftRadius.
    /// Visual: dissolve particles at old position, reappear particles at new position.
    /// </summary>
    public class EntityBehaviorBossPhaseShift : BossAbilityBase
    {
        private const string LastShiftKey = "alegacyvsquest:bossphaseshift:lastMs";
        protected override string CooldownKey => LastShiftKey;

        private class Stage : BossAbilityStage
        {
            public float shiftRadius;
            public float durationMs;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                shiftRadius = json["shiftRadius"].AsFloat(8f);
                durationMs = json["durationMs"].AsFloat(1500f);
                sound = json["sound"].AsString("block/meteoriciron");
                soundRange = json["soundRange"].AsFloat(16f);
            }
        }

        private List<Stage> stages = new();

        public EntityBehaviorBossPhaseShift(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossphaseshift";

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();

            Vec3d oldPos = entity.Pos.XYZ.Clone();

            // Pick random target position
            var rand = Sapi.World.Rand;
            double angle = rand.NextDouble() * Math.PI * 2;
            double dist = 3 + rand.NextDouble() * (stage.shiftRadius - 3);
            Vec3d newPos = new Vec3d(
                entity.Pos.X + Math.Cos(angle) * dist,
                entity.Pos.Y + 1.0, // +1 block up to avoid getting stuck in terrain
                entity.Pos.Z + Math.Sin(angle) * dist
            );

            // Particles at old position (departure burst)
            ParticleUtils.SpawnShadowExplosion(Sapi, oldPos, 1.5f, 2);
            ParticleUtils.SpawnSpiral(Sapi, oldPos, 1f, 2.5f, ParticleUtils.Colors.Void, 24, 0.3f);
            ParticleUtils.SpawnShockwave(Sapi, oldPos, 2.5f, ParticleUtils.Colors.Shadow, 16, 0.4f);

            // Sound at departure
            TryPlaySound(stage.sound, stage.soundRange, 0, 0.5f);

            // Delayed teleport after durationMs
            RegisterCallbackTracked(_ =>
            {
                if (entity == null || !entity.Alive) return;

                entity.TeleportTo(newPos);

                // Particles at new position (arrival burst)
                ParticleUtils.SpawnShadowExplosion(Sapi, newPos, 1.5f, 2);
                ParticleUtils.SpawnSpiral(Sapi, newPos, 1f, 2.5f, ParticleUtils.Colors.Void, 24, 0.3f);
                ParticleUtils.SpawnShockwave(Sapi, newPos, 2.5f, ParticleUtils.Colors.Shadow, 16, 0.4f);

                // Sound at arrival
                TryPlaySound(stage.sound, stage.soundRange, 0, 0.5f);

                SetAbilityActive(false);
            }, (int)stage.durationMs);
        }

        protected override void StopAbility() { }
    }
}
