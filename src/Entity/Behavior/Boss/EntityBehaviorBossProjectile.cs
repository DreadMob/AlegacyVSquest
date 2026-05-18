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
    /// Boss Projectile: fires particle-based projectiles toward players.
    /// Configurable shape (ball, spiral, burst, wave, homing, mortar, beam) and
    /// variant (fire, shadow, ice, poison, lightning, void, blood, chain).
    /// </summary>
    public class EntityBehaviorBossProjectile : BossAbilityBase
    {
        private const string LastProjectileKey = "alegacyvsquest:bossprojectile:lastMs";
        protected override string CooldownKey => LastProjectileKey;

        private class Stage : BossAbilityStage
        {
            public float damage;
            public float speed;
            public float hitRadius;
            public float maxRange;
            public string variant;
            public string shape;
            public int damageTier;
            public int burstCount;
            public int waveCount;
            public float waveSpreadDeg;
            public float homingTurnDegPerSec;
            public string sound;
            public float soundRange;
            public int windupMs;
            public string windupSound;
            public float windupSoundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                damage = json["damage"].AsFloat(10f);
                speed = json["speed"].AsFloat(12f);
                hitRadius = json["hitRadius"].AsFloat(1.4f);
                maxRange = json["maxRange"].AsFloat(30f);
                variant = json["variant"].AsString("fire");
                shape = json["shape"].AsString("ball");
                damageTier = json["damageTier"].AsInt(3);
                burstCount = json["burstCount"].AsInt(3);
                waveCount = json["waveCount"].AsInt(5);
                waveSpreadDeg = json["waveSpreadDeg"].AsFloat(60f);
                homingTurnDegPerSec = json["homingTurnDegPerSec"].AsFloat(20f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(24f);
                windupMs = json["windupMs"].AsInt(0);
                windupSound = json["windupSound"].AsString(null);
                windupSoundRange = json["windupSoundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();
        private readonly List<Projectile> activeProjectiles = new();
        private int burstRemaining;
        private long lastBurstMs;
        private int burstStageIndex;

        // Windup state
        private bool isWindingUp;
        private long windupStartMs;
        private int windupStageIndex;
        private long windupTargetEntityId;

        public EntityBehaviorBossProjectile(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossprojectile";

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
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Process active projectiles
            ProcessProjectiles(dt, nowMs);

            // Process windup (delayed fire)
            if (isWindingUp)
            {
                if (nowMs - windupStartMs >= stages[windupStageIndex].windupMs)
                {
                    isWindingUp = false;
                    var wStage = stages[windupStageIndex];

                    // Find target (prefer original, fallback to nearest)
                    EntityPlayer wTarget = null;
                    if (windupTargetEntityId > 0)
                    {
                        var e = Sapi.World.GetEntityById(windupTargetEntityId);
                        if (e is EntityPlayer ep && ep.Alive) wTarget = ep;
                    }
                    if (wTarget == null)
                        TargetingSystem.TryFindTarget(wStage.maxTargetRange, 0, out wTarget, out _);

                    if (wTarget != null)
                    {
                        TryPlaySound(wStage.sound, wStage.soundRange);
                        ExecuteFire(wStage, windupStageIndex, wTarget, nowMs);
                    }
                }
                else
                {
                    // Spawn windup particles (charging effect at boss position)
                    SpawnWindupParticles(stages[windupStageIndex]);
                }
                return; // Don't fire new projectiles while winding up
            }

            // Process burst queue
            if (burstRemaining > 0 && nowMs - lastBurstMs >= 200)
            {
                lastBurstMs = nowMs;
                burstRemaining--;
                if (burstStageIndex >= 0 && burstStageIndex < stages.Count)
                {
                    var bStage = stages[burstStageIndex];
                    if (TargetingSystem.TryFindTarget(bStage.maxTargetRange, 0, out EntityPlayer bTarget, out _))
                    {
                        FireSingle(bStage, bTarget);
                    }
                }
            }

            // Try to fire new projectile
            if (stages.Count == 0) return;
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;
            if (!IsCooldownReady(stageObj)) return;
            if (!TargetingSystem.TryFindTarget(stage.maxTargetRange, 0, out EntityPlayer target, out _)) return;

            MarkCooldownStart();

            // If windupMs is set, start windup instead of firing immediately
            if (stage.windupMs > 0)
            {
                isWindingUp = true;
                windupStartMs = nowMs;
                windupStageIndex = stageIndex;
                windupTargetEntityId = target.EntityId;
                if (!string.IsNullOrWhiteSpace(stage.windupSound))
                    TryPlaySound(stage.windupSound, stage.windupSoundRange);
                return;
            }

            TryPlaySound(stage.sound, stage.soundRange);
            ExecuteFire(stage, stageIndex, target, nowMs);
        }

        private void ExecuteFire(Stage stage, int stageIndex, EntityPlayer target, long nowMs)
        {
            switch (stage.shape.ToLowerInvariant())
            {
                case "ball":
                    FireSingle(stage, target);
                    break;
                case "spiral":
                    FireSingle(stage, target, spiral: true);
                    break;
                case "burst":
                    burstRemaining = stage.burstCount - 1;
                    burstStageIndex = stageIndex;
                    lastBurstMs = nowMs;
                    FireSingle(stage, target);
                    break;
                case "wave":
                    FireWave(stage, target);
                    break;
                case "homing":
                    FireHoming(stage, target);
                    break;
                case "mortar":
                    FireMortar(stage, target);
                    break;
                case "beam":
                    FireBeam(stage, target);
                    break;
                default:
                    FireSingle(stage, target);
                    break;
            }
        }

        private void SpawnWindupParticles(Stage stage)
        {
            Vec3d pos = entity.Pos.XYZ.AddCopy(0, 1.5, 0);
            int color = GetVariantColor(stage.variant);
            Sapi.World.SpawnParticles(new SimpleParticleProperties(
                2, 4, color,
                new Vec3d(pos.X - 0.3, pos.Y - 0.3, pos.Z - 0.3),
                new Vec3d(pos.X + 0.3, pos.Y + 0.3, pos.Z + 0.3),
                new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.2f, 0),
                0.5f, 0f, 0.4f, 0.8f
            ));
        }

        private void FireSingle(Stage stage, EntityPlayer target, bool spiral = false)
        {
            Vec3d startPos = entity.Pos.XYZ.AddCopy(0, 1.5, 0);
            Vec3d targetPos = target.Pos.XYZ.AddCopy(0, 1, 0);
            Vec3d direction = targetPos.SubCopy(startPos);
            direction.Normalize();

            activeProjectiles.Add(new Projectile
            {
                position = startPos,
                direction = direction,
                startPos = startPos.Clone(),
                speed = stage.speed,
                hitRadius = stage.hitRadius,
                maxRangeSq = stage.maxRange * stage.maxRange,
                damage = stage.damage,
                damageTier = stage.damageTier,
                variant = stage.variant,
                dim = entity.Pos.Dimension,
                isSpiral = spiral,
                spawnedAtMs = Sapi.World.ElapsedMilliseconds
            });
        }

        private void FireWave(Stage stage, EntityPlayer target)
        {
            Vec3d startPos = entity.Pos.XYZ.AddCopy(0, 1.5, 0);
            Vec3d targetPos = target.Pos.XYZ.AddCopy(0, 1, 0);
            Vec3d centerDir = targetPos.SubCopy(startPos);
            centerDir.Normalize();

            float spreadRad = stage.waveSpreadDeg * (float)Math.PI / 180f;
            float step = spreadRad / Math.Max(1, stage.waveCount - 1);
            float startAngle = -spreadRad / 2f;

            for (int i = 0; i < stage.waveCount; i++)
            {
                float angle = startAngle + step * i;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                Vec3d dir = new Vec3d(
                    centerDir.X * cos - centerDir.Z * sin,
                    centerDir.Y,
                    centerDir.X * sin + centerDir.Z * cos
                );
                dir.Normalize();

                activeProjectiles.Add(new Projectile
                {
                    position = startPos.Clone(),
                    direction = dir,
                    startPos = startPos.Clone(),
                    speed = stage.speed * 0.85f,
                    hitRadius = stage.hitRadius * 0.8f,
                    maxRangeSq = stage.maxRange * stage.maxRange,
                    damage = stage.damage * 0.7f,
                    damageTier = stage.damageTier,
                    variant = stage.variant,
                    dim = entity.Pos.Dimension,
                    spawnedAtMs = Sapi.World.ElapsedMilliseconds
                });
            }
        }

        private void FireHoming(Stage stage, EntityPlayer target)
        {
            Vec3d startPos = entity.Pos.XYZ.AddCopy(0, 1.5, 0);
            Vec3d targetPos = target.Pos.XYZ.AddCopy(0, 1, 0);
            Vec3d direction = targetPos.SubCopy(startPos);
            direction.Normalize();

            activeProjectiles.Add(new Projectile
            {
                position = startPos,
                direction = direction,
                startPos = startPos.Clone(),
                speed = stage.speed * 0.7f,
                hitRadius = stage.hitRadius,
                maxRangeSq = stage.maxRange * stage.maxRange,
                damage = stage.damage,
                damageTier = stage.damageTier,
                variant = stage.variant,
                dim = entity.Pos.Dimension,
                isHoming = true,
                homingTurnRad = stage.homingTurnDegPerSec * (float)Math.PI / 180f,
                targetUid = target.PlayerUID,
                spawnedAtMs = Sapi.World.ElapsedMilliseconds
            });
        }

        private void FireMortar(Stage stage, EntityPlayer target)
        {
            Vec3d startPos = entity.Pos.XYZ.AddCopy(0, 2, 0);
            Vec3d landingPos = target.Pos.XYZ.Clone();

            activeProjectiles.Add(new Projectile
            {
                position = startPos,
                direction = new Vec3d(0, 1, 0),
                startPos = startPos.Clone(),
                landingTarget = landingPos,
                speed = stage.speed,
                hitRadius = stage.hitRadius * 1.5f,
                maxRangeSq = stage.maxRange * stage.maxRange,
                damage = stage.damage * 1.3f,
                damageTier = stage.damageTier,
                variant = stage.variant,
                dim = entity.Pos.Dimension,
                isMortar = true,
                mortarFlightSec = 1.5f,
                spawnedAtMs = Sapi.World.ElapsedMilliseconds
            });
        }

        private void FireBeam(Stage stage, EntityPlayer target)
        {
            // Beam is instant — no projectile travel. Telegraph + hit.
            Vec3d from = entity.Pos.XYZ.AddCopy(0, 1.5, 0);
            Vec3d to = target.Pos.XYZ.AddCopy(0, 1, 0);

            // Visual beam line
            int color = GetVariantColor(stage.variant);
            for (int i = 0; i < 5; i++)
            {
                double t = i / 4.0;
                Vec3d point = new Vec3d(
                    from.X + (to.X - from.X) * t,
                    from.Y + (to.Y - from.Y) * t,
                    from.Z + (to.Z - from.Z) * t
                );
                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    3, 6, color,
                    point.AddCopy(-0.15, -0.15, -0.15),
                    point.AddCopy(0.15, 0.15, 0.15),
                    new Vec3f(0, 0, 0), new Vec3f(0, 0.05f, 0),
                    0.3f, 0f, 0.6f, 1.0f,
                    EnumParticleModel.Cube
                ));
            }

            // Instant damage
            float dmg = ApplyDamageMultiplier(stage.damage);
            target.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = GetDamageType(stage.variant),
                DamageTier = stage.damageTier
            }, dmg);

            ParticleUtils.SpawnImpact(Sapi, target, color, 15, 0.5f);
        }

        private void ProcessProjectiles(float dt, long nowMs)
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                var proj = activeProjectiles[i];

                // Mortar: arc trajectory
                if (proj.isMortar)
                {
                    float elapsed = (nowMs - proj.spawnedAtMs) / 1000f;
                    float progress = Math.Min(1f, elapsed / proj.mortarFlightSec);

                    // Parabolic arc
                    double arcHeight = 6.0 * Math.Sin(progress * Math.PI);
                    proj.position.X = proj.startPos.X + (proj.landingTarget.X - proj.startPos.X) * progress;
                    proj.position.Z = proj.startPos.Z + (proj.landingTarget.Z - proj.startPos.Z) * progress;
                    proj.position.Y = proj.startPos.Y + arcHeight + (proj.landingTarget.Y - proj.startPos.Y) * progress;

                    SpawnProjectileParticles(proj, nowMs);

                    if (progress >= 1f)
                    {
                        // Impact at landing
                        MortarImpact(proj);
                        activeProjectiles.RemoveAt(i);
                    }
                    continue;
                }

                // Homing: adjust direction toward target
                if (proj.isHoming && !string.IsNullOrEmpty(proj.targetUid))
                {
                    var targetPlayer = Sapi.World.PlayerByUid(proj.targetUid) as IServerPlayer;
                    if (targetPlayer?.Entity != null && targetPlayer.Entity.Alive)
                    {
                        Vec3d toTarget = targetPlayer.Entity.Pos.XYZ.AddCopy(0, 1, 0).SubCopy(proj.position);
                        toTarget.Normalize();

                        float turnAmount = proj.homingTurnRad * dt;
                        proj.direction.X += (toTarget.X - proj.direction.X) * turnAmount;
                        proj.direction.Y += (toTarget.Y - proj.direction.Y) * turnAmount;
                        proj.direction.Z += (toTarget.Z - proj.direction.Z) * turnAmount;
                        proj.direction.Normalize();
                    }
                }

                // Move projectile
                double moveAmount = proj.speed * dt;
                proj.position.X += proj.direction.X * moveAmount;
                proj.position.Y += proj.direction.Y * moveAmount;
                proj.position.Z += proj.direction.Z * moveAmount;

                // Check max range
                double traveledSq = proj.position.SquareDistanceTo(proj.startPos);
                if (traveledSq > proj.maxRangeSq)
                {
                    SpawnDissipate(proj);
                    activeProjectiles.RemoveAt(i);
                    continue;
                }

                // Spawn particles
                SpawnProjectileParticles(proj, nowMs);

                // Check hit
                if (CheckHit(proj))
                {
                    activeProjectiles.RemoveAt(i);
                }
            }
        }

        private void SpawnProjectileParticles(Projectile proj, long nowMs)
        {
            int color = GetVariantColor(proj.variant);
            float size = 0.7f;
            int count = 4;

            // Spiral: offset particles in rotating pattern
            if (proj.isSpiral)
            {
                float elapsed = (nowMs - proj.spawnedAtMs) / 200f;
                double angle = elapsed * Math.PI * 2;
                double offX = Math.Cos(angle) * 0.5;
                double offZ = Math.Sin(angle) * 0.5;

                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    count, count + 2, color,
                    proj.position.AddCopy(offX - 0.1, -0.1, offZ - 0.1),
                    proj.position.AddCopy(offX + 0.1, 0.1, offZ + 0.1),
                    new Vec3f(0, 0, 0), new Vec3f(0, 0.02f, 0),
                    0.4f, 0f, size, size * 1.3f,
                    EnumParticleModel.Cube
                ));
                return;
            }

            // Default ball shape — dense cluster
            Sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 3, color,
                proj.position.AddCopy(-0.25, -0.25, -0.25),
                proj.position.AddCopy(0.25, 0.25, 0.25),
                new Vec3f(0, 0, 0), new Vec3f(0, 0.03f, 0),
                0.35f, 0f, size, size * 1.2f,
                EnumParticleModel.Quad
            ));

            // Trail behind
            Vec3d trailPos = proj.position.AddCopy(-proj.direction.X * 0.5, -proj.direction.Y * 0.5, -proj.direction.Z * 0.5);
            int trailAlpha = ColorUtil.ColorA(color) / 2;
            int trailColor = ColorUtil.ToRgba(trailAlpha, ColorUtil.ColorR(color), ColorUtil.ColorG(color), ColorUtil.ColorB(color));

            Sapi.World.SpawnParticles(new SimpleParticleProperties(
                2, 3, trailColor,
                trailPos.AddCopy(-0.15, -0.15, -0.15),
                trailPos.AddCopy(0.15, 0.15, 0.15),
                new Vec3f(0, 0.02f, 0), new Vec3f(0, 0.06f, 0),
                0.6f, 0f, size * 0.6f, size * 0.8f
            ));
        }

        private void MortarImpact(Projectile proj)
        {
            int color = GetVariantColor(proj.variant);
            float dmg = ApplyDamageMultiplier(proj.damage);

            // AoE at landing
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != proj.dim) continue;

                double dx = pe.Pos.X - proj.landingTarget.X;
                double dz = pe.Pos.Z - proj.landingTarget.Z;
                if (dx * dx + dz * dz <= proj.hitRadius * proj.hitRadius)
                {
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = GetDamageType(proj.variant),
                        DamageTier = proj.damageTier
                    }, dmg);
                }
            }

            // Impact explosion
            ParticleUtils.SpawnShockwave(Sapi, proj.landingTarget, proj.hitRadius, color, 20, 0.6f);
            Sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/largeexplosion"),
                proj.landingTarget.X, proj.landingTarget.Y, proj.landingTarget.Z, null, true, 64f, 0.5f);
        }

        private bool CheckHit(Projectile proj)
        {
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != proj.dim) continue;

                double distSq = pe.Pos.XYZ.AddCopy(0, 1, 0).SquareDistanceTo(proj.position);
                if (distSq <= proj.hitRadius * proj.hitRadius)
                {
                    float dmg = ApplyDamageMultiplier(proj.damage);
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = GetDamageType(proj.variant),
                        DamageTier = proj.damageTier
                    }, dmg);

                    int color = GetVariantColor(proj.variant);
                    ParticleUtils.SpawnImpact(Sapi, pe, color, 15, 0.6f);
                    entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/projectilehit"), pe.Pos.X, pe.Pos.Y, pe.Pos.Z, null, true, 48, 0.5f);

                    // Chain variant: apply slow on hit
                    if (string.Equals(proj.variant, "chain", StringComparison.OrdinalIgnoreCase))
                    {
                        pe.Stats.Set("walkspeed", "projchain", -0.4f, false);
                        RegisterCallbackTracked(_ => { pe?.Stats?.Remove("walkspeed", "projchain"); }, 2000);
                    }

                    // Blood variant: heal boss
                    if (string.Equals(proj.variant, "blood", StringComparison.OrdinalIgnoreCase))
                    {
                        var health = entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
                        if (health != null)
                        {
                            health.Health = Math.Min(health.MaxHealth, health.Health + dmg * 0.3f);
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        private void SpawnDissipate(Projectile proj)
        {
            int color = GetVariantColor(proj.variant);
            ParticleUtils.SpawnAuraSphere(Sapi, proj.position, 1.2f, color, 10, 0.4f);
        }

        private int GetVariantColor(string variant)
        {
            return variant switch
            {
                "fire" => ParticleUtils.Colors.Fire,
                "shadow" => ParticleUtils.Colors.Shadow,
                "ice" => ParticleUtils.Colors.Ice,
                "poison" => ParticleUtils.Colors.Poison,
                "lightning" => ParticleUtils.Colors.Lightning,
                "void" => ParticleUtils.Colors.Void,
                "blood" => ParticleUtils.Colors.Blood,
                "chain" => ParticleUtils.Colors.Chain,
                _ => ParticleUtils.Colors.Fire
            };
        }

        private EnumDamageType GetDamageType(string variant)
        {
            return variant switch
            {
                "fire" => EnumDamageType.Fire,
                "ice" => EnumDamageType.Frost,
                "poison" => EnumDamageType.Poison,
                "lightning" => EnumDamageType.Electricity,
                _ => EnumDamageType.Injury
            };
        }

        protected override void StopAbility()
        {
            activeProjectiles.Clear();
            burstRemaining = 0;
        }

        private class Projectile
        {
            public Vec3d position;
            public Vec3d direction;
            public Vec3d startPos;
            public Vec3d landingTarget;
            public float speed;
            public float hitRadius;
            public double maxRangeSq;
            public float damage;
            public int damageTier;
            public string variant;
            public int dim;
            public bool isSpiral;
            public bool isHoming;
            public bool isMortar;
            public float homingTurnRad;
            public float mortarFlightSec;
            public string targetUid;
            public long spawnedAtMs;
        }
    }
}
