using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Generic action for opening trial reward containers (Rift Shards).
    /// Fully data-driven: reads tier and item pool from the item's JSON attributes.
    /// Uses the existing reroll animation system for visual feedback.
    /// 
    /// Required item attributes:
    ///   "alegacyvsquest:trialcase:tier": int (1-3, affects quality roll chances)
    ///   "alegacyvsquest:trialcase:possibleItems": string[] (item codes without quality suffix)
    /// </summary>
    public class OpenTrialCaseAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player?.Entity == null) return;

            var heldSlot = player.Entity.RightHandItemSlot;
            var heldItem = heldSlot?.Itemstack;

            if (heldItem?.Item?.Attributes == null) return;

            var itemAttrs = heldItem.Item.Attributes;
            int tier = itemAttrs["alegacyvsquest:trialcase:tier"].AsInt(1);
            var possibleItems = itemAttrs["alegacyvsquest:trialcase:possibleItems"].AsArray<string>();

            if (possibleItems == null || possibleItems.Length == 0) return;

            // Roll quality based on tier
            int quality = TrialQualityRoller.Roll(tier, null, sapi.World.Rand);

            string qualitySuffix = quality switch
            {
                1 => "dim",
                2 => "shimmer",
                3 => "radiant",
                4 => "abyssal",
                _ => "dim"
            };

            // Pick random item from pool
            string selectedItemCode = possibleItems[sapi.World.Rand.Next(possibleItems.Length)];
            string resultItemCode = selectedItemCode + "-" + qualitySuffix;

            // Build animation data (all possible items for the spinning animation)
            var allItemCodes = new List<string>();
            var allItemNames = new List<string>();
            string[] suffixes = { "dim", "shimmer", "radiant", "abyssal" };

            foreach (var itemCode in possibleItems)
            {
                foreach (var suffix in suffixes)
                {
                    string code = itemCode + "-" + suffix;
                    allItemCodes.Add(code);
                    allItemNames.Add(LocalizationUtils.GetSafe("albase:trial-quality-" + suffix));
                }
            }

            string resultItemName = LocalizationUtils.GetSafe("albase:trial-quality-" + qualitySuffix);

            // Send animation to client and give item
            // The animation is purely visual — item is given immediately on server
            // (unlike reroll which waits for claim, we give instantly since it's a purchase)
            try
            {
                // Give item immediately
                var item = sapi.World.GetItem(new AssetLocation(resultItemCode));
                if (item == null)
                {
                    item = sapi.World.GetItem(new AssetLocation(selectedItemCode));
                    resultItemCode = selectedItemCode;
                }

                if (item != null)
                {
                    var stack = new ItemStack(item, 1);
                    if (!player.InventoryManager.TryGiveItemstack(stack))
                    {
                        sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                    }
                }

                // Send animation to client (cosmetic only)
                sapi.Network.GetChannel(VsQuestNetworkRegistry.RerollChannelName).SendPacket(new StartRerollAnimationMessage
                {
                    ItemIds = allItemCodes.ToArray(),
                    ItemNames = allItemNames.ToArray(),
                    ItemCodes = allItemCodes.ToArray(),
                    ResultItemId = resultItemCode,
                    ResultItemName = resultItemName,
                    ResultItemCode = resultItemCode,
                    AnimationType = "simplespin",
                    GroupId = "" // empty = no claim needed, item already given
                }, player);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[OpenTrialCase] Failed: {0}", ex.Message);
                // Fallback
                GiveItemDirectly(sapi, player, resultItemCode, qualitySuffix);
            }
        }

        private void GiveItemDirectly(ICoreServerAPI sapi, IServerPlayer player, string fullItemCode, string qualitySuffix)
        {
            try
            {
                var item = sapi.World.GetItem(new AssetLocation(fullItemCode));
                if (item == null) return;

                var stack = new ItemStack(item, 1);
                if (!player.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                }

                string qualityName = LocalizationUtils.GetSafe("albase:trial-quality-" + qualitySuffix);
                string colorHex = TrialQualityRoller.GetColorHex(int.Parse(qualitySuffix switch
                {
                    "dim" => "1", "shimmer" => "2", "radiant" => "3", "abyssal" => "4", _ => "1"
                }));
                string msg = LocalizationUtils.GetSafe("albase:trial-shard-opened", $"<font color=\"{colorHex}\">{qualityName}</font>");
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[OpenTrialCase] Fallback give failed: {0}", ex.Message);
            }
        }
    }
}
