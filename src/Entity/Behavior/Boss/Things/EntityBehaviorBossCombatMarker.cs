using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossCombatMarker : EntityBehaviorBossCombatMarkerBase
    {
        public const string BossCombatAttackersKey = "alegacyvsquest:bosscombat:attackers";
        public const string BossCombatDamageByPlayerKey = "alegacyvsquest:bosscombat:damageByPlayer";
        public const string BossCombatLastDamageMsKey = "alegacyvsquest:bosscombat:lastDamageMs";

        protected override string AttackersKey => BossCombatAttackersKey;
        protected override string DamageByPlayerKey => BossCombatDamageByPlayerKey;
        protected override string LastDamageMsKey => BossCombatLastDamageMsKey;
        protected override bool TrackCombatTime => trackCombatTime;

        private bool trackCombatTime;

        public EntityBehaviorBossCombatMarker(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            trackCombatTime = attributes["trackCombatTime"].AsBool(true);
        }

        public override string PropertyName() => "bosscombatmarker";
    }
}
