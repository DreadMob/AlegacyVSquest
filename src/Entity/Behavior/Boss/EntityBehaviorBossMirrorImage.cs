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
    /// Mirror Image: boss creates illusion entities and teleports.
    /// Illusions have 1 HP and auto-despawn after lifetime.
    /// </summary>
    public class EntityBehaviorBossMirrorImage : BossAbilityBase
    {
        private const string LastMirrorKey = "alegacyvsquest:bossmirrorimage:lastMs";
        protected override string CooldownKey => LastMirrorKey;

        private class Stage : BossAbilityStage
        {
            public int imageCount;
            public float imageLifetimeSec;
            public bool teleportOnCast;
            public float teleportRadius;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                imageCount = json["imageCount"].AsInt(2);
                imageLifetimeSec = json["imageLifetimeSec"].AsFloat(6f);
                teleportOnCast = json["teleportOnCast"].AsBool(true);
                teleportRadius = json["teleportRadius"].AsFloat(6f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private readonly List<long> activeImageIds = new();

        public EntityBehaviorBossMirrorImage(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossmirrorimage";

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();

            var bossPos = entity.Pos.XYZ.Clone();
            var rand = Sapi.World.Rand;

            // Spawn illusions at random positions around boss
            for (int i = 0; i < stage.imageCount; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double dist = 2 + rand.NextDouble() * 3;
                Vec3d imagePos = new Vec3d(
                    bossPos.X + Math.Cos(angle) * dist,
                    bossPos.Y,
                    bossPos.Z + Math.Sin(angle) * dist
                );

                SpawnIllusion(imagePos, stage.imageLifetimeSec);

                // Shadow explosion at each illusion position
                ParticleUtils.SpawnShadowExplosion(Sapi, imagePos, 1.5f, 1);
            }

            // Teleport real boss
            if (stage.teleportOnCast)
            {
                double tpAngle = rand.NextDouble() * Math.PI * 2;
                double tpDist = 3 + rand.NextDouble() * (stage.teleportRadius - 3);
                Vec3d tpPos = new Vec3d(
                    bossPos.X + Math.Cos(tpAngle) * tpDist,
                    bossPos.Y,
                    bossPos.Z + Math.Sin(tpAngle) * tpDist
                );

                entity.TeleportTo(tpPos);

                // Shadow explosion at boss old and new positions
                ParticleUtils.SpawnShadowExplosion(Sapi, bossPos, 1.5f, 1);
                ParticleUtils.SpawnShadowExplosion(Sapi, tpPos, 1.5f, 1);
            }

            // Sound
            TryPlaySound(stage.sound, stage.soundRange);

            SetAbilityActive(false);
        }

        private void SpawnIllusion(Vec3d pos, float lifetimeSec)
        {
            try
            {
                var type = entity.Properties;
                var illusion = Sapi.World.ClassRegistry.CreateEntity(type);
                if (illusion == null) return;

                illusion.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + entity.Pos.Dimension * 32768.0, pos.Z));
                illusion.Pos.SetFrom(illusion.Pos);

                // Mark as illusion (1 HP, no AI attack, auto-despawn)
                illusion.WatchedAttributes.SetBool("alegacyvsquest:mirrorillusion", true);
                illusion.WatchedAttributes.SetBool("alegacyvsquest:bossclone", true);
                illusion.WatchedAttributes.MarkPathDirty("alegacyvsquest:mirrorillusion");

                Sapi.World.SpawnEntity(illusion);
                activeImageIds.Add(illusion.EntityId);

                // Schedule despawn after lifetime
                RegisterCallbackTracked(_ =>
                {
                    if (illusion.Alive)
                    {
                        Sapi.World.DespawnEntity(illusion, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    activeImageIds.Remove(illusion.EntityId);
                }, (int)(lifetimeSec * 1000));
            }
            catch (Exception ex)
            {
                Sapi?.Logger?.Warning("[BossMirrorImage] Failed to spawn illusion: {0}", ex.Message);
            }
        }

        protected override void StopAbility()
        {
            // Clean up illusions on death/despawn
            if (Sapi != null)
            {
                foreach (var id in activeImageIds)
                {
                    var e = Sapi.World.GetEntityById(id);
                    if (e != null && e.Alive)
                    {
                        Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
            }
            activeImageIds.Clear();
        }
    }
}
