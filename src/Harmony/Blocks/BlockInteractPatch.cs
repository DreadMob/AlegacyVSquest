using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using VsQuest.Systems.Client;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class BlockInteractPatch
    {
        // Debounce for different blocks (prevents spam when clicking around)
        private const int DebounceDifferentBlockMs = 500;
        // Debounce for same block (low enough for hold mechanic to work smoothly)
        private const int DebounceSameBlockMs = 150;

        private static long lastSendMs;
        private static int lastX = int.MinValue;
        private static int lastY = int.MinValue;
        private static int lastZ = int.MinValue;
        private static int lastDim = int.MinValue;
        private static string lastBlockCode;

        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (!HarmonyPatchSwitches.BlockInteractEnabled(HarmonyPatchSwitches.BlockInteract_Block_OnBlockInteractStart)) return;
            if (world.Api.Side != EnumAppSide.Client || blockSel == null) return;

            // Skip air blocks - no need to track interactions with empty blocks
            string blockCode = __instance?.Code?.ToString();
            if (string.IsNullOrWhiteSpace(blockCode) || blockCode == "air" || blockCode.EndsWith(":air"))
            {
                return;
            }

            if (__result)
            {
                return;
            }

            // If the player is holding a block, this is very likely a placement attempt.
            // We already track placement on the server via DidPlaceBlock, and sending an extra
            // "vanilla interact" packet for every placement attempt can create server-side load
            // spikes when quests are active (leading to rubberband).
            try
            {
                var held = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
                if (held?.Collectible is Block)
                {
                    return;
                }
            }
            catch
            {
                // ignore and fall through to keep behavior stable if API changes
            }

            var capi = world.Api as ICoreClientAPI;
            if (capi == null) return;

            // Skip if player has no active quests with interact objectives
            // This avoids unnecessary network traffic for players without relevant quests
            try
            {
                var questSystem = capi.ModLoader.GetModSystem<QuestSystem>();
                if (!ClientQuestState.HasInteractObjectives(questSystem))
                {
                    return;
                }
            }
            catch
            {
                // If we can't check, fall through to default behavior
            }

            try
            {
                int x = blockSel.Position.X;
                int y = blockSel.Position.Y;
                int z = blockSel.Position.Z;
                int dim = byPlayer?.Entity?.Pos?.Dimension ?? 0;
                string code = __instance?.Code?.ToString();

                long now = Environment.TickCount64;
                bool sameBlock = (lastX == x && lastY == y && lastZ == z
                    && lastDim == dim
                    && string.Equals(lastBlockCode, code, StringComparison.Ordinal));

                // Use shorter debounce for same block (hold mechanic needs frequent updates)
                // Use longer debounce for different blocks (prevents spam)
                int debounceMs = sameBlock ? DebounceSameBlockMs : DebounceDifferentBlockMs;

                if ((now - lastSendMs) < debounceMs && sameBlock)
                {
                    return;
                }

                lastSendMs = now;
                lastX = x;
                lastY = y;
                lastZ = z;
                lastDim = dim;
                lastBlockCode = code;
            }
            catch
            {
            }

            capi.Network.GetChannel("alegacyvsquest").SendPacket(new VanillaBlockInteractMessage()
            {
                Position = blockSel.Position,
                BlockCode = __instance.Code.ToString()
            });
        }
    }
}
