using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DamageQuestGiverAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "damagequestgiver";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            float damage = 10f;
            if (!float.TryParse(args[0], out damage))
            {
                damage = 10f;
            }

            float delaySeconds = 0f;
            if (args.Length >= 2 && !float.TryParse(args[1], out delaySeconds))
            {
                delaySeconds = 0f;
            }

            // Get quest giver entity from message
            Entity targetEntity = null;
            if (message?.questGiverId > 0)
            {
                targetEntity = sapi.World.GetEntityById(message.questGiverId);
            }

            if (targetEntity == null)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.InfoLogChatGroup, 
                    Lang.Get("alegacyvsquest:damagequestgiver-not-found"), EnumChatType.Notification);
                return;
            }

            if (delaySeconds > 0)
            {
                // Schedule damage after delay
                sapi.World.RegisterCallback((dt) =>
                {
                    DealDamage(targetEntity, byPlayer.Entity, damage);
                }, (int)(delaySeconds * 1000));
            }
            else
            {
                DealDamage(targetEntity, byPlayer.Entity, damage);
            }
        }

        private void DealDamage(Entity target, Entity source, float damage)
        {
            if (target == null || !target.Alive) return;

            target.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                Type = EnumDamageType.PiercingAttack,
                DamageTier = 99,
                KnockbackStrength = 0f
            }, damage);
        }
    }
}
