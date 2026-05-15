using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace VsQuest
{
    /// <summary>
    /// Handles trial shop network registration and server-side purchase logic.
    /// Loads shop config from config/npcs/*.json (TrialNpcConfig).
    /// </summary>
    public class TrialShopNetworkHandler
    {
        public const string ChannelName = "alegacyvsquest:trialshop";

        private ICoreServerAPI sapi;
        private TrialNpcConfig npcConfig;

        public void RegisterServer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;

            sapi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<OpenTrialShopMessage>()
                .RegisterMessageType<BuyTrialShopItemMessage>()
                .SetMessageHandler<BuyTrialShopItemMessage>(OnBuyRequest);

            LoadNpcConfig();
        }

        public void RegisterClient(ICoreClientAPI capi)
        {
            capi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<OpenTrialShopMessage>()
                .SetMessageHandler<OpenTrialShopMessage>(msg => TrialShopGui.ShowFromMessage(msg, capi))
                .RegisterMessageType<BuyTrialShopItemMessage>();
        }

        private void LoadNpcConfig()
        {
            if (sapi == null) return;

            try
            {
                // Try direct asset load with manual JSON deserialization
                var asset = sapi.Assets.TryGet(new Vintagestory.API.Common.AssetLocation("albase", "config/npcs/trialwarden.json"));
                if (asset != null)
                {
                    string json = asset.ToText();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<TrialNpcConfig>(json);
                        if (cfg != null && !string.IsNullOrWhiteSpace(cfg.npcId) && cfg.shop?.items?.Count > 0)
                        {
                            npcConfig = cfg;
                            sapi.Logger.Notification("[TrialShop] Loaded shop config (direct): {0} items", cfg.shop.items.Count);
                            return;
                        }
                    }
                }

                // Fallback: try GetMany
                foreach (var mod in sapi.ModLoader.Mods)
                {
                    var assets = sapi.Assets.GetMany<TrialNpcConfig>(sapi.Logger, "config/npcs", mod.Info.ModID);
                    if (assets == null) continue;

                    foreach (var a in assets)
                    {
                        var cfg = a.Value;
                        if (cfg == null) continue;
                        if (string.IsNullOrWhiteSpace(cfg.npcId)) continue;
                        if (cfg.shop == null || cfg.shop.items == null || cfg.shop.items.Count == 0) continue;

                        if (string.Equals(cfg.npcId, "albase:trialwarden", StringComparison.OrdinalIgnoreCase))
                        {
                            npcConfig = cfg;
                            sapi.Logger.Notification("[TrialShop] Loaded shop config (GetMany): {0} items from mod {1}", cfg.shop.items.Count, mod.Info.ModID);
                            return;
                        }
                    }
                }

                sapi.Logger.Warning("[TrialShop] No shop config found for 'albase:trialwarden' in any mod.");
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialShop] Failed to load NPC config: {0}", ex.Message);
            }
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
            // Lazy-load if config wasn't available at startup
            if (npcConfig == null)
            {
                LoadNpcConfig();
            }

            if (npcConfig?.shop?.items == null || npcConfig.shop.items.Count == 0)
                return Array.Empty<TrialShopItemData>();

            var repManager = sapi.ModLoader.GetModSystem<HollowTrialSystem>()?.GetReputationManager();

            var items = new List<TrialShopItemData>(npcConfig.shop.items.Count);
            foreach (var cfg in npcConfig.shop.items)
            {
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.itemCode)) continue;

                int purchases = repManager?.GetPurchaseCount(playerUid, cfg.itemCode) ?? 0;

                items.Add(new TrialShopItemData
                {
                    ItemCode = cfg.itemCode,
                    NameKey = cfg.nameKey,
                    Cost = cfg.cost,
                    RequiredReputation = cfg.requiredReputation,
                    IsLocked = playerReputation < cfg.requiredReputation,
                    MaxPurchases = cfg.maxPurchases,
                    PurchasesMade = purchases
                });
            }

            return items.ToArray();
        }

        /// <summary>
        /// Public method for QuestGiverMessageBuilder to get shop items for a player.
        /// </summary>
        public List<TrialShopItemData> BuildShopItemsForMessage(string playerUid)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(playerUid)) return null;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            var repManager = trialSystem?.GetReputationManager();
            if (repManager == null) return null;

            int reputation = repManager.GetReputation(playerUid);
            var arr = BuildShopItems(reputation, playerUid);
            return arr != null ? new List<TrialShopItemData>(arr) : null;
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

            // Find the item in config
            var itemCfg = npcConfig?.shop?.items?.FirstOrDefault(it =>
                string.Equals(it?.itemCode, message.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (itemCfg == null)
            {
                sapi.Logger.Warning("[TrialShop] Unknown item: {0}", message.ItemCode);
                return;
            }

            // Verify reputation
            if (repManager.GetReputation(playerUid) < itemCfg.requiredReputation)
            {
                Msg(player, LocalizationUtils.GetSafe("albase:trial-warden-shop-locked"));
                return;
            }

            // Verify purchase limit
            if (itemCfg.maxPurchases > 0)
            {
                int purchases = repManager.GetPurchaseCount(playerUid, itemCfg.itemCode);
                if (purchases >= itemCfg.maxPurchases) return;
            }

            // Verify and spend shards
            if (!repManager.SpendVoidShards(playerUid, itemCfg.cost))
            {
                Msg(player, LocalizationUtils.GetSafe("albase:trial-shop-not-enough-shards"));
                return;
            }

            // Process purchase: virtual case OR plain item
            try
            {
                if (itemCfg.itemCode.StartsWith("case:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ProcessCasePurchase(player, itemCfg))
                    {
                        // Refund on failure
                        repManager.RefundVoidShards(playerUid, itemCfg.cost);
                        return;
                    }
                }
                else
                {
                    if (!GivePlainItem(player, itemCfg.itemCode))
                    {
                        repManager.RefundVoidShards(playerUid, itemCfg.cost);
                        return;
                    }
                }

                repManager.RecordPurchase(playerUid, itemCfg.itemCode);

                // Notification
                string itemName = !string.IsNullOrWhiteSpace(itemCfg.nameKey)
                    ? LocalizationUtils.GetSafe(itemCfg.nameKey)
                    : itemCfg.itemCode;
                Msg(player, LocalizationUtils.GetSafe("albase:trial-shop-purchased", itemName, itemCfg.cost));
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialShop] Purchase failed: {0}", ex.Message);
                repManager.RefundVoidShards(playerUid, itemCfg.cost);
            }
        }

        private bool GivePlainItem(IServerPlayer player, string itemCode)
        {
            var item = sapi.World.GetItem(new AssetLocation(itemCode));
            if (item == null)
            {
                sapi.Logger.Warning("[TrialShop] Item not found: {0}", itemCode);
                return false;
            }

            var stack = new ItemStack(item, 1);
            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
            return true;
        }

        /// <summary>
        /// Open a virtual case: roll quality, pick random item from pool, give it.
        /// Sends the reroll-style animation packet for visual feedback.
        /// </summary>
        private bool ProcessCasePurchase(IServerPlayer player, TrialShopConfigItem itemCfg)
        {
            if (itemCfg.casePool == null || itemCfg.casePool.Length == 0)
            {
                sapi.Logger.Warning("[TrialShop] Case '{0}' has empty pool", itemCfg.itemCode);
                return false;
            }

            // Pick random actionitem ID from pool
            string actionItemId = itemCfg.casePool[sapi.World.Rand.Next(itemCfg.casePool.Length)];

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (itemSystem == null) return false;

            // Resolve via ActionItemRegistry
            if (!itemSystem.ActionItemRegistry.TryGetValue(actionItemId, out var actionItem))
            {
                // Fallback: give raw item
                var rawItem = sapi.World.GetItem(new AssetLocation(actionItemId));
                if (rawItem == null)
                {
                    sapi.Logger.Warning("[TrialShop] Case item not found: {0}", actionItemId);
                    return false;
                }
                var rawStack = new ItemStack(rawItem, 1);
                if (!player.InventoryManager.TryGiveItemstack(rawStack))
                    sapi.World.SpawnItemEntity(rawStack, player.Entity.Pos.XYZ);
                return true;
            }

            // Resolve base collectible
            if (!ItemAttributeUtils.TryResolveCollectible(sapi, actionItem.itemCode, out var collectible))
            {
                sapi.Logger.Warning("[TrialShop] Base item not found for actionitem: {0}", actionItem.itemCode);
                return false;
            }

            var stack = new ItemStack(collectible);
            ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

            // Apply quality via standard quality service
            var qualityService = itemSystem.QualityService;
            qualityService?.TryApplyQuality(stack, actionItem, sapi.World.Rand);

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }

            // Notification
            string itemName = collectible.GetHeldItemName(stack);
            Msg(player, LocalizationUtils.GetSafe("albase:trial-shard-opened", itemName));

            return true;
        }

        private void Msg(IServerPlayer player, string text)
        {
            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, text, EnumChatType.Notification);
        }
    }
}
