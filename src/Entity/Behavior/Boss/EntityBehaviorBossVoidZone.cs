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
    /// Void Zone: places an expanding damage zone at target position.
    /// Grows from 0 to maxRadius over growDurationSec. Deals DPS while standing in it.
    /// Uses periodic tick to process active zones.
    /// </summary>
    public class EntityBehaviorBossVoidZone : BossAbilityBase
    {
        private const string LastVoidZoneKey = "alegacyvsquest:bossvoidzone:lastMs";
        protected override string CooldownKey => LastVoidZoneKey;

        private class Stage : BossAbilityStage
        {
            public float maxRadius;
            public float growDurationSec;
            public float dpsInZone;
            public float lifetimeSec;
            public int maxActiveZones;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                maxRadius = json["maxRadius"].AsFloat(6f);
                growDurationSec = json["growDurationSec"].AsFloat(3f);
                dpsInZone = json["dpsInZone"].AsFloat(4f);
                lifetimeSec = json["lifetimeSec"].AsFloat(8f);
                maxActiveZones = json["maxActiveZones"].AsInt(3);
                sound = json["sound"].AsString("effect/translocate-idle");
                soundRange = json["soundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();
        private readonly List<VoidZoneInstance> activeZones = new();

        public EntityBehaviorBossVoidZone(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossvoidzone";

        protected override bool UsePeriodicTick() => true;

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
            if (activeZones.Count >= stage.maxActiveZones) return;

            MarkCooldownStart();

            Vec3d zonePos = target.Pos.XYZ.Clone();

            activeZones.Add(new VoidZoneInstance
            {
                center = zonePos,
                spawnedAtMs = Sapi.World.ElapsedMilliseconds,
                dim = entity.Pos.Dimension,
                stageIndex = stageIndex
            });

            // Spawn initial poison burst on zone creation
            ParticleUtils.SpawnPoisonExplosion(Sapi, zonePos, 1.5f, 1);
            ParticleUtils.SpawnAuraRing(Sapi, zonePos, 1f, ParticleUtils.Colors.Shadow, 10, 0.3f);

            // Sound
            TryPlaySound(stage.sound, stage.soundRange);
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || activeZones.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                var zone = activeZones[i];
                int si = zone.stageIndex;
                if (si < 0 || si >= stages.Count)
                {
                    activeZones.RemoveAt(i);
                    continue;
                }

                var stage = stages[si];
                double ageSec = (nowMs - zone.spawnedAtMs) / 1000.0;

                // Remove expired zones
                if (ageSec >= stage.lifetimeSec)
                {
                    activeZones.RemoveAt(i);
                    continue;
                }

                // Calculate current radius (grows over time)
                float currentRadius = (float)Math.Min(stage.maxRadius, stage.maxRadius * (ageSec / stage.growDurationSec));

                // Spawn aura ring while active
                if (currentRadius >= 1f)
                {
                    ParticleUtils.SpawnAuraRing(Sapi, zone.center, currentRadius, ParticleUtils.Colors.Poison, (int)(currentRadius * 3), 0.25f);
                    ParticleUtils.SpawnFalling(Sapi, zone.center, currentRadius, 2f, ParticleUtils.Colors.Shadow, (int)(currentRadius * 2), 0.2f);
                }

                // Damage players in zone
                DamagePlayersInZone(zone, stage, currentRadius, dt);
            }
        }

        private void DamagePlayersInZone(VoidZoneInstance zone, Stage stage, float currentRadius, float dt)
        {
            float dmg = stage.dpsInZone * dt;
            if (dmg <= 0) return;

            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != zone.dim) continue;

                double dx = pe.Pos.X - zone.center.X;
                double dz = pe.Pos.Z - zone.center.Z;

                if (dx * dx + dz * dz <= currentRadius * currentRadius)
                {
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.Poison
                    }, dmg);
                }
            }
        }

        protected override void StopAbility()
        {
            activeZones.Clear();
        }

        private class VoidZoneInstance
        {
            public Vec3d center;
            public long spawnedAtMs;
            public int dim;
            public int stageIndex;
        }
    }
}
