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
    /// Boss Trail: leaves a damaging particle trail behind the boss as it moves.
    /// Trail segments persist for a configurable lifetime and deal damage to players who walk through them.
    /// Uses periodic tick to spawn segments and process damage.
    /// </summary>
    public class EntityBehaviorBossTrail : BossAbilityBase
    {
        private const string LastTrailKey = "alegacyvsquest:bosstrail:lastMs";
        protected override string CooldownKey => LastTrailKey;

        private class Stage : BossAbilityStage
        {
            public float trailDps;
            public float trailRadius;
            public float lifetimeSec;
            public int trailIntervalMs;
            public int maxSegments;
            public string trailColor; // "fire", "poison", "shadow", "ice"

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                trailDps = json["trailDps"].AsFloat(4f);
                trailRadius = json["trailRadius"].AsFloat(1.5f);
                lifetimeSec = json["lifetimeSec"].AsFloat(5f);
                trailIntervalMs = json["trailIntervalMs"].AsInt(300);
                maxSegments = json["maxSegments"].AsInt(20);
                trailColor = json["trailColor"].AsString("fire");
            }
        }

        private List<Stage> stages = new();
        private readonly List<TrailSegment> activeSegments = new();
        private Vec3d lastBossPos;
        private long lastSegmentMs;

        public EntityBehaviorBossTrail(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosstrail";

        protected override bool UsePeriodicTick() => true;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => 0;
        protected override float GetMaxTargetRange(object stage) => 0;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;
            if (stages.Count == 0) return;

            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, _) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Spawn new trail segment if boss moved
            Vec3d currentPos = entity.Pos.XYZ.Clone();
            if (lastBossPos != null && currentPos.SquareDistanceTo(lastBossPos) > 0.5)
            {
                if (nowMs - lastSegmentMs >= stage.trailIntervalMs)
                {
                    lastSegmentMs = nowMs;

                    // Remove oldest if at max
                    if (activeSegments.Count >= stage.maxSegments)
                    {
                        activeSegments.RemoveAt(0);
                    }

                    activeSegments.Add(new TrailSegment
                    {
                        position = lastBossPos.Clone(),
                        spawnedAtMs = nowMs,
                        dim = entity.Pos.Dimension
                    });
                }
            }
            lastBossPos = currentPos;

            // Process active segments: particles + damage + cleanup
            int color = GetTrailColor(stage.trailColor);
            EnumDamageType damageType = GetDamageType(stage.trailColor);

            for (int i = activeSegments.Count - 1; i >= 0; i--)
            {
                var seg = activeSegments[i];
                double ageSec = (nowMs - seg.spawnedAtMs) / 1000.0;

                // Remove expired
                if (ageSec >= stage.lifetimeSec)
                {
                    activeSegments.RemoveAt(i);
                    continue;
                }

                // Spawn particles (fade with age)
                float ageRatio = (float)(ageSec / stage.lifetimeSec);
                int alpha = (int)(200 * (1f - ageRatio));
                int fadedColor = ColorUtil.ToRgba(alpha, ColorUtil.ColorR(color), ColorUtil.ColorG(color), ColorUtil.ColorB(color));

                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, fadedColor,
                    seg.position.AddCopy(-0.3, 0.1, -0.3),
                    seg.position.AddCopy(0.3, 0.6, 0.3),
                    new Vec3f(0, 0.05f, 0), new Vec3f(0, 0.15f, 0),
                    0.6f, -0.01f,
                    0.2f + (1f - ageRatio) * 0.3f,
                    0.4f + (1f - ageRatio) * 0.4f
                ));

                // Damage players in segment radius
                float dmg = stage.trailDps * dt;
                if (dmg <= 0) continue;

                foreach (var p in Sapi.World.AllOnlinePlayers)
                {
                    if (p is not IServerPlayer sp) continue;
                    var pe = sp.Entity;
                    if (pe == null || !pe.Alive) continue;
                    if (pe.Pos.Dimension != seg.dim) continue;

                    double dx = pe.Pos.X - seg.position.X;
                    double dz = pe.Pos.Z - seg.position.Z;
                    if (dx * dx + dz * dz <= stage.trailRadius * stage.trailRadius)
                    {
                        pe.ReceiveDamage(new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = damageType
                        }, dmg);

                        entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/projectilehit"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 40, 0.3f);
                    }
                }
            }
        }

        private int GetTrailColor(string colorName)
        {
            return colorName switch
            {
                "fire" => ParticleUtils.Colors.Fire,
                "poison" => ParticleUtils.Colors.Poison,
                "shadow" => ParticleUtils.Colors.Shadow,
                "ice" => ParticleUtils.Colors.Ice,
                "blood" => ParticleUtils.Colors.Blood,
                _ => ParticleUtils.Colors.Fire
            };
        }

        private EnumDamageType GetDamageType(string colorName)
        {
            return colorName switch
            {
                "fire" => EnumDamageType.Fire,
                "poison" => EnumDamageType.Poison,
                "ice" => EnumDamageType.Frost,
                _ => EnumDamageType.Injury
            };
        }

        protected override void StopAbility()
        {
            activeSegments.Clear();
        }

        private class TrailSegment
        {
            public Vec3d position;
            public long spawnedAtMs;
            public int dim;
        }
    }
}
