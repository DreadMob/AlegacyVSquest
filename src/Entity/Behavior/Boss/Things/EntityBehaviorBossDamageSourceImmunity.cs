using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class EntityBehaviorBossDamageSourceImmunity : EntityBehaviorBossBase
    {
        private bool ignoreWeather;
        private bool ignoreExplosion;
        private bool ignoreElectricity;
        private bool onlyAllowPlayerDamage;

        public EntityBehaviorBossDamageSourceImmunity(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdamagesourceimmunity";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            ignoreWeather = attributes?["ignoreWeather"].AsBool(true) ?? true;
            ignoreExplosion = attributes?["ignoreExplosion"].AsBool(true) ?? true;
            ignoreElectricity = attributes?["ignoreElectricity"].AsBool(true) ?? true;
            onlyAllowPlayerDamage = attributes?["onlyAllowPlayerDamage"].AsBool(false) ?? false;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0f) return;

            try
            {
                if (onlyAllowPlayerDamage)
                {
                    try
                    {
                        // Allow only direct player melee damage or player-caused projectile damage.
                        // DamageSource.GetCauseEntity() returns CauseEntity (e.g. thrower) or SourceEntity (e.g. attacker).
                        var cause = damageSource?.GetCauseEntity();
                        if (cause is not EntityPlayer)
                        {
                            damage = 0f;
                            return;
                        }
                    }
                    catch
                    {
                        // If we cannot resolve cause safely, default to blocking the damage when in strict mode.
                        damage = 0f;
                        return;
                    }
                }

                if (damageSource?.Source == EnumDamageSource.Weather && ignoreWeather)
                {
                    damage = 0f;
                    return;
                }

                if (damageSource?.Source == EnumDamageSource.Explosion && ignoreExplosion)
                {
                    damage = 0f;
                    return;
                }

                if (damageSource != null && ignoreElectricity)
                {
                    try
                    {
                        if (damageSource.Type == EnumDamageType.Electricity)
                        {
                            damage = 0f;
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
