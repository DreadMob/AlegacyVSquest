using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Item merge charging patches - handles Second Chance and Uranium Mask charging mechanics.
    /// </summary>
    public class ItemMergeChargePatches
    {
        // Static API reference set by QuestSystem
        public static ICoreAPI API;

        [HarmonyPatch(typeof(CollectibleObject), "GetMergableQuantity")]
        public class CollectibleObject_GetMergableQuantity_CustomMerge_Patch
        {
            public static bool Prefix(
                CollectibleObject __instance,
                ItemStack sinkStack,
                ItemStack sourceStack,
                EnumMergePriority priority,
                ref int __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_TryMergeStacks_SecondChanceCharge)) return true;
                if (priority != EnumMergePriority.DirectMerge) return true;
                if (sinkStack == null || sourceStack == null) return true;

                // Allow direct merge when custom repair material matches and item is not fully repaired yet.
                if (CanRepairWithCustomItem(sinkStack, sourceStack, out _))
                {
                    __result = 1;
                    return false;
                }

                // Ensure special charge paths can also enter TryMergeStacks on newer VS merge flow.
                if (CanChargeSecondChance(sinkStack, sourceStack) || CanChargeCryptSightMask(sinkStack, sourceStack))
                {
                    __result = 1;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "TryMergeStacks")]
        public class CollectibleObject_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(CollectibleObject __instance, ItemStackMergeOperation op)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_TryMergeStacks_SecondChanceCharge)) return true;
                if (op?.CurrentPriority != EnumMergePriority.DirectMerge) return true;
                
                //API?.Logger.Debug($"[vsquest] TryMergeStacks called: sink={op?.SinkSlot?.Itemstack?.Collectible?.Code}, source={op?.SourceSlot?.Itemstack?.Collectible?.Code}");
                
                // Try custom repair first (for items with repairItemCode)
                if (TryHandleCustomItemRepair(op))
                {
                    API?.Logger.Notification($"[vsquest] Custom repair handled");
                    return false;
                }
                
                // Try Second Chance charge
                if (TryHandleSecondChanceCharge(op))
                {
                    API?.Logger.Notification($"[vsquest] Second Chance charge handled");
                    return false;
                }
                
                // Try Crypt Sight Mask charge
                if (TryHandleCryptSightMaskCharge(op))
                {
                    API?.Logger.Notification($"[vsquest] Crypt Sight charge handled");
                    return false;
                }
                
                // Block vanilla repair if item has custom repairItemCode
                if (HasCustomRepairItem(op?.SinkSlot?.Itemstack))
                {
                    API?.Logger.Debug($"[vsquest] Blocking vanilla repair (has custom repairItemCode)");
                    return false;
                }
                
                return true;
            }
        }

        // ItemWearable merge patches removed - obsolete in 1.22
        // ItemWearable inherits TryMergeStacks/GetMergableQuantity from CollectibleObject
        // The CollectibleObject patch above handles all items including wearables

        private static bool TryHandleSecondChanceCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeSecondChance(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            sinkStack.Attributes.SetFloat(chargeKey, 1f);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            
            API?.Logger.Notification($"[vsquest] Second Chance mask charged: {sinkStack.Collectible.Code}");
            
            return true;
        }

        private static bool TryHandleCryptSightMaskCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeCryptSightMask(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            float hours = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            hours = System.Math.Min(100f, hours + 8f);
            sinkStack.Attributes.SetFloat(chargeKey, hours);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            
            API?.Logger.Notification($"[vsquest] Crypt Sight mask charged: {sinkStack.Collectible.Code} ({hours:F1} hours)");
            
            return true;
        }

        private static bool CanChargeSecondChance(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            // Check if this is a Second Chance mask by item code
            if (!IsSecondChanceMask(sinkStack.Collectible.Code))
            {
                //API?.Logger.Debug($"[vsquest] Not a Second Chance mask: {sinkStack.Collectible.Code}");
                return false;
            }

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            // Initialize attribute if missing (for items from creative inventory)
            if (!sinkStack.Attributes.HasAttribute(chargeKey))
            {
                sinkStack.Attributes.SetFloat(chargeKey, 0f);
            }

            if (!IsDiamondRough(sourceStack.Collectible.Code))
            {
                API?.Logger.Debug($"[vsquest] Source is not diamond-rough: {sourceStack.Collectible.Code}");
                return false;
            }
            
            if (!HasHighPotential(sourceStack))
            {
                API?.Logger.Debug($"[vsquest] Diamond does not have high potential");
                return false;
            }

            float charges = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrSecondChanceCharges, 0f);
            if (charges >= 0.5f)
            {
                API?.Logger.Debug($"[vsquest] Mask already charged: {charges}");
                return false;
            }
            
            return true;
        }

        private static bool CanChargeCryptSightMask(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            // Check if this is a Crypt Sight Mask by item code
            if (!IsCryptSightMask(sinkStack.Collectible.Code)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            // Initialize attribute if missing (for items from creative inventory)
            if (!sinkStack.Attributes.HasAttribute(chargeKey))
            {
                sinkStack.Attributes.SetFloat(chargeKey, 0f);
            }

            if (!IsPhosphoriteChunk(sourceStack.Collectible.Code)) return false;

            float hours = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrUraniumMaskChargeHours, 0f);
            return hours < 100f;
        }

        private static bool IsDiamondRough(AssetLocation code)
        {
            return code.Path.Contains("diamond-rough") || code.Path.Contains("rough-diamond");
        }

        private static bool HasHighPotential(ItemStack stack)
        {
            // Check if the diamond has high potential (for second chance charging)
            string potential = stack.Attributes?.GetString("potential");
            if (!string.IsNullOrEmpty(potential))
            {
                return potential == "high" || potential == "veryhigh" || potential == "excellent";
            }
            // If no potential attribute, assume it's valid (for backward compatibility)
            return true;
        }

        private static bool IsPhosphoriteChunk(AssetLocation code)
        {
            // Accept: ore-*-phosphorite-*, chunk-phosphorite, gem-phosphorite-*
            string path = code.Path;
            if (!path.Contains("phosphorite")) return false;
            return path.Contains("chunk") || path.Contains("ore") || path.Contains("nugget") || path.Contains("gem");
        }

        private static bool IsCryptSightMask(AssetLocation code)
        {
            return code.Path.Contains("uranium-mask");
        }

        private static bool IsSecondChanceMask(AssetLocation code)
        {
            return code.Path.Contains("2cnah-mask");
        }

        /// <summary>
        /// Handles custom item repair based on repairItemCode attribute.
        /// Items can specify repairItemCode to define what item repairs them.
        /// repairItemCode can be:
        ///   - string: "gear-temporal" (repairs 100%)
        ///   - object: { "code": "gear-temporal", "strength": 0.4 } (repairs 40%)
        /// </summary>
        private static bool TryHandleCustomItemRepair(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            var sourceStack = op.SourceSlot.Itemstack;

            if (!CanRepairWithCustomItem(sinkStack, sourceStack, out float repairStrength)) return false;

            // Repair the item
            // Initialize condition if it doesn't exist
            if (!sinkStack.Attributes.HasAttribute("condition"))
            {
                sinkStack.Attributes.SetFloat("condition", 0f);
            }
            
            float currentCondition = sinkStack.Attributes.GetFloat("condition", 0f);
            float newCondition = System.Math.Min(1f, currentCondition + repairStrength);
            sinkStack.Attributes.SetFloat("condition", newCondition);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            
            API?.Logger.Notification($"[vsquest] Item repaired: {sinkStack.Collectible.Code} ({currentCondition:F2} -> {newCondition:F2}) using {sourceStack.Collectible.Code}");
            
            return true;
        }

        private static bool CanRepairWithCustomItem(ItemStack sinkStack, ItemStack sourceStack, out float repairStrength)
        {
            repairStrength = 1f;
            if (sinkStack?.Collectible?.Code == null || sourceStack?.Collectible?.Code == null) return false;

            // Check if sink item has repairItemCode attribute
            var repairItemAttr = sinkStack.ItemAttributes?["repairItemCode"];
            if (repairItemAttr == null || !repairItemAttr.Exists)
            {
                return false;
            }

            string repairItemCode;
            
            // ItemAttributes are JsonObject (collectible JSON), so object form is read via JsonObject keys.
            if (repairItemAttr["code"]?.Exists == true)
            {
                repairItemCode = repairItemAttr["code"].AsString(null);
                repairStrength = repairItemAttr["strength"]?.AsFloat(1f) ?? 1f;
            }
            else
            {
                // Simple string format
                repairItemCode = repairItemAttr.AsString(null);
            }
            
            if (string.IsNullOrEmpty(repairItemCode)) return false;

            // Check if source item matches the repair item code
            string sourceCode = sourceStack.Collectible.Code.ToString();
            string sourcePath = sourceStack.Collectible.Code.Path;
            
            // Match against full code or path only
            bool matches = string.Equals(sourceCode, repairItemCode, System.StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(sourcePath, repairItemCode, System.StringComparison.OrdinalIgnoreCase) ||
                          sourceCode.EndsWith(":" + repairItemCode, System.StringComparison.OrdinalIgnoreCase) ||
                          sourceCode.EndsWith(repairItemCode, System.StringComparison.OrdinalIgnoreCase);
            
            if (!matches)
            {
                //API?.Logger.Debug($"[vsquest] Source item doesn't match repair code: {sourceCode} != {repairItemCode}");
                return false;
            }

            // Check if item needs repair (condition < 1.0)
            // If condition attribute doesn't exist, assume it needs repair
            float condition = sinkStack.Attributes.GetFloat("condition", 0f);
            if (condition >= 1f)
            {
                API?.Logger.Debug($"[vsquest] Item already at max condition: {condition}");
                return false;
            }
            
            return true;
        }

        private static bool CanRepairWithCustomItem(ItemStack sinkStack, ItemStack sourceStack)
        {
            return CanRepairWithCustomItem(sinkStack, sourceStack, out _);
        }

        private static bool HasCustomRepairItem(ItemStack sinkStack)
        {
            if (sinkStack?.ItemAttributes == null) return false;
            
            try
            {
                var repairItemAttr = sinkStack.ItemAttributes["repairItemCode"];
                return repairItemAttr != null && repairItemAttr.Exists;
            }
            catch
            {
                return false;
            }
        }
    }
}
