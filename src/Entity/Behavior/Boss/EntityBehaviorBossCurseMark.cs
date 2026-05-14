using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Curse Mark: boss marks the player. After a delay, the mark detonates dealing damage.
    /// Player must move away from the boss (or other players in group context) to reduce damage.
    /// Closer = more damage. Visual: growing dark circle under player.
    /// </summary>
    public class EntityBehaviorBossCurseMark : EntityBehavior
    {
        private float markDelaySec = 3f;
        private float baseDamage = 12f;
        private float safeDistance = 10f;
        private float cooldownSec = 14f;
        private float minDamageMultiplier = 0.2f; // damage at max distance

        private bool markActive;
        private double markStartMs;
        private string markedPlayerUid;
        private double lastMarkMs;

        public bool IsMarkActive => markActive;

        public EntityBehaviorBossCurseMark(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosscursemark";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            markDelaySec = typeAttributes["markDelaySec"].AsFloat(3f);
            baseDamage = typeAttributes["baseDamage"].AsFloat(12f);
            safeDistance = typeAttributes["safeDistance"].AsFloat(10f);
            cooldownSec = typeAttributes["cooldownSec"].AsFloat(14f);
            minDamageMultiplier = typeAttributes["minDamageMultiplier"].AsFloat(0.2f);
        }

        /// <summary>
        /// Mark the nearest player.
        /// </summary>
        public bool TryMark()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive || markActive) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastMarkMs < cooldownSec * 1000) return false;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return false;

            // Find nearest player
            IServerPlayer target = null;
            double nearestDist = double.MaxValue;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                if (sp.Entity == null || !sp.Entity.Alive) continue;
                if (sp.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = sp.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    target = sp;
                }
            }

            if (target == null) return false;

            markActive = true;
            markStartMs = nowMs;
            markedPlayerUid = target.PlayerUID;

            // Notify player
            var curseMarkSapi = entity.Api as ICoreServerAPI;
            if (curseMarkSapi != null)
            {
                curseMarkSapi.SendMessage(target, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                    LocalizationUtils.GetSafe("albase:trial-cursemark-warning"), EnumChatType.Notification);
            }

            return true;
        }

        public void OnGameTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!markActive) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            double elapsed = (nowMs - markStartMs) / 1000.0;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            var player = sapi.World.PlayerByUid(markedPlayerUid) as IServerPlayer;
            if (player?.Entity == null || !player.Entity.Alive)
            {
                markActive = false;
                return;
            }

            // Spawn growing mark particles under player
            float progress = (float)(elapsed / markDelaySec);
            SpawnMarkParticles(player.Entity.Pos.XYZ, progress);

            // Detonate
            if (elapsed >= markDelaySec)
            {
                Detonate(sapi, player);
            }
        }

        private void Detonate(ICoreServerAPI sapi, IServerPlayer player)
        {
            markActive = false;
            lastMarkMs = entity.World.ElapsedMilliseconds;

            if (player?.Entity == null || !player.Entity.Alive) return;

            // Calculate damage based on distance to boss
            double dist = Math.Sqrt(player.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ));
            float distRatio = (float)Math.Min(1.0, dist / safeDistance);

            // Closer = more damage, further = less (linear interpolation)
            float damageMultiplier = 1f - distRatio * (1f - minDamageMultiplier);
            float finalDamage = baseDamage * damageMultiplier;

            if (finalDamage > 0.5f)
            {
                player.Entity.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = EnumDamageType.Injury
                }, finalDamage);
            }

            // Detonation particles
            SpawnDetonationParticles(player.Entity.Pos.XYZ);
            markedPlayerUid = null;
        }

        private void SpawnMarkParticles(Vec3d pos, float progress)
        {
            float currentRadius = 0.5f + progress * 2f;
            var rand = entity.World.Rand;
            int count = (int)(currentRadius * 3);

            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double r = rand.NextDouble() * currentRadius;

                entity.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba((int)(100 + progress * 120), 80, 0, 120),
                    new Vec3d(pos.X + Math.Cos(angle) * r, pos.Y + 0.1, pos.Z + Math.Sin(angle) * r),
                    new Vec3d(pos.X + Math.Cos(angle) * r, pos.Y + 0.1, pos.Z + Math.Sin(angle) * r),
                    new Vec3f(0, 0.02f, 0), new Vec3f(0, 0.05f, 0),
                    0.3f, 0f, 0.1f, 0.2f));
            }
        }

        private void SpawnDetonationParticles(Vec3d pos)
        {
            entity.World.SpawnParticles(new SimpleParticleProperties(
                20, 30, ColorUtil.ToRgba(220, 120, 0, 180),
                new Vec3d(pos.X - 1.5, pos.Y, pos.Z - 1.5),
                new Vec3d(pos.X + 1.5, pos.Y + 2, pos.Z + 1.5),
                new Vec3f(-0.3f, 0.3f, -0.3f), new Vec3f(0.3f, 0.8f, 0.3f),
                1.0f, 0.02f, 0.2f, 0.5f));
        }
    }
}
