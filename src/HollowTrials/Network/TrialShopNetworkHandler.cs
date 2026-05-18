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

            // Retry loading after save game is loaded (assets from content mods may not be available yet)
            if (npcConfig == null)
            {
                sapi.Event.SaveGameLoaded += () =>
                {
                    if (npcConfig == null)
                    {
                        LoadNpcConfig();
                    }
                };
            }
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
                // Approach 1: Direct asset load via TryGet
                IAsset asset = null;
                string[] pathsToTry = new[]
                {
                    "config/npcs/trialwarden.json",
                    "config/npcs/trialwarden"
                };

                foreach (var path in pathsToTry)
                {
                    try
                    {
                        asset = sapi.Assets.TryGet(new AssetLocation("albase", path));
                        if (asset != null) break;
                    }
                    catch { }
                }

                if (asset != null)
                {
                    string json = asset.ToText();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<TrialNpcConfig>(json);
                        if (cfg?.shop?.items?.Count > 0)
                        {
                            npcConfig = cfg;
                            sapi.Logger.Notification("[TrialShop] Loaded shop config from asset: {0} items", cfg.shop.items.Count);
                            return;
                        }
                    }
                }

                // Approach 2: Scan all loaded assets for config/npcs files
                try
                {
                    var allAssets = sapi.Assets.GetMany<TrialNpcConfig>(sapi.Logger, "config/npcs");
                    if (allAssets != null)
                    {
                        foreach (var kvp in allAssets)
                        {
                            var cfg = kvp.Value;
                            if (cfg?.shop?.items?.Count > 0)
                            {
                                npcConfig = cfg;
                                sapi.Logger.Notification("[TrialShop] Loaded shop config via GetMany: {0} items from {1}", cfg.shop.items.Count, kvp.Key);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Debug("[TrialShop] GetMany scan failed: {0}", ex.Message);
                }

                // Approach 3: Hardcoded fallback — ensures shop always works
                sapi.Logger.Warning("[TrialShop] Asset load failed. Using hardcoded fallback config.");
                npcConfig = CreateFallbackConfig();
                sapi.Logger.Notification("[TrialShop] Fallback config loaded: {0} items", npcConfig.shop.items.Count);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialShop] LoadNpcConfig exception: {0}. Using fallback.", ex.Message);
                npcConfig = CreateFallbackConfig();
            }
        }

        /// <summary>
        /// Hardcoded fallback config matching quests/albase/assets/albase/config/npcs/trialwarden.json.
        /// This ensures the shop always works even if asset loading fails.
        /// </summary>
        private static TrialNpcConfig CreateFallbackConfig()
        {
            return new TrialNpcConfig
            {
                npcId = "albase:trialwarden",
                nameKey = "albase:trial-warden-name",
                titleKey = "albase:trial-warden-title",
                questGiver = true,
                trialActiveOnly = true,
                trialMaxTier = 3,
                shop = new TrialShopConfigBlock
                {
                    currencyKey = "albase:trial-currency-name",
                    items = new List<TrialShopConfigItem>
                    {
                        new TrialShopConfigItem { itemCode = "albase:trial-tracker", nameKey = "albase:trial-tracker-name", cost = 30, requiredReputation = 0, maxPurchases = 1 },
                        new TrialShopConfigItem { itemCode = "case:tier1", nameKey = "albase:trial-case-tier1", cost = 20, requiredReputation = 0, maxPurchases = -1, caseTier = 1, casePool = new[] { "albase:trial-shadow-earring", "albase:trial-rift-bracelet", "albase:trial-void-pendant" } },
                        new TrialShopConfigItem { itemCode = "case:tier2", nameKey = "albase:trial-case-tier2", cost = 50, requiredReputation = 100, maxPurchases = -1, caseTier = 2, casePool = new[] { "albase:trial-void-cloak", "albase:trial-abyss-belt", "albase:trial-deep-sigil" } },
                        new TrialShopConfigItem { itemCode = "case:tier3", nameKey = "albase:trial-case-tier3", cost = 120, requiredReputation = 300, maxPurchases = -1, caseTier = 3, casePool = new[] { "albase:trial-void-cloak", "albase:trial-abyss-belt", "albase:trial-deep-sigil", "albase:trial-bow", "albase:trial-abyss-pendant", "albase:trial-void-ring" } },
                        new TrialShopConfigItem { itemCode = "albase:trial-bow", nameKey = "item-albase:trial-bow", cost = 150, requiredReputation = 400, maxPurchases = 1 },
                        new TrialShopConfigItem { itemCode = "albase:trial-abyss-mask", nameKey = "albase:trial-abyss-mask-name", cost = 200, requiredReputation = 600, maxPurchases = 1 }
                    }
                }
            };
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
                sapi?.Logger?.Debug("[TrialShop] BuildShopItems: npcConfig is null, attempting lazy-load...");
                LoadNpcConfig();
            }

            if (npcConfig?.shop?.items == null || npcConfig.shop.items.Count == 0)
            {
                sapi?.Logger?.Debug("[TrialShop] BuildShopItems: no items available. npcConfig={0}", npcConfig == null ? "null" : "loaded");
                return Array.Empty<TrialShopItemData>();
            }

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
            if (sapi == null || string.IsNullOrWhiteSpace(playerUid)) return new List<TrialShopItemData>();

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            var repManager = trialSystem?.GetReputationManager();

            int reputation = repManager?.GetReputation(playerUid) ?? 0;
            var arr = BuildShopItems(reputation, playerUid);
            return arr != null && arr.Length > 0 ? new List<TrialShopItemData>(arr) : new List<TrialShopItemData>();
        }

        private void OnBuyRequest(IServerPlayer player, BuyTrialShopItemMessage message)
        {
            if (sapi == null || player == null || message == null) return;

            // Empty ItemCode = "open shop" request
            if (string.IsNullOrWhiteSpace(message.ItemCode))
            {
                SendShopToPlayer(player);
                return;
            }

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
