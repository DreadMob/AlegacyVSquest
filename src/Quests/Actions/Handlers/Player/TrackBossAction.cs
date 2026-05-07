using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class TrackBossAction : PlayerActionBase
    {
        private const float HpCost = 3f;

        protected override int MinArgs => 0;
        protected override string ActionName => "trackboss";

        private static string GetLorePrefixKey(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return "alegacyvsquest:trackboss-prefix-default";

            // Data-driven approach: Check if a specialized prefix exists in localization files.
            // For example: "alegacyvsquest:trackboss-prefix-ossuarywarden"
            string prefixKey = "alegacyvsquest:trackboss-prefix-" + bossKey.ToLowerInvariant();
            if (Lang.HasTranslation(prefixKey))
            {
                return prefixKey;
            }

            return "alegacyvsquest:trackboss-prefix-default";
        }

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            string bossKey = args.Length >= 1 ? args[0] : null;

            var playerEntity = player.Entity;
            double nowHours = sapi.World.Calendar.TotalHours;

            var healthBh = playerEntity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh != null && healthBh.Health <= HpCost)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-not-enough-health")
                }, player);
                return;
            }

            var bossSystem = sapi.ModLoader.GetModSystem<BossHuntSystem>();
            if (bossSystem == null)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-system-unavailable")
                }, player);
                return;
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                bossKey = bossSystem.GetActiveBossKey();
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-no-active-target")
                }, player);
                return;
            }

            var activeQuestId = bossSystem.GetActiveBossQuestId();
            if (!string.IsNullOrWhiteSpace(activeQuestId))
            {
                var active = sapi.ModLoader.GetModSystem<QuestSystem>()?.GetPlayerQuests(player.PlayerUID);
                bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, activeQuestId, StringComparison.OrdinalIgnoreCase));
                if (!isActive)
                {
                    sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                    {
                        Notification = Lang.Get("alegacyvsquest:trackboss-only-during-hunt")
                    }, player);
                    return;
                }
            }

            if (!bossSystem.TryGetBossPosition(bossKey, out Vec3d bossPos, out int bossDim, out bool isLiveEntity))
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-trail-not-found")
                }, player);
                return;
            }

            int playerDim = playerEntity.Pos?.Dimension ?? 0;
            if (playerDim != bossDim)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-different-dimension")
                }, player);
                return;
            }

            Vec3d playerPos = new Vec3d(playerEntity.Pos.X, playerEntity.Pos.Y, playerEntity.Pos.Z);
            double dx = playerPos.X - bossPos.X;
            double dy = playerPos.Y - bossPos.Y;
            double dz = playerPos.Z - bossPos.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            playerEntity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury,
                DamageTier = 0,
                KnockbackStrength = 0f,
                IgnoreInvFrames = true
            }, HpCost);

            string liveSuffix = isLiveEntity ? "" : Lang.Get("alegacyvsquest:trackboss-trail-suffix");
            string prefix = Lang.Get(GetLorePrefixKey(bossKey));
            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
            {
                Notification = Lang.Get("alegacyvsquest:trackboss-distance-lore", prefix, liveSuffix, dist)
            }, player);
        }
    }
}
