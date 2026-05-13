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
    public class EntityBehaviorBossLifeDrainNova : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bosslifedrainnova:lastCastMs";

        private class Stage : BossAbilityStage
        {
            public int durationMs;
            public int tickIntervalMs;
            public float drainPerSecond;
            public float healMult;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                durationMs = json["durationMs"].AsInt(5000);
                tickIntervalMs = json["tickIntervalMs"].AsInt(1000);
                drainPerSecond = json["drainPerSecond"].AsFloat(10f);
                healMult = json["healMult"].AsFloat(1.0f);

                // Validation
                if (durationMs <= 0) durationMs = 500;
                if (tickIntervalMs < 50) tickIntervalMs = 50;
                if (drainPerSecond < 0f) drainPerSecond = 0f;
                if (healMult < 0f) healMult = 0f;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private EntityBehaviorHealth healthBehavior;

        private long novaEndMs;
        private long lastTickMs;
        private int activeStageIndex;

        public EntityBehaviorBossLifeDrainNova(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosslifedrainnova";

        protected override void InitializeStages(JsonObject attributes)
        {
            healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override bool RequiresTarget() => false;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            StartNova(stage, stageIndex);
        }

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void StopAbility()
        {
            SetAbilityActive(false);
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            
            long now = Sapi.World.ElapsedMilliseconds;
            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                ProcessActiveNova(stages[activeStageIndex], now);
            }
            
            if (now > novaEndMs)
            {
                return false;
            }
            return true;
        }

        private void StartNova(Stage stage, int stageIndex)
        {
            if (Sapi == null || entity == null || stage == null) return;
            
            MarkCooldownStart();
            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            long now = Sapi.World.ElapsedMilliseconds;
            
            PerformNova(stage, now);
            novaEndMs = now + stage.durationMs;
            lastTickMs = now;
        }

        private void ProcessActiveNova(Stage stage, long now)
        {
            if (!IsAbilityActive || now > novaEndMs)
            {
                return;
            }

            // Process damage tick
            if (now - lastTickMs >= stage.tickIntervalMs)
            {
                lastTickMs = now;
                LaunchNovaTick(stage);
            }
        }

        private void PerformNova(Stage stage, long now)
        {
            // Visual nova ring at start
            SpawnNovaRing(stage);
            // First tick immediately
            LaunchNovaTick(stage);
        }

        private void LaunchNovaTick(Stage stage)
        {
            float totalDamage = 0;
            var damagedPlayers = new List<EntityPlayer>();

            // Find and damage players in range
            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = player.Entity.Pos.DistanceTo(entity.Pos);
                if (dist > stage.maxTargetRange) continue;

                float damage = stage.drainPerSecond * (stage.tickIntervalMs / 1000f);
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    damage
                );

                totalDamage += damage;
                damagedPlayers.Add(player.Entity);
            }

            // Heal boss
            if (totalDamage > 0 && healthBehavior != null)
            {
                float healAmount = totalDamage * stage.healMult;
                healthBehavior.Health = Math.Min(healthBehavior.MaxHealth, healthBehavior.Health + healAmount);

                // Visual heal effect - particles flying from victims to boss
                foreach (var player in damagedPlayers)
                {
                    Vec3d startPos = player.Pos.XYZ.Add(0, 1, 0);
                    Vec3d endPos = entity.Pos.XYZ.Add(0, 1.5, 0);
                    ParticleUtils.SpawnLine(Sapi, startPos, endPos, ParticleUtils.Colors.Blood, 5, 0.3f);
                }
            }

            // Visual nova ring
            SpawnNovaRing(stage);

            // Sound
            Sapi.World.PlaySoundAt(
                new AssetLocation("albase:sounds/mechanical/mecha switch power"),
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z,
                null, true, 32, 0.133f
            );
        }

        private void SpawnNovaRing(Stage stage)
        {
            if (Sapi == null || entity == null) return;
            ParticleUtils.SpawnAuraRing(Sapi, entity.Pos.XYZ.Add(0, 0.5, 0), stage.maxTargetRange, ParticleUtils.Colors.Blood, 24, 0.5f);
        }
    }
}
