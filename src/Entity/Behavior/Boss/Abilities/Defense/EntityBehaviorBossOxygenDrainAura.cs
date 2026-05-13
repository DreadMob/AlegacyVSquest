using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossOxygenDrainAura : BossAbilityBase
    {
        private const string LastTickMsKey = "alegacyvsquest:bossoxygendrainaura:lastTickMs";

        protected override string CooldownKey => "alegacyvsquest:bossoxygendrainaura:lastTickMs";
        protected override bool UsePeriodicTick() => true;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public float range;
            public float oxygenDrainPerSecond;
            public int intervalMs;
            public float minOxygenRel;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                range = json["range"].AsFloat(18f);
                oxygenDrainPerSecond = json["oxygenDrainPerSecond"].AsFloat(0f);
                intervalMs = json["intervalMs"].AsInt(500);
                minOxygenRel = json["minOxygenRel"].AsFloat(0f);

                if (range <= 0f) range = 18f;
                if (intervalMs < 100) intervalMs = 100;
                if (minOxygenRel < 0f) minOxygenRel = 0f;
                if (minOxygenRel > 1f) minOxygenRel = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();

        public EntityBehaviorBossOxygenDrainAura(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossoxygendrainaura";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || entity == null || stages.Count == 0 || !entity.Alive) return;

            if (!TryGetHealthFraction(out float frac)) return;

            (object stageObj, int stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;
            if (!ShouldRunInterval(LastTickMsKey, stage.intervalMs, nowMs)) return;

            if (stage.oxygenDrainPerSecond <= 0f) return;

            // Periodic aura visual
            if (Sapi.World.Rand.NextDouble() < 0.3)
            {
                ParticleUtils.SpawnAuraRing(Sapi, entity.Pos.XYZ.Add(0, 0.3, 0), stage.range * 0.5f, ParticleUtils.Colors.Ice, 8, 0.2f);
            }

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double rangeSq = (stage.range > 0 ? stage.range : 18f) * (stage.range > 0 ? stage.range : 18f);
            var selfPos = entity.Pos;
            float drain = stage.oxygenDrainPerSecond * (stage.intervalMs / 1000f);

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null || playerEntity.Pos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.Pos.X - selfPos.X;
                double dy = playerEntity.Pos.Y - selfPos.Y;
                double dz = playerEntity.Pos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                var breathe = playerEntity.GetBehavior<EntityBehaviorBreathe>();
                if (breathe == null) continue;

                float maxOxygen = breathe.MaxOxygen;
                float minOxygen = maxOxygen * stage.minOxygenRel;
                float current = breathe.Oxygen;
                float newOxygen = GameMath.Clamp(current - drain, minOxygen, maxOxygen);

                if (newOxygen < current)
                {
                    breathe.Oxygen = newOxygen;
                }
            }
        }

        // Required abstract overrides for BossAbilityBase (not used in periodic tick mode)
        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility() { }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
