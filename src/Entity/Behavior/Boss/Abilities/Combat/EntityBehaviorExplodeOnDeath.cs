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
    public class EntityBehaviorExplodeOnDeath : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:explodeondeath:lastExplode";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false; // Only activates on death
        private class Stage : BossAbilityStage
        {
            public int fuseMs;
            public float explosionRadius;
            public float explosionDamage;
            public int damageTier;
            public EnumDamageType damageType;
            public AssetLocation explodeSound;
            public float explodeSoundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                fuseMs = json["fuseMs"].AsInt(2000);
                explosionRadius = json["explosionRadius"].AsFloat(3f);
                explosionDamage = json["explosionDamage"].AsFloat(10f);
                damageTier = json["damageTier"].AsInt(1);
                damageType = (EnumDamageType)json["damageType"].AsInt((int)EnumDamageType.PiercingAttack);
                
                string soundPath = json["explodeSound"].AsString("effect/smallexplosion");
                explodeSound = new AssetLocation(soundPath);
                explodeSoundVolume = json["explodeSoundVolume"].AsFloat(0.5f);
            }
        }

        private List<Stage> stages = new List<Stage>();
        private bool scheduled;
        private long explodeCallbackId;

        public EntityBehaviorExplodeOnDeath(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "explodeondeath";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).explosionRadius;

        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target)
        {
        }

        protected override void StopAbility()
        {
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            
            if (Sapi == null || scheduled || stages.Count == 0) return;
            
            var stage = stages[0];
            // Schedule explosion after fuse time
            scheduled = true;
            explodeCallbackId = Sapi.Event.RegisterCallback((_) =>
            {
                TryExplode(stage);
            }, stage.fuseMs);
            
            // Play ticking fuse sound periodically
            int tickInterval = 500; // ms
            int totalTicks = stage.fuseMs / tickInterval;
            for (int i = 1; i <= totalTicks; i++)
            {
                int tickDelay = i * tickInterval;
                if (tickDelay < stage.fuseMs)
                {
                    Sapi.Event.RegisterCallback((_) =>
                    {
                        if (entity != null && entity.Alive == false)
                        {
                            float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                            Sapi.World.PlaySoundAt(
                                new AssetLocation("game:sounds/tick"),
                                entity,
                                null,
                                pitch,
                                32,
                                0.3f
                            );
                        }
                    }, tickDelay);
                }
            }
        }

        private void TryExplode(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            
            var pos = entity.Pos.XYZ;
            
            // Play explosion sound
            Sapi.World.PlaySoundAt(
                stage.explodeSound,
                entity,
                null,
                1f,
                32,
                stage.explodeSoundVolume
            );
            
            // Create explosion
            var blockPos = new BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z);
            Sapi.World.CreateExplosion(
                blockPos, 
                EnumBlastType.EntityBlast, 
                stage.explosionRadius, 
                stage.explosionDamage
            );
            
            // Remove the entity
            entity.Die(EnumDespawnReason.Removed);
        }
    }
}
