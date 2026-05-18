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
    /// <summary>
    /// Boss Split: at configured HP threshold, boss despawns and spawns 2 mini-copies.
    /// Each copy has 25% of original max HP. Both must be killed to complete the encounter.
    /// Tier 2/3 only.
    /// </summary>
    public class EntityBehaviorBossSplit : EntityBehavior
    {
        private float splitHealthThreshold = 0.5f;
        private bool hasSplit;

        public EntityBehaviorBossSplit(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosssplit";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            splitHealthThreshold = typeAttributes["splitHealthThreshold"].AsFloat(0.5f);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (hasSplit || !entity.Alive) return;

            // Check HP threshold
            var health = entity.GetBehavior<EntityBehaviorHealth>();
            if (health == null || health.MaxHealth <= 0) return;

            float frac = health.Health / health.MaxHealth;
            if (frac > splitHealthThreshold) return;

            // Split!
            hasSplit = true;
            PerformSplit(health);
        }

        private void PerformSplit(EntityBehaviorHealth health)
        {
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            Vec3d bossPos = entity.Pos.XYZ.Clone();
            string entityCode = entity.Code.ToString();
            float copyHp = health.MaxHealth * 0.25f;

            // Visual explosion before split
            ParticleUtils.SpawnShadowExplosion(sapi, bossPos, 3f, 2);
            ParticleUtils.SpawnShockwave(sapi, bossPos, 5f, ParticleUtils.Colors.Void, 24, 0.6f);

            // Sound
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"),
                bossPos.X, bossPos.Y, bossPos.Z, null, true, 48f, 0.8f);

            // Despawn original
            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });

            // Spawn 2 copies
            for (int i = 0; i < 2; i++)
            {
                double offsetX = (i == 0 ? -2 : 2) + (sapi.World.Rand.NextDouble() - 0.5);
                double offsetZ = (sapi.World.Rand.NextDouble() - 0.5) * 2;

                var spawnPos = bossPos.AddCopy(offsetX, 0, offsetZ);

                var entityType = sapi.World.GetEntityType(new AssetLocation(entityCode));
                if (entityType == null) continue;

                var copy = sapi.World.ClassRegistry.CreateEntity(entityType);
                if (copy == null) continue;

                copy.Pos.SetPos(spawnPos);

                // Mark as clone
                copy.WatchedAttributes.SetBool("alegacyvsquest:bossclone", true);
                copy.WatchedAttributes.SetFloat("alegacyvsquest:splitcopy:maxhp", copyHp);

                sapi.World.SpawnEntity(copy);

                // Set HP after spawn
                var copyHealth = copy.GetBehavior<EntityBehaviorHealth>();
                if (copyHealth != null)
                {
                    copyHealth.MaxHealth = copyHp;
                    copyHealth.Health = copyHp;
                }

                // Spawn effect at copy position
                ParticleUtils.SpawnAuraSphere(sapi, spawnPos, 1.5f, ParticleUtils.Colors.Shadow, 12, 0.5f);
            }
        }
    }
}
