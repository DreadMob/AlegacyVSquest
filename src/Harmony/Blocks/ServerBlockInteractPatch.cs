using HarmonyLib;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class ServerBlockInteractPatch
    {
        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (!HarmonyPatchSwitches.ServerBlockInteractEnabled(HarmonyPatchSwitches.ServerBlockInteract_Block_OnBlockInteractStart)) return;
            if (world.Api.Side != EnumAppSide.Server || !__result || blockSel == null) return;

            var sapi = world.Api as ICoreServerAPI;
            var player = byPlayer as IServerPlayer;
            if (sapi == null || player == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            int[] position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                // Only forward block-use events to quests that have interact-related objectives.
                // This avoids per-interact overhead for unrelated quests.
                try
                {
                    if (questSystem.QuestRegistry == null) continue;
                    if (!questSystem.QuestRegistry.TryGetValue(quest.questId, out var questDef) || questDef == null) continue;

                    // Check current stage for interact objectives
                    var currentStage = questDef.GetStage(quest.currentStageIndex);
                    bool needsBlockUse = (currentStage?.interactObjectives != null && currentStage.interactObjectives.Count > 0);
                    
                    if (!needsBlockUse && currentStage?.actionObjectives != null)
                    {
                        for (int ao = 0; ao < currentStage.actionObjectives.Count; ao++)
                        {
                            var a = currentStage.actionObjectives[ao];
                            if (a == null) continue;
                            if (a.id == "interactat" || a.id == "interactcount")
                            {
                                needsBlockUse = true;
                                break;
                            }
                        }
                    }

                    if (!needsBlockUse) continue;
                }
                catch
                {
                }

                quest.OnBlockUsed(__instance.Code.ToString(), position, player, sapi, null);
            }
        }
    }
}
