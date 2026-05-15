using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Boss enrage behavior: after a configurable timer from first damage,
    /// boss gains +50% damage and +50% move speed permanently until death/soft reset.
    /// Visual: red aura particles. Warning pulse at 10 seconds before enrage.
    /// </summary>
    public class EntityBehaviorBossEnrage : EntityBehavior
    {
        private const string AttrFirstDamageHours = "alegacyvsquest:enrage:firstDamageHours";
        private const string AttrEnraged = "alegacyvsquest:enrage:active";

        private double enrageTimerSeconds = 240; // default 4 minutes
        private double firstDamageTimeHours;
        private bool enraged;
        private bool warningShown;
        private long tickListenerId;

        public bool IsEnraged => enraged;

        public EntityBehaviorBossEnrage(Entity entity) : base(entity) { }

        public override string PropertyName() => "bossenrage";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            enrageTimerSeconds = typeAttributes["enrageTimerSeconds"].AsDouble(240);

            // Override from trial tier data (set by HollowTrialSystem.ApplyTierStats)
            double tierOverride = entity.WatchedAttributes.GetDouble("alegacyvsquest:trial:enrageTimerSeconds", 0);
            if (tierOverride > 0)
            {
                enrageTimerSeconds = tierOverride;
            }

            // Restore state from WatchedAttributes
            firstDamageTimeHours = entity.WatchedAttributes.GetDouble(AttrFirstDamageHours, 0);
            enraged = entity.WatchedAttributes.GetBool(AttrEnraged, false);

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnTick, 1000);
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (damageSource?.SourceEntity == null) return;
            if (damage <= 0) return;

            // Record first damage time
            if (firstDamageTimeHours <= 0)
            {
                firstDamageTimeHours = entity.World.Calendar.TotalHours;
                entity.WatchedAttributes.SetDouble(AttrFirstDamageHours, firstDamageTimeHours);
                entity.WatchedAttributes.MarkPathDirty(AttrFirstDamageHours);
            }

            // Apply vulnerability multiplier if enraged (enrage doesn't affect incoming damage, only outgoing)
        }

        private void OnTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive) return;

            // Spawn enrage aura particles while enraged
            if (enraged)
            {
                SpawnEnrageAura();
                return;
            }

            if (firstDamageTimeHours <= 0) return;

            double nowHours = entity.World.Calendar.TotalHours;
            double elapsedSeconds = (nowHours - firstDamageTimeHours) * 3600.0;

            // Warning at 10 seconds before enrage
            if (!warningShown && elapsedSeconds >= enrageTimerSeconds - 10)
            {
                warningShown = true;
                SpawnWarningParticles();
            }

            // Activate enrage
            if (elapsedSeconds >= enrageTimerSeconds)
            {
                ActivateEnrage();
            }
        }

        private void ActivateEnrage()
        {
            enraged = true;
            entity.WatchedAttributes.SetBool(AttrEnraged, true);
            entity.WatchedAttributes.MarkPathDirty(AttrEnraged);

            // Apply stat boosts via WatchedAttributes so other systems can read them
            entity.WatchedAttributes.SetFloat("alegacyvsquest:enrage:damageMultiplier", 1.5f);
            entity.WatchedAttributes.SetFloat("alegacyvsquest:enrage:speedMultiplier", 1.5f);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:enrage:damageMultiplier");
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:enrage:speedMultiplier");

            // Boost walk/run speed
            var stats = entity.Stats;
            if (stats != null)
            {
                stats.Set("walkspeed", "enrage", 0.5f, false);
            }

            // Enrage activation sound
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/environment/thunder1"), entity, null, true, 48);
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/creature/drifter-death"), entity, null, true, 32);

            // Enrage activation particles
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                ParticleUtils.SpawnShockwave(sapi, entity.Pos.XYZ, 4f, ParticleUtils.Colors.Fire, 30, 0.5f);
                ParticleUtils.SpawnEntityAura(sapi, entity, ParticleUtils.Colors.Fire, 12, 0.5f, 0.8f);
            }
        }

        private void SpawnWarningParticles()
        {
            // Red pulsing particles as warning (server-side spawn, synced to clients)
            if (entity.Api?.Side != EnumAppSide.Server) return;

            var pos = entity.Pos;
            SimpleParticleProperties particles = new SimpleParticleProperties(
                minQuantity: 20, maxQuantity: 30,
                color: ColorUtil.ToRgba(200, 255, 50, 50),
                minPos: new Vec3d(pos.X - 1, pos.Y, pos.Z - 1),
                maxPos: new Vec3d(pos.X + 1, pos.Y + 2, pos.Z + 1),
                minVelocity: new Vec3f(-0.1f, 0.2f, -0.1f),
                maxVelocity: new Vec3f(0.1f, 0.5f, 0.1f),
                lifeLength: 1.5f,
                gravityEffect: -0.05f,
                minSize: 0.3f, maxSize: 0.6f
            );

            entity.World.SpawnParticles(particles);
        }

        /// <summary>
        /// Spawn red aura particles while enraged (called from client-side renderer or server tick).
        /// </summary>
        public void SpawnEnrageAura()
        {
            if (!enraged || !entity.Alive) return;

            var pos = entity.Pos;
            SimpleParticleProperties particles = new SimpleParticleProperties(
                minQuantity: 5, maxQuantity: 10,
                color: ColorUtil.ToRgba(150, 255, 30, 30),
                minPos: new Vec3d(pos.X - 0.8, pos.Y + 0.2, pos.Z - 0.8),
                maxPos: new Vec3d(pos.X + 0.8, pos.Y + 1.8, pos.Z + 0.8),
                minVelocity: new Vec3f(-0.05f, 0.1f, -0.05f),
                maxVelocity: new Vec3f(0.05f, 0.3f, 0.05f),
                lifeLength: 0.8f,
                gravityEffect: -0.02f,
                minSize: 0.2f, maxSize: 0.4f
            );

            entity.World.SpawnParticles(particles);
        }

        /// <summary>
        /// Reset enrage state (used on soft reset / despawn).
        /// </summary>
        public void Reset()
        {
            enraged = false;
            warningShown = false;
            firstDamageTimeHours = 0;

            entity.WatchedAttributes.SetBool(AttrEnraged, false);
            entity.WatchedAttributes.SetDouble(AttrFirstDamageHours, 0);
            entity.WatchedAttributes.RemoveAttribute("alegacyvsquest:enrage:damageMultiplier");
            entity.WatchedAttributes.RemoveAttribute("alegacyvsquest:enrage:speedMultiplier");

            var stats = entity.Stats;
            stats?.Remove("walkspeed", "enrage");
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (tickListenerId != 0 && entity.World != null)
            {
                entity.World.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }
        }
    }
}
