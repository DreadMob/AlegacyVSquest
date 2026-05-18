using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Dispatches game events to experimental objectives.
    /// Called from QuestEventHandler and QuestDeathUtil.
    /// </summary>
    public static class ExperimentalObjectiveDispatcher
    {
        /// <summary>
        /// Handle block broken event for mineblock and harvestcrop objectives.
        /// </summary>
        public static void OnBlockBroken(
            ICoreServerAPI sapi,
            IServerPlayer player,
            string blockCode,
            List<ActiveQuest> quests)
        {
            if (sapi == null || player == null || quests == null) return;
            if (string.IsNullOrWhiteSpace(blockCode)) return;

            var registry = QuestRegistryService.ActionObjectiveRegistry;
            if (registry == null) return;

            registry.TryGetValue("mineblock", out var mineImpl);
            registry.TryGetValue("harvestcrop", out var harvestImpl);
            var mineObj = mineImpl as MineBlockObjective;
            var harvestObj = harvestImpl as HarvestCropObjective;

            if (mineObj == null && harvestObj == null) return;

            for (int i = 0; i < quests.Count; i++)
            {
                var aq = quests[i];
                if (aq == null || string.IsNullOrWhiteSpace(aq.questId)) continue;

                if (!QuestRegistryService.QuestRegistry.TryGetValue(aq.questId, out var questDef))
                    continue;

                var objectives = questDef?.GetActionObjectives(aq.currentStageIndex);
                if (objectives == null) continue;

                for (int j = 0; j < objectives.Count; j++)
                {
                    var ao = objectives[j];
                    if (ao == null) continue;

                    if (ao.id == "mineblock" && mineObj != null)
                    {
                        if (!MineBlockObjective.TryParseArgs(ao.args, out string qId, out string objId, out string code, out int need))
                            continue;
                        if (!string.Equals(qId, aq.questId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        mineObj.TryIncrement(player, blockCode, qId, objId, code, need);

                        if (mineObj.IsCompletable(player, ao.args))
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, aq, ao, objId, true);
                    }
                    else if (ao.id == "harvestcrop" && harvestObj != null)
                    {
                        if (!HarvestCropObjective.TryParseArgs(ao.args, out string qId, out string objId, out string code, out int need))
                            continue;
                        if (!string.Equals(qId, aq.questId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        harvestObj.TryIncrement(player, blockCode, qId, objId, code, need);

                        if (harvestObj.IsCompletable(player, ao.args))
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, aq, ao, objId, true);
                    }
                }
            }
        }

        /// <summary>
        /// Handle block placed event for placeblock objectives.
        /// </summary>
        public static void OnBlockPlaced(
            ICoreServerAPI sapi,
            IServerPlayer player,
            string blockCode,
            List<ActiveQuest> quests)
        {
            if (sapi == null || player == null || quests == null) return;
            if (string.IsNullOrWhiteSpace(blockCode)) return;

            var registry = QuestRegistryService.ActionObjectiveRegistry;
            if (registry == null) return;

            registry.TryGetValue("placeblock", out var placeImpl);
            var placeObj = placeImpl as PlaceBlockObjective;
            if (placeObj == null) return;

            for (int i = 0; i < quests.Count; i++)
            {
                var aq = quests[i];
                if (aq == null || string.IsNullOrWhiteSpace(aq.questId)) continue;

                if (!QuestRegistryService.QuestRegistry.TryGetValue(aq.questId, out var questDef))
                    continue;

                var objectives = questDef?.GetActionObjectives(aq.currentStageIndex);
                if (objectives == null) continue;

                for (int j = 0; j < objectives.Count; j++)
                {
                    var ao = objectives[j];
                    if (ao == null || ao.id != "placeblock") continue;

                    if (!PlaceBlockObjective.TryParseArgs(ao.args, out string qId, out string objId, out string code, out int need))
                        continue;
                    if (!string.Equals(qId, aq.questId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    placeObj.TryIncrement(player, blockCode, qId, objId, code, need);

                    if (placeObj.IsCompletable(player, ao.args))
                        QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, aq, ao, objId, true);
                }
            }
        }

        /// <summary>
        /// Handle entity kill for killwithweapon objectives.
        /// </summary>
        public static void OnEntityKilled(
            ICoreServerAPI sapi,
            IServerPlayer player,
            List<ActiveQuest> quests)
        {
            if (sapi == null || player == null || quests == null) return;

            var registry = QuestRegistryService.ActionObjectiveRegistry;
            if (registry == null) return;

            registry.TryGetValue("killwithweapon", out var killImpl);
            var killObj = killImpl as KillWithWeaponObjective;
            if (killObj == null) return;

            // Get the weapon the player is currently holding
            var activeSlot = player.InventoryManager?.ActiveHotbarSlot;
            if (activeSlot == null || activeSlot.Empty) return;

            string heldCode = activeSlot.Itemstack?.Collectible?.Code?.Path;
            if (string.IsNullOrWhiteSpace(heldCode)) return;

            for (int i = 0; i < quests.Count; i++)
            {
                var aq = quests[i];
                if (aq == null || string.IsNullOrWhiteSpace(aq.questId)) continue;

                if (!QuestRegistryService.QuestRegistry.TryGetValue(aq.questId, out var questDef))
                    continue;

                var objectives = questDef?.GetActionObjectives(aq.currentStageIndex);
                if (objectives == null) continue;

                for (int j = 0; j < objectives.Count; j++)
                {
                    var ao = objectives[j];
                    if (ao == null || ao.id != "killwithweapon") continue;

                    if (!KillWithWeaponObjective.TryParseArgs(ao.args, out string qId, out string objId, out string weaponCode, out int need))
                        continue;
                    if (!string.Equals(qId, aq.questId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    killObj.TryIncrement(player, heldCode, qId, objId, weaponCode, need);

                    if (killObj.IsCompletable(player, ao.args))
                        QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, aq, ao, objId, true);
                }
            }
        }
    }
}
