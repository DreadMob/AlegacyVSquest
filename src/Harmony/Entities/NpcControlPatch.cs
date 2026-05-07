using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class NpcControlPatch
    {
        private static bool patched;

        public static void TryPatch(HarmonyLib.Harmony harmony)
        {
            if (patched) return;
            if (harmony == null) return;

            var npcControlType = AccessTools.TypeByName("Vintagestory.ServerMods.NpcControl");
            if (npcControlType == null) return;

            var targetMethod = AccessTools.Method(npcControlType, "Event_OnPlayerInteractEntity");
            if (targetMethod == null) return;

            var prefix = new HarmonyMethod(typeof(NpcControlPatch).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.Public));
            harmony.Patch(targetMethod, prefix: prefix);
            patched = true;
        }

        public static bool Prefix(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
        {
            // Suppress "Ok, npc selected" message for PlayerBot entities (like Loran NPC)
            // Only allow the message for actual AnimalBot entities
            if (entity is EntityPlayerBot)
            {
                handling = EnumHandling.Handled;
                return false; // Skip original method
            }
            return true; // Allow original method for other cases
        }
    }
}
