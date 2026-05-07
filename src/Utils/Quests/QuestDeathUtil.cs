using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestDeathUtil
    {
        public static void HandleEntityDeath(ICoreServerAPI sapi, List<ActiveQuest> quests, EntityPlayer player, Entity killedEntity)
        {
            sapi.Logger.Debug($"[QuestDeathUtil] HandleEntityDeath called: killed={killedEntity?.Code?.Path}, player={player?.Player?.PlayerName}, quests={quests?.Count}");
            if (sapi == null || player == null || quests == null) return;

            var rebirth2 = killedEntity?.GetBehavior<EntityBehaviorBossRebirth2>();
            if (rebirth2 != null && !rebirth2.IsFinalStage) return;

            string killedCode = killedEntity?.Code?.Path;
            var serverPlayer = player.Player as IServerPlayer;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            foreach (var quest in quests)
            {
                sapi.Logger.Debug($"[QuestDeathUtil] Processing quest {quest.questId} for kill {killedCode}");
                quest.EnsureInitialized(player.Player);
                quest.GetOrCreateTracker().OnEntityKilled(killedCode, player.Player, null);

                if (serverPlayer != null)
                {
                    Quest questDef = null;
                    QuestRegistryService.QuestRegistry.TryGetValue(quest.questId, out questDef);

                    // killactiontarget objectives
                    KillActionObjectiveUtil.TryHandleKill(sapi, serverPlayer, quest, killedEntity);

                    // Get action objectives from current stage using centralized method
                    var actionObjectives = questDef?.GetActionObjectives(quest.currentStageIndex);

                    // killnear objectives
                    ProcessKillNearObjectives(sapi, serverPlayer, quest, questDef, killedEntity, killedCode, actionObjectives, questSystem);

                    string killObjectiveId = FindRandomKillObjectiveId(actionObjectives);
                    sapi.Logger.Debug($"[QuestDeathUtil] Found randomkill objective: {killObjectiveId}");

                    if (QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, QuestRegistryService.ActionObjectiveRegistry, quest.currentStageIndex, "kill", killObjectiveId))
                    {
                        sapi.Logger.Debug($"[QuestDeathUtil] TimeGate allows progress, calling TryHandleKill");
                        RandomKillQuestUtils.TryHandleKill(sapi, serverPlayer, quest, killedCode);
                    }
                    else
                    {
                        sapi.Logger.Debug($"[QuestDeathUtil] TimeGate blocks progress for quest {quest.questId}");
                    }
                }
            }
        }

        private static string FindRandomKillObjectiveId(List<ActionWithArgs> actionObjectives)
        {
            if (actionObjectives == null) return null;

            foreach (var ao in actionObjectives)
            {
                if (ao?.id == "randomkill" && !string.IsNullOrWhiteSpace(ao.objectiveId))
                    return ao.objectiveId;
            }

            return null;
        }

        private static void ProcessKillNearObjectives(
            ICoreServerAPI sapi,
            IServerPlayer serverPlayer,
            ActiveQuest quest,
            Quest questDef,
            Entity killedEntity,
            string killedCode,
            List<ActionWithArgs> actionObjectives,
            QuestSystem questSystem)
        {
            if (actionObjectives == null) return;

            foreach (var ao in actionObjectives)
            {
                if (!IsValidKillNearObjective(ao, quest.questId, quest.currentStageIndex, killedCode, killedEntity, questDef, serverPlayer, questSystem, out var x, out var y, out var z, out var radius, out var need)) continue;

                UpdateKillNearProgress(sapi, serverPlayer, quest, ao, need);
            }
        }

        private static bool IsValidKillNearObjective(
            ActionWithArgs ao,
            string questId,
            int stageIndex,
            string killedCode,
            Entity killedEntity,
            Quest questDef,
            IServerPlayer serverPlayer,
            QuestSystem questSystem,
            out int x, out int y, out int z, out double radius, out int need)
        {
            x = y = z = 0;
            radius = 0;
            need = 0;

            if (ao?.id != "killnear") return false;
            if (ao.args == null || ao.args.Length < 6) return false;
            if (string.IsNullOrWhiteSpace(ao.objectiveId)) return false;

            if (!KillNearObjective.TryParseArgs(ao.args, out string questIdArg, out _, out x, out y, out z, out radius, out string mobCode, out need)) return false;
            if (!string.Equals(questIdArg, questId, System.StringComparison.OrdinalIgnoreCase)) return false;
            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, questSystem?.ActionObjectiveRegistry, stageIndex, "kill", ao.objectiveId)) return false;
            if (!string.IsNullOrWhiteSpace(mobCode) && mobCode != "*" && !LocalizationUtils.MobCodeMatches(mobCode, killedCode)) return false;

            var pos = killedEntity?.Pos;
            if (pos == null) return false;

            double dx = pos.X - x;
            double dy = pos.Y - y;
            double dz = pos.Z - z;
            if ((dx * dx + dy * dy + dz * dz) > radius * radius) return false;

            return true;
        }

        private static void UpdateKillNearProgress(ICoreServerAPI sapi, IServerPlayer serverPlayer, ActiveQuest quest, ActionWithArgs ao, int need)
        {
            var wa = serverPlayer.Entity?.WatchedAttributes;
            if (wa == null) return;

            string haveKey = KillNearObjective.HaveKey(quest.questId, ao.objectiveId);
            int have = wa.GetInt(haveKey, 0);
            if (have >= need) return;

            have++;
            wa.SetInt(haveKey, have);
            wa.MarkPathDirty(haveKey);

            if (have >= need)
            {
                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, quest, ao, ao.objectiveId, true);
            }
        }
    }
}
