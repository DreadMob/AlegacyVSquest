using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    /// <summary>
    /// Telegraph behavior: displays a particle zone on the ground before a powerful attack.
    /// The attack is delayed by windup time, giving players time to dodge.
    /// Supports shapes: circle, line, cone.
    /// </summary>
    public class EntityBehaviorBossTelegraph : EntityBehavior
    {
        private const string AttrTelegraphActive = "alegacyvsquest:telegraph:active";

        /// <summary>
        /// Windup time in milliseconds before the attack lands.
        /// </summary>
        private float windupMs = 1000f;

        /// <summary>
        /// Shape of the telegraph zone: "circle", "line", "cone".
        /// </summary>
        private string shape = "circle";

        /// <summary>
        /// Radius for circle/cone shapes (blocks).
        /// </summary>
        private float radius = 5f;

        /// <summary>
        /// Length for line shape (blocks).
        /// </summary>
        private float length = 10f;

        /// <summary>
        /// Width for line shape (blocks).
        /// </summary>
        private float width = 2f;

        /// <summary>
        /// Angle for cone shape (degrees).
        /// </summary>
        private float angle = 45f;

        private bool telegraphActive;
        private double telegraphStartMs;
        private Vec3d telegraphCenter;
        private Vec3f telegraphDirection;
        private Action onTelegraphComplete;
        private long tickListenerId;

        public bool IsTelegraphActive => telegraphActive;

        public EntityBehaviorBossTelegraph(Entity entity) : base(entity) { }

        public override string PropertyName() => "bosstelegraph";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            windupMs = typeAttributes["windupMs"].AsFloat(1000f);
            shape = typeAttributes["shape"].AsString("circle");
            radius = typeAttributes["radius"].AsFloat(5f);
            length = typeAttributes["length"].AsFloat(10f);
            width = typeAttributes["width"].AsFloat(2f);
            angle = typeAttributes["angle"].AsFloat(45f);

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnTick, 150);
            }
        }

        /// <summary>
        /// Start a telegraph at the specified position/direction.
        /// The onComplete callback fires after windup, allowing the ability to deal damage.
        /// </summary>
        /// <param name="center">Center of the telegraph zone.</param>
        /// <param name="direction">Direction for line/cone shapes.</param>
        /// <param name="onComplete">Callback when windup finishes (execute the attack).</param>
        public void StartTelegraph(Vec3d center, Vec3f direction, Action onComplete)
        {
            if (telegraphActive) return;

            telegraphActive = true;
            telegraphStartMs = entity.World.ElapsedMilliseconds;
            telegraphCenter = center;
            telegraphDirection = direction ?? new Vec3f(0, 0, 1);
            onTelegraphComplete = onComplete;

            entity.WatchedAttributes.SetBool(AttrTelegraphActive, true);
            entity.WatchedAttributes.MarkPathDirty(AttrTelegraphActive);
        }

        /// <summary>
        /// Check if a position is inside the current telegraph zone.
        /// Used to determine if a player should take damage when the attack lands.
        /// </summary>
        public bool IsPositionInZone(Vec3d position)
        {
            if (!telegraphActive || telegraphCenter == null || position == null) return false;

            switch (shape.ToLowerInvariant())
            {
                case "circle":
                    return IsInCircle(position);
                case "line":
                    return IsInLine(position);
                case "cone":
                    return IsInCone(position);
                default:
                    return IsInCircle(position);
            }
        }

        private bool IsInCircle(Vec3d pos)
        {
            double dx = pos.X - telegraphCenter.X;
            double dz = pos.Z - telegraphCenter.Z;
            return dx * dx + dz * dz <= radius * radius;
        }

        private bool IsInLine(Vec3d pos)
        {
            if (telegraphDirection == null) return false;

            // Project position onto line direction
            double dx = pos.X - telegraphCenter.X;
            double dz = pos.Z - telegraphCenter.Z;

            double dirLen = Math.Sqrt(telegraphDirection.X * telegraphDirection.X + telegraphDirection.Z * telegraphDirection.Z);
            if (dirLen < 0.001) return false;

            double ndx = telegraphDirection.X / dirLen;
            double ndz = telegraphDirection.Z / dirLen;

            // Distance along line
            double along = dx * ndx + dz * ndz;
            if (along < 0 || along > length) return false;

            // Distance perpendicular to line
            double perp = Math.Abs(dx * (-ndz) + dz * ndx);
            return perp <= width / 2.0;
        }

        private bool IsInCone(Vec3d pos)
        {
            if (telegraphDirection == null) return false;

            double dx = pos.X - telegraphCenter.X;
            double dz = pos.Z - telegraphCenter.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);

            if (dist > radius || dist < 0.01) return false;

            double dirLen = Math.Sqrt(telegraphDirection.X * telegraphDirection.X + telegraphDirection.Z * telegraphDirection.Z);
            if (dirLen < 0.001) return false;

            double ndx = telegraphDirection.X / dirLen;
            double ndz = telegraphDirection.Z / dirLen;

            double dot = (dx / dist) * ndx + (dz / dist) * ndz;
            double halfAngleRad = angle * 0.5 * Math.PI / 180.0;

            return dot >= Math.Cos(halfAngleRad);
        }

        private void OnTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive || !telegraphActive) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            double elapsed = nowMs - telegraphStartMs;

            // Spawn telegraph particles
            SpawnTelegraphParticles();

            // Check if windup complete
            if (elapsed >= windupMs)
            {
                CompleteTelegraph();
            }
        }

        private void CompleteTelegraph()
        {
            telegraphActive = false;
            entity.WatchedAttributes.SetBool(AttrTelegraphActive, false);
            entity.WatchedAttributes.MarkPathDirty(AttrTelegraphActive);

            // Fire the attack callback
            try
            {
                onTelegraphComplete?.Invoke();
            }
            catch (Exception ex)
            {
                entity.Api?.Logger?.Warning("[BossTelegraph] onComplete callback failed: {0}", ex.Message);
            }

            onTelegraphComplete = null;
            telegraphCenter = null;
            telegraphDirection = null;
        }

        /// <summary>
        /// Cancel an active telegraph without executing the attack.
        /// </summary>
        public void CancelTelegraph()
        {
            if (!telegraphActive) return;

            telegraphActive = false;
            entity.WatchedAttributes.SetBool(AttrTelegraphActive, false);
            entity.WatchedAttributes.MarkPathDirty(AttrTelegraphActive);

            onTelegraphComplete = null;
            telegraphCenter = null;
            telegraphDirection = null;
        }

        private void SpawnTelegraphParticles()
        {
            if (telegraphCenter == null) return;

            // Purple particles on the ground in the telegraph zone
            int count = shape == "circle" ? (int)(radius * 3) : (int)(length * 2);
            var rand = entity.World.Rand;

            for (int i = 0; i < count; i++)
            {
                Vec3d particlePos = GetRandomPointInZone(rand);
                if (particlePos == null) continue;

                SimpleParticleProperties particles = new SimpleParticleProperties(
                    minQuantity: 1, maxQuantity: 1,
                    color: ColorUtil.ToRgba(160, 180, 80, 255),
                    minPos: particlePos,
                    maxPos: particlePos,
                    minVelocity: new Vec3f(0, 0.05f, 0),
                    maxVelocity: new Vec3f(0, 0.15f, 0),
                    lifeLength: 0.4f,
                    gravityEffect: 0f,
                    minSize: 0.15f, maxSize: 0.25f
                );

                entity.World.SpawnParticles(particles);
            }
        }

        private Vec3d GetRandomPointInZone(Random rand)
        {
            if (telegraphCenter == null) return null;

            switch (shape.ToLowerInvariant())
            {
                case "circle":
                {
                    double a = rand.NextDouble() * Math.PI * 2;
                    double r = Math.Sqrt(rand.NextDouble()) * radius;
                    return new Vec3d(
                        telegraphCenter.X + Math.Cos(a) * r,
                        telegraphCenter.Y,
                        telegraphCenter.Z + Math.Sin(a) * r
                    );
                }
                case "line":
                {
                    double dirLen = Math.Sqrt(telegraphDirection.X * telegraphDirection.X + telegraphDirection.Z * telegraphDirection.Z);
                    if (dirLen < 0.001) return telegraphCenter;

                    double ndx = telegraphDirection.X / dirLen;
                    double ndz = telegraphDirection.Z / dirLen;

                    double along = rand.NextDouble() * length;
                    double perp = (rand.NextDouble() - 0.5) * width;

                    return new Vec3d(
                        telegraphCenter.X + ndx * along + (-ndz) * perp,
                        telegraphCenter.Y,
                        telegraphCenter.Z + ndz * along + ndx * perp
                    );
                }
                case "cone":
                {
                    double halfAngleRad = angle * 0.5 * Math.PI / 180.0;
                    double dirAngle = Math.Atan2(telegraphDirection.Z, telegraphDirection.X);
                    double a = dirAngle + (rand.NextDouble() - 0.5) * 2 * halfAngleRad;
                    double r = Math.Sqrt(rand.NextDouble()) * radius;

                    return new Vec3d(
                        telegraphCenter.X + Math.Cos(a) * r,
                        telegraphCenter.Y,
                        telegraphCenter.Z + Math.Sin(a) * r
                    );
                }
                default:
                    return telegraphCenter;
            }
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
