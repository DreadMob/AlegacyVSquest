using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class EntityBehaviorBossHealthbarOverride : EntityBehavior
    {
        private bool showHealthbar = true;

        public EntityBehaviorBossHealthbarOverride(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            showHealthbar = attributes?["show"].AsBool(true) ?? true;

            Apply();
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            Apply();
        }

        private void Apply()
        {
            try
            {
                entity?.WatchedAttributes?.SetBool("showHealthbar", showHealthbar);
                entity?.WatchedAttributes?.MarkPathDirty("showHealthbar");
            }
            catch (Exception ex)
            {
                entity?.World?.Logger?.Warning("[EntityBehaviorBossHealthbarOverride] Failed to set healthbar visibility: {0}", ex.Message);
            }
        }

        public override string PropertyName() => "alegacyvsquestbosshealthbar";
    }
}
