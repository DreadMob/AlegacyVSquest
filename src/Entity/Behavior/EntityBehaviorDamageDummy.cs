using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    /// <summary>
    /// Damage dummy behavior: reports damage taken to chat and heals back to full instantly.
    /// Used for testing damage output from weapons/items.
    /// </summary>
    public class EntityBehaviorDamageDummy : EntityBehavior
    {
        public EntityBehaviorDamageDummy(Vintagestory.API.Common.Entities.Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "damagedummy";

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;

            // Suppress knockback
            if (damageSource != null)
            {
                damageSource.KnockbackStrength = 0f;
            }

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            // Identify attacker
            string attackerName = "Unknown";
            string sourceInfo = "";

            var causeEntity = damageSource.CauseEntity;
            var sourceEntity = damageSource.SourceEntity;

            if (causeEntity is EntityPlayer playerEntity)
            {
                attackerName = playerEntity.Player?.PlayerName ?? "Player";
            }

            if (sourceEntity != null && sourceEntity != causeEntity)
            {
                sourceInfo = $" (projectile: {sourceEntity.Code?.Path ?? "unknown"})";
            }

            string damageType = damageSource.Type.ToString();

            // Send message to all nearby players
            string message = $"[Dummy] {attackerName} dealt {damage:F2} {damageType} damage{sourceInfo}";

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                double dist = player.Entity.Pos.DistanceTo(entity.Pos.XYZ);
                if (dist < 60)
                {
                    sapi.SendMessage(player as IServerPlayer, 0, message, EnumChatType.Notification);
                }
            }

            // Heal back to full after taking damage
            entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
            
            // Schedule heal on next tick to ensure damage is processed first
            sapi.Event.RegisterCallback((dt) =>
            {
                if (entity.Alive)
                {
                    var healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
                    if (healthBehavior != null)
                    {
                        healthBehavior.Health = healthBehavior.MaxHealth;
                        healthBehavior.MarkDirty();
                    }
                }
            }, 50);

            base.OnEntityReceiveDamage(damageSource, ref damage);
        }
    }
}
