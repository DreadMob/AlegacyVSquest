using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossHuntCombatMarker : EntityBehaviorBossCombatMarkerBase
    {
        public const string BossHuntAttackersKey = "alegacyvsquest:bosshunt:attackers";
        public const string BossHuntDamageByPlayerKey = "alegacyvsquest:bosshunt:damageByPlayer";
        public const string BossHuntLastDamageMsKey = "alegacyvsquest:bosshunt:lastDamageMs";

        protected override string AttackersKey => BossHuntAttackersKey;
        protected override string DamageByPlayerKey => BossHuntDamageByPlayerKey;
        protected override string LastDamageMsKey => BossHuntLastDamageMsKey;

        public EntityBehaviorBossHuntCombatMarker(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosshuntcombatmarker";
    }
}
