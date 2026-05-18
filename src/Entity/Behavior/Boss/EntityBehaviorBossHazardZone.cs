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
    /// Universal hazard zone ability (particle-based, no blocks).
    /// Supports multiple zone types:
    /// - "void": reduces view distance + slow (dark purple particles)
    /// - "ember": fire damage + burn DoT after leaving (orange/red particles)
    /// - "corrupt": poison damage + poison DoT after leaving (green particles)
    /// - "frost": slow only, no damage (blue/white particles)
    /// Zones are placed at target position, grow over time, and expire.
    /// </summary>
    public class EntityBehaviorBossHazardZone : BossAbilityBase
    {
        private const string LastZoneKey = "alegacyvsquest:bosshazardzone:lastMs";
        protected override string CooldownKey => LastZoneKey;

        private class Stage : BossAbilityStage
        {
            public string zoneType; // "void", "ember", "corrupt", "frost"
            public float radius;
            public float lifetimeSec;
            public float growDurationSec;
            public float dpsInZone;
            public float dotDps; // DoT after leaving
            public float dotDurationSec;
            public float slowFactor;
            public int maxActiveZones;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                zoneType = json["zoneType"].AsString("ember");
                radius = json["radius"].AsFloat(4f);
                lifetimeSec = json["lifetimeSec"].AsFloat(8f);
                growDurationSec = json["growDurationSec"].AsFloat(2f);
                dpsInZone = json["dpsInZone"].AsFloat(4f);
                dotDps = json["dotDps"].AsFloat(2f);
                dotDurationSec = json["dotDurationSec"].AsFloat(3f);
                slowFactor = json["slowFactor"].AsFloat(0.4f);
                maxActiveZones = json["maxActiveZones"].AsInt(3);
                sound = json["sound"].AsString("effect/translocate-idle");
                soundRange = json["soundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();
        private readonly List<HazardZoneInstance> activeZones = new();
        private readonly Dictionary<string, DotTracker> activeDots = new(StringComparer.OrdinalIgnoreCase);

        public EntityBehaviorBossHazardZone(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosshazardzone";

        protected override bool UsePeriodicTick() => true;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Process active zones
            ProcessZones(dt, nowMs);

            // Process active DoTs
            ProcessDots(dt, nowMs);

            // Try spawn new zone
            if (!entity.Alive || IsBossClone || stages.Count == 0) return;
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;
            if (activeZones.Count >= stage.maxActiveZones) return;
            if (!IsCooldownReady(stageObj)) return;
            if (!TargetingSystem.TryFindTarget(stage.maxTargetRange, 0, out EntityPlayer target, out _)) return;

            MarkCooldownStart();

            activeZones.Add(new HazardZoneInstance
            {
                center = target.Pos.XYZ.Clone(),
                spawnedAtMs = nowMs,
                dim = entity.Pos.Dimension,
                stageIndex = stageIndex
            });

            // Spawn burst on creation
            int color = GetZoneColor(stage.zoneType);
            ParticleUtils.SpawnAuraRing(Sapi, target.Pos.XYZ, 2.5f, color, 24, 0.8f);
            TryPlaySound(stage.sound, stage.soundRange);
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), target.Pos.X, target.Pos.Y, target.Pos.Z, null, true, 48, 0.4f);
        }

        private void ProcessZones(float dt, long nowMs)
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                var zone = activeZones[i];
                int si = zone.stageIndex;
                if (si < 0 || si >= stages.Count) { activeZones.RemoveAt(i); continue; }

                var stage = stages[si];
                double ageSec = (nowMs - zone.spawnedAtMs) / 1000.0;

                if (ageSec >= stage.lifetimeSec) { activeZones.RemoveAt(i); continue; }

                float currentRadius = (float)Math.Min(stage.radius, stage.radius * (ageSec / stage.growDurationSec));
                if (currentRadius < 0.5f) continue;

                // Spawn zone particles
                SpawnZoneParticles(zone.center, currentRadius, stage.zoneType);

                // Process players in zone
                foreach (var p in Sapi.World.AllOnlinePlayers)
                {
                    if (p is not IServerPlayer sp) continue;
                    var pe = sp.Entity;
                    if (pe == null || !pe.Alive) continue;
                    if (pe.Pos.Dimension != zone.dim) continue;

                    double dx = pe.Pos.X - zone.center.X;
                    double dz = pe.Pos.Z - zone.center.Z;
                    bool inZone = dx * dx + dz * dz <= currentRadius * currentRadius;
                    string uid = sp.PlayerUID;

                    if (inZone)
                    {
                        // Apply in-zone effects
                        float dmg = stage.dpsInZone * dt;
                        if (dmg > 0)
                        {
                            pe.ReceiveDamage(new DamageSource
                            {
                                Source = EnumDamageSource.Entity,
                                SourceEntity = entity,
                                Type = GetDamageType(stage.zoneType)
                            }, dmg);

                            entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/projectilehit"), pe.Pos.X, pe.Pos.Y, pe.Pos.Z, null, true, 32, 0.2f);
                        }

                        // Void zone: apply slow
                        if (stage.zoneType == "void" || stage.zoneType == "frost")
                        {
                            pe.Stats.Set("walkspeed", "hazardzone", -stage.slowFactor, false);
                        }

                        // Mark player as in-zone for DoT tracking
                        if (!zone.playersInZone.Contains(uid))
                            zone.playersInZone.Add(uid);
                    }
                    else
                    {
                        // Player left zone — apply DoT if they were in it
                        if (zone.playersInZone.Contains(uid))
                        {
                            zone.playersInZone.Remove(uid);

                            // Remove slow
                            pe.Stats.Remove("walkspeed", "hazardzone");

                            // Start DoT
                            if (stage.dotDps > 0 && stage.dotDurationSec > 0)
                            {
                                activeDots[uid] = new DotTracker
                                {
                                    startMs = nowMs,
                                    durationMs = (long)(stage.dotDurationSec * 1000),
                                    dps = stage.dotDps,
                                    zoneType = stage.zoneType
                                };
                            }
                        }
                    }
                }
            }
        }

        private void ProcessDots(float dt, long nowMs)
        {
            if (activeDots.Count == 0) return;

            var toRemove = new List<string>();
            foreach (var kvp in activeDots)
            {
                var dot = kvp.Value;
                if (nowMs - dot.startMs >= dot.durationMs)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var player = Sapi.World.PlayerByUid(kvp.Key) as IServerPlayer;
                if (player?.Entity == null || !player.Entity.Alive)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                float dmg = dot.dps * dt;
                if (dmg > 0)
                {
                    player.Entity.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = GetDamageType(dot.zoneType)
                    }, dmg);
                }
            }

            foreach (var uid in toRemove) activeDots.Remove(uid);
        }

        private void SpawnZoneParticles(Vec3d center, float radius, string zoneType)
        {
            int color = GetZoneColor(zoneType);
            int count = (int)(radius * 5);
            var rand = Sapi.World.Rand;

            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double r = rand.NextDouble() * radius;
                double x = center.X + Math.Cos(angle) * r;
                double z = center.Z + Math.Sin(angle) * r;

                float vy = zoneType == "ember" ? 0.2f : 0.08f;
                float size = 0.8f + (float)rand.NextDouble() * 0.7f;

                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 3, color,
                    new Vec3d(x - 0.15, center.Y + 0.1, z - 0.15),
                    new Vec3d(x + 0.15, center.Y + 0.5, z + 0.15),
                    new Vec3f(-0.03f, vy * 0.5f, -0.03f),
                    new Vec3f(0.03f, vy, 0.03f),
                    1.2f, -0.01f, size * 0.7f, size
                ));
            }
        }

        private int GetZoneColor(string zoneType)
        {
            return zoneType switch
            {
                "void" => ParticleUtils.Colors.Void,
                "ember" => ParticleUtils.Colors.Fire,
                "corrupt" => ParticleUtils.Colors.Poison,
                "frost" => ParticleUtils.Colors.Ice,
                _ => ParticleUtils.Colors.Shadow
            };
        }

        private EnumDamageType GetDamageType(string zoneType)
        {
            return zoneType switch
            {
                "ember" => EnumDamageType.Fire,
                "corrupt" => EnumDamageType.Poison,
                "frost" => EnumDamageType.Frost,
                _ => EnumDamageType.Injury
            };
        }

        protected override void StopAbility()
        {
            activeZones.Clear();
            activeDots.Clear();
        }

        private class HazardZoneInstance
        {
            public Vec3d center;
            public long spawnedAtMs;
            public int dim;
            public int stageIndex;
            public readonly List<string> playersInZone = new();
        }

        private class DotTracker
        {
            public long startMs;
            public long durationMs;
            public float dps;
            public string zoneType;
        }
    }
}
