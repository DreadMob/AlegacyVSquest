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
    /// Mirror Image: boss creates 1-2 illusions (same model, 1 HP, don't attack).
    /// Real boss teleports. Illusions disappear after timeout or 1 hit.
    /// </summary>
    public class EntityBehaviorBossMirrorImage : EntityBehavior
    {
        private int imageCount = 2;
        private float imageLifetimeSec = 6f;
        private bool teleportOnCast = true;
        private float teleportRadius = 6f;
        private float cooldownSec = 20f;

        private readonly List<long> activeImageIds = new();
        private double lastCastMs;

        public EntityBehaviorBossMirrorImage(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossmirrorimage";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            imageCount = typeAttributes["imageCount"].AsInt(2);
            imageLifetimeSec = typeAttributes["imageLifetimeSec"].AsFloat(6f);
            teleportOnCast = typeAttributes["teleportOnCast"].AsBool(true);
            teleportRadius = typeAttributes["teleportRadius"].AsFloat(6f);
            cooldownSec = typeAttributes["cooldownSec"].AsFloat(20f);
        }

        /// <summary>
        /// Create mirror images and optionally teleport.
        /// </summary>
        public bool TryCast()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastCastMs < cooldownSec * 1000) return false;

            lastCastMs = nowMs;
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return false;

            var bossPos = entity.Pos.XYZ;
            var rand = entity.World.Rand;

            // Spawn illusions at random positions around boss
            for (int i = 0; i < imageCount; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double dist = 2 + rand.NextDouble() * 3;
                Vec3d imagePos = new Vec3d(
                    bossPos.X + Math.Cos(angle) * dist,
                    bossPos.Y,
                    bossPos.Z + Math.Sin(angle) * dist
                );

                SpawnIllusion(sapi, imagePos);
            }

            // Teleport real boss
            if (teleportOnCast)
            {
                double tpAngle = rand.NextDouble() * Math.PI * 2;
                double tpDist = 3 + rand.NextDouble() * (teleportRadius - 3);
                Vec3d tpPos = new Vec3d(
                    bossPos.X + Math.Cos(tpAngle) * tpDist,
                    bossPos.Y,
                    bossPos.Z + Math.Sin(tpAngle) * tpDist
                );

                entity.TeleportTo(tpPos);
                SpawnTeleportParticles(bossPos);
                SpawnTeleportParticles(tpPos);
            }

            return true;
        }

        private void SpawnIllusion(ICoreServerAPI sapi, Vec3d pos)
        {
            try
            {
                // Create same entity type but with 1 HP and marked as illusion
                var type = entity.Properties;
                var illusion = sapi.World.ClassRegistry.CreateEntity(type);
                if (illusion == null) return;

                illusion.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + entity.Pos.Dimension * 32768.0, pos.Z));
                illusion.Pos.SetFrom(illusion.Pos);

                // Mark as illusion (1 HP, no AI attack, auto-despawn)
                illusion.WatchedAttributes.SetBool("alegacyvsquest:mirrorillusion", true);
                illusion.WatchedAttributes.MarkPathDirty("alegacyvsquest:mirrorillusion");

                sapi.World.SpawnEntity(illusion);
                activeImageIds.Add(illusion.EntityId);

                // Schedule despawn after lifetime
                sapi.Event.RegisterCallback(_ =>
                {
                    if (illusion.Alive)
                    {
                        sapi.World.DespawnEntity(illusion, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    activeImageIds.Remove(illusion.EntityId);
                }, (int)(imageLifetimeSec * 1000));
            }
            catch (Exception ex)
            {
                entity.Api?.Logger?.Warning("[BossMirrorImage] Failed to spawn illusion: {0}", ex.Message);
            }
        }

        private void SpawnTeleportParticles(Vec3d pos)
        {
            entity.World.SpawnParticles(new SimpleParticleProperties(
                10, 15, ColorUtil.ToRgba(160, 140, 80, 200),
                new Vec3d(pos.X - 0.5, pos.Y, pos.Z - 0.5),
                new Vec3d(pos.X + 0.5, pos.Y + 2, pos.Z + 0.5),
                new Vec3f(-0.2f, 0.1f, -0.2f), new Vec3f(0.2f, 0.4f, 0.2f),
                0.8f, 0f, 0.15f, 0.35f));
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            // Clean up illusions
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                foreach (var id in activeImageIds)
                {
                    var e = sapi.World.GetEntityById(id);
                    if (e != null && e.Alive)
                    {
                        sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
            }
            activeImageIds.Clear();
        }
    }
}
