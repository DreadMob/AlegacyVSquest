using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace VsQuest
{
    /// <summary>
    /// Boss ability that temporarily changes the boss's visual model (shape/texture)
    /// without changing abilities or behaviors. Used for visual deception.
    /// Uses WatchedAttributes to sync the visual change to clients.
    /// </summary>
    public class EntityBehaviorBossModelSwap : BossAbilityBase
    {
        private const string CurrentShapeKey = "alegacyvsquest:bossmodelswap:shape";
        private const string CurrentTextureKey = "alegacyvsquest:bossmodelswap:texture";
        private const string IsSwappedKey = "alegacyvsquest:bossmodelswap:isswapped";
        private const string SwapEndTimeKey = "alegacyvsquest:bossmodelswap:endtime";
        private const string UsedStagesKey = "alegacyvsquest:bossmodelswap:usedstages";

        private class FormConfig
        {
            public string shape;
            public string texture;
            public int durationMs;

            public void FromJson(JsonObject json)
            {
                shape = json["shape"].AsString(null);
                texture = json["texture"].AsString(null);
                durationMs = json["durationMs"].AsInt(15000);
            }
        }

        private class Stage : BossAbilityStage
        {
            public List<FormConfig> forms = new List<FormConfig>();
            public string swapSound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                swapSound = json["swapSound"].AsString("albase:dark-magic-charge-up");
                soundRange = json["soundRange"].AsFloat(32f);

                var formsArray = json["forms"]?.AsArray();
                if (formsArray != null)
                {
                    foreach (var formObj in formsArray)
                    {
                        if (formObj == null || !formObj.Exists) continue;
                        var form = new FormConfig();
                        form.FromJson(formObj);
                        forms.Add(form);
                    }
                }
            }
        }

        private List<Stage> stages = new List<Stage>();
        protected override string CooldownKey => "alegacyvsquest:bossmodelswap:lastStartMs";
        protected override bool UseHealthBasedStages() => true;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 1000;

        private long revertCallbackId;
        private HashSet<int> usedStages = new HashSet<int>();

        public EntityBehaviorBossModelSwap(Entity entity) : base(entity)
        {
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            
            // Load used stages from attributes
            var usedStr = entity.WatchedAttributes.GetString(UsedStagesKey, "");
            if (!string.IsNullOrEmpty(usedStr))
            {
                var parts = usedStr.Split(',');
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int idx)) usedStages.Add(idx);
                }
            }
        }

        public override string PropertyName() => "bossmodelswap";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override bool ShouldCheckAbility()
        {
            // Only check if not already swapped
            if (entity.WatchedAttributes.GetBool(IsSwappedKey, false)) return false;
            
            // Check if there are any unused stages
            if (!entity.TryGetHealthFraction(out float frac)) return false;
            
            for (int i = 0; i < GetStageCount(); i++)
            {
                if (usedStages.Contains(i)) continue;
                var stage = GetStage(i);
                if (stage == null) continue;
                float threshold = GetStageHealthThreshold(stage);
                if (frac <= threshold) return true;
            }
            
            return false;
        }

        protected override void CheckAbility()
        {
            // Universal check: never activate abilities on boss clones
            if (IsBossClone) return;

            if (!ShouldCheckAbility()) return;
            if (!entity.Alive) return;

            // Find unused stage for current health
            if (!entity.TryGetHealthFraction(out float frac)) return;

            int stageIndex = -1;
            object selectedStage = null;
            float highestThreshold = 0f;

            for (int i = 0; i < GetStageCount(); i++)
            {
                var stage = GetStage(i);
                if (stage == null) continue;

                float threshold = GetStageHealthThreshold(stage);

                // Skip if already used this stage
                if (usedStages.Contains(i)) continue;

                if (frac <= threshold && threshold > highestThreshold)
                {
                    highestThreshold = threshold;
                    stageIndex = i;
                    selectedStage = stage;
                }
            }

            if (selectedStage == null) return;

            if (!IsCooldownReady(selectedStage)) return;

            EntityPlayer target = null;
            if (RequiresTarget())
            {
                float maxRange = ApplyRangeMultiplier(GetMaxTargetRange(selectedStage));
                if (!TargetingSystem.TryFindTarget(maxRange, MinTargetRange, out target, out float dist))
                {
                    return;
                }
            }

            if (!CanActivateWithConditions(selectedStage, target)) return;

            SetAbilityActive(true);
            ActivateAbility(selectedStage, stageIndex, target);
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            if (stage.forms.Count == 0) return;

            // Pick random form from the list
            var form = stage.forms[entity.World.Rand.Next(stage.forms.Count)];

            // Store the new shape/texture in watched attributes for client sync
            entity.WatchedAttributes.SetString(CurrentShapeKey, form.shape ?? "");
            entity.WatchedAttributes.SetString(CurrentTextureKey, form.texture ?? "");
            entity.WatchedAttributes.SetBool(IsSwappedKey, true);
            entity.WatchedAttributes.SetLong(SwapEndTimeKey, entity.World.ElapsedMilliseconds + form.durationMs);
            entity.WatchedAttributes.MarkPathDirty(CurrentShapeKey);
            entity.WatchedAttributes.MarkPathDirty(CurrentTextureKey);
            entity.WatchedAttributes.MarkPathDirty(IsSwappedKey);

            // Play sound
            TryPlaySound(stage.swapSound, stage.soundRange, 0, 0.5f);

            // Schedule revert
            revertCallbackId = RegisterCallbackTracked(dt => RevertModel(), form.durationMs);

            // Mark this stage as used
            usedStages.Add(stageIndex);
            entity.WatchedAttributes.SetString(UsedStagesKey, string.Join(",", usedStages));
            entity.WatchedAttributes.MarkPathDirty(UsedStagesKey);

            // Trigger client visual update
            entity.MarkShapeModified();

            MarkCooldownStart();
        }

        private void RevertModel()
        {
            if (entity == null || !entity.Alive) return;

            // Clear the swapped shape/texture
            entity.WatchedAttributes.SetString(CurrentShapeKey, "");
            entity.WatchedAttributes.SetString(CurrentTextureKey, "");
            entity.WatchedAttributes.SetBool(IsSwappedKey, false);
            entity.WatchedAttributes.MarkPathDirty(CurrentShapeKey);
            entity.WatchedAttributes.MarkPathDirty(CurrentTextureKey);
            entity.WatchedAttributes.MarkPathDirty(IsSwappedKey);

            // Trigger client visual update
            entity.MarkShapeModified();

            revertCallbackId = 0;
        }

        protected override void StopAbility()
        {
            RevertModel();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            RevertModel();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            RevertModel();
            base.OnEntityDespawn(despawn);
        }

        // Client-side: override shape when swapped
        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            if (entity.World.Side == EnumAppSide.Server) return;

            var swappedShape = entity.WatchedAttributes.GetString(CurrentShapeKey);
            var swappedTexture = entity.WatchedAttributes.GetString(CurrentTextureKey);
            var isSwapped = entity.WatchedAttributes.GetBool(IsSwappedKey, false);

            if (!isSwapped || string.IsNullOrEmpty(swappedShape))
            {
                base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
                return;
            }

            // Load the swapped shape
            var capi = entity.Api as ICoreClientAPI;
            if (capi == null) return;

            var shapeLoc = AssetLocation.Create(swappedShape, "game");
            if (shapeLoc == null)
            {
                base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
                return;
            }

            var newShape = capi.Assets.TryGet(shapeLoc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"))?.ToObject<Shape>();
            if (newShape != null)
            {
                entityShape = newShape;
                shapeIsCloned = true;

                // Apply swapped texture if specified
                if (!string.IsNullOrEmpty(swappedTexture))
                {
                    var texLoc = AssetLocation.Create(swappedTexture, "game");
                    if (texLoc != null && entityShape.Textures != null)
                    {
                        entityShape.Textures["skin"] = texLoc;
                    }
                }
            }

            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        }
    }
}
