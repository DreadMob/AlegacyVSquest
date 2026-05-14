using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace VsQuest
{
    /// <summary>
    /// Handles trial shop network registration and server-side purchase logic.
    /// </summary>
    public class TrialShopNetworkHandler
    {
        public const string ChannelName = "alegacyvsquest:trialshop";

        private ICoreServerAPI sapi;

        public void RegisterServer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

            sapi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<OpenTrialShopMessage>()
                .RegisterMessageType<BuyTrialShopItemMessage>()
                .SetMessageHandler<BuyTrialShopItemMessage>(OnBuyRequest);
        }

        public void RegisterClient(ICoreClientAPI capi)
        {
            capi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<OpenTrialShopMessage>()
                .SetMessageHandler<OpenTrialShopMessage>(msg => TrialShopGui.ShowFromMessage(msg, capi))
                .RegisterMessageType<BuyTrialShopItemMessage>();
        }

        /// <summary>
        /// Send shop data to a player (called when player opens shop via NPC dialogue).
        /// </summary>
        public void SendShopToPlayer(IServerPlayer player)
        {
            if (sapi == null || player == null) return;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            var repManager = trialSystem.GetReputationManager();
            if (repManager == null) return;

            string playerUid = player.PlayerUID;
            int reputation = repManager.GetReputation(playerUid);
            int shards = repManager.GetVoidShards(playerUid);
            string rankName = repManager.GetRankName(playerUid);

            // Build shop items from NPC config (loaded by trial system)
            var shopItems = BuildShopItems(reputation, playerUid);

            sapi.Network.GetChannel(ChannelName).SendPacket(new OpenTrialShopMessage
            {
                Reputation = reputation,
                VoidShards = shards,
                RankName = rankName,
                ShopItems = shopItems
            }, player);
        }

        private TrialShopItemData[] BuildShopItems(int playerReputation, string playerUid)
        {
            // TODO: Load from NPC config JSON. For now, hardcoded shop structure
            // that matches the trialwarden.json config.
            // In production, this should read from the loaded NPC config.
            var items = new List<TrialShopItemData>
            {
                new TrialShopItemData
                {
                    ItemCode = "albase:trial-tracker",
                    NameKey = "albase:trial-tracker-name",
                    Cost = 50,
                    RequiredReputation = 0,
                    MaxPurchases = 1,
                    IsLocked = false
                },
                new TrialShopItemData
                {
                    ItemCode = "albase:trial-bow-dim",
                    NameKey = "albase:trial-bow",
                    Cost = 80,
                    RequiredReputation = 100,
                    IsLocked = playerReputation < 100,
                    MaxPurchases = 1
                },
                new TrialShopItemData
                {
                    ItemCode = "albase:trial-case-tier1",
                    NameKey = "albase:trial-case-tier1",
                    Cost = 25,
                    RequiredReputation = 0,
                    IsLocked = false,
                    MaxPurchases = -1
                },
                new TrialShopItemData
                {
                    ItemCode = "albase:trial-case-tier2",
                    NameKey = "albase:trial-case-tier2",
                    Cost = 50,
                    RequiredReputation = 100,
                    IsLocked = playerReputation < 100,
                    MaxPurchases = -1
                },
                new TrialShopItemData
                {
                    ItemCode = "albase:trial-case-tier3",
                    NameKey = "albase:trial-case-tier3",
                    Cost = 100,
                    RequiredReputation = 300,
                    IsLocked = playerReputation < 300,
                    MaxPurchases = -1
                }
            };

            return items.ToArray();
        }

        private void OnBuyRequest(IServerPlayer player, BuyTrialShopItemMessage message)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.ItemCode)) return;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            var repManager = trialSystem.GetReputationManager();
            if (repManager == null) return;

            string playerUid = player.PlayerUID;

            // Verify player can afford
            if (!repManager.SpendVoidShards(playerUid, message.Cost))
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup,
                    LocalizationUtils.GetSafe("albase:trial-shop-not-enough-shards"), EnumChatType.Notification);
                return;
            }

            // Give item
            try
            {
                var item = sapi.World.GetItem(new AssetLocation(message.ItemCode));
                if (item == null)
                {
                    // Refund
                    repManager.AddReputationAndShards(playerUid, 0); // Can't add just shards with current API
                    sapi.Logger.Warning("[TrialShop] Item not found: {0}", message.ItemCode);
                    return;
                }

                var stack = new ItemStack(item, 1);
                if (!player.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                }

                string itemName = stack.GetName();
                string msg = LocalizationUtils.GetSafe("albase:trial-shop-purchased", itemName, message.Cost);
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialShop] Purchase failed: {0}", ex.Message);
            }
        }
    }
}
