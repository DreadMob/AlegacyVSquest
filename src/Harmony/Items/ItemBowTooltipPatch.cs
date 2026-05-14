using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    /// <summary>
    /// Patches ItemBow.GetHeldItemInfo to remove vanilla damage/accuracy lines
    /// when the bow is an action item with hideVanillaTooltips containing "attackpower".
    /// Our Postfix on CollectibleObject doesn't catch ItemBow's override additions.
    /// </summary>
    [HarmonyPatch(typeof(ItemBow), "GetHeldItemInfo")]
    public class ItemBow_GetHeldItemInfo_Tooltip_Patch
    {
        public static void Postfix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot?.Itemstack == null) return;
            if (inSlot.Itemstack.Attributes == null) return;

            string actionsJson = inSlot.Itemstack.Attributes.GetString("alegacyvsquest:actions");
            if (string.IsNullOrEmpty(actionsJson)) return;

            string hideVanillaJson = inSlot.Itemstack.Attributes.GetString("alegacyvsquest:hideVanilla");
            if (string.IsNullOrEmpty(hideVanillaJson) || !hideVanillaJson.Contains("attackpower")) return;

            // Remove lines containing damage/accuracy info added by ItemBow
            string text = dsc.ToString();
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            dsc.Clear();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                bool shouldRemove = false;

                if (trimmed.Contains("\u0443\u0440\u043E\u043D\u0430") && !trimmed.Contains("<font")) shouldRemove = true; // "урона" but not our formatted lines
                else if (trimmed.Contains("\u0442\u043E\u0447\u043D\u043E\u0441\u0442\u0438") && !trimmed.Contains("<font")) shouldRemove = true; // "точности"
                else if (trimmed.Contains("damage") && !trimmed.Contains("<font")) shouldRemove = true;
                else if (trimmed.Contains("accuracy") && !trimmed.Contains("<font")) shouldRemove = true;

                if (!shouldRemove)
                {
                    dsc.AppendLine(line);
                }
            }

            // Trim trailing newlines
            while (dsc.Length > 0 && (dsc[dsc.Length - 1] == '\n' || dsc[dsc.Length - 1] == '\r'))
            {
                dsc.Length--;
            }
        }
    }
}
