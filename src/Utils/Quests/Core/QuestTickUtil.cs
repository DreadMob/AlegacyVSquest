using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestTickUtil
    {
        private static readonly Dictionary<string, double> lastMissingQuestLogHoursByKey = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> questHasTickObjectivesByQuestId = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

        private static double lastPassiveCheckHours = -1.0;

        /// <summary>
        /// Очищает кэши при отключении игрока или перезагрузке квестов
        /// </summary>
        public static void ClearPlayerCache(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;

            // Remove entries for this player from lastMissingQuestLogHoursByKey
            var keysToRemove = new List<string>();
            foreach (var key in lastMissingQuestLogHoursByKey.Keys)
            {
                if (key != null && key.StartsWith(playerUid + ":"))
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                lastMissingQuestLogHoursByKey.Remove(key);
            }
        }

        /// <summary>
        /// Полная очистка всех кэшей (например, при перезагрузке квестов)
        /// </summary>
        public static void ClearAllCaches()
        {
            lastMissingQuestLogHoursByKey.Clear();
            questHasTickObjectivesByQuestId.Clear();
        }

        public static void HandleQuestTick(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, IPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, System.Action<string> markPlayerDirty, ICoreServerAPI sapi, double missingQuestLogThrottleHours = (1.0 / 60.0), double passiveCompletionThrottleHours = (1.0 / 60.0))
        {
            var sw = QuestProfiler.StartMeasurement("QuestTickUtil.HandleQuestTick");
            try
            {
                HandleQuestTickInternal(dt, questRegistry, actionObjectiveRegistry, players, getPlayerQuests, markPlayerDirty, sapi, missingQuestLogThrottleHours, passiveCompletionThrottleHours);
            }
            finally
            {
                QuestProfiler.EndMeasurement("QuestTickUtil.HandleQuestTick", sw);
                QuestProfiler.LogStats();
            }
        }

        private static void HandleQuestTickInternal(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, IPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, System.Action<string> markPlayerDirty, ICoreServerAPI sapi, double missingQuestLogThrottleHours, double passiveCompletionThrottleHours)
        {
            if (players == null || players.Length == 0) return;

			double missingLogThrottle = missingQuestLogThrottleHours;
			if (missingLogThrottle <= 0) missingLogThrottle = 1.0 / 60.0;

			double passiveThrottle = passiveCompletionThrottleHours;
			if (passiveThrottle <= 0) passiveThrottle = 1.0 / 60.0;

            double nowHours = sapi?.World?.Calendar?.TotalHours ?? 0;
            bool shouldCheckPassive = lastPassiveCheckHours < 0 || (nowHours - lastPassiveCheckHours) >= (1.0 / 60.0);
            if (shouldCheckPassive) lastPassiveCheckHours = nowHours;

            for (int p = 0; p < players.Length; p++)
            {
                if (players[p] is not IServerPlayer serverPlayer) continue;

                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                if (activeQuests == null || activeQuests.Count == 0) continue;

                bool playerDirty = false;
                var registry = questRegistry; // Локальная переменная для чуть более быстрого доступа

                for (int aq = 0; aq < activeQuests.Count; aq++)
                {
                    var activeQuest = activeQuests[aq];
                    if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                    if (!registry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null)
                    {
                        // Throttle log to avoid spamming every tick for every player.
                        // Keyed by player uid + quest id.
                        double logNowHours = sapi?.World?.Calendar?.TotalHours ?? 0;
                        string logKey = serverPlayer.PlayerUID + ":" + activeQuest.questId;
                        if (!lastMissingQuestLogHoursByKey.TryGetValue(logKey, out var lastHours) || (logNowHours - lastHours) > missingLogThrottle)
                        {
                            lastMissingQuestLogHoursByKey[logKey] = logNowHours;
                            sapi.Logger.Error($"[alegacyvsquest] Active quest with id '{activeQuest.questId}' for player '{serverPlayer.PlayerUID}' not found in QuestRegistry. Skipping tick update. This might happen if a quest was removed but player data was not updated.");
                        }

                        // Auto-heal: remove missing quest from active quests so we don't spam logs forever.
                        // Safe iteration: adjust index after removal.
                        activeQuests.RemoveAt(aq);
                        aq--;
                        playerDirty = true;

                        continue;
                    }

                    bool hasTickObjectives;
                    if (!questHasTickObjectivesByQuestId.TryGetValue(activeQuest.questId, out hasTickObjectives))
                    {
                        hasTickObjectives = false;
                        if (questDef.actionObjectives != null)
                        {
                            for (int i = 0; i < questDef.actionObjectives.Count; i++)
                            {
                                var objective = questDef.actionObjectives[i];
                                if (objective == null) continue;
                                if (objective.id == "walkdistance" || objective.id == "temporalstorm" || objective.id == "timer")
                                {
                                    hasTickObjectives = true;
                                    break;
                                }
                            }
                        }

                        questHasTickObjectivesByQuestId[activeQuest.questId] = hasTickObjectives;
                    }

                    if (hasTickObjectives)
                    {
                        // Use stage-aware action objectives
                        var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
                        if (stageActionObjectives != null)
                        {
                            for (int i = 0; i < stageActionObjectives.Count; i++)
                            {
                                var objective = stageActionObjectives[i];
                                if (objective == null) continue;

                                if (objective.id == "walkdistance")
                                {
                                    if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, actionObjectiveRegistry, activeQuest.currentStageIndex, "tick", objective.objectiveId)) continue;

                                    if (!actionObjectiveRegistry.TryGetValue(objective.id, out var impl) || impl == null) continue;
                                    var objectiveImplementation = impl as WalkDistanceObjective;
                                    objectiveImplementation?.OnTick(serverPlayer, activeQuest, i, objective.args, sapi, dt);
                                }
                                else if (objective.id == "temporalstorm")
                                {
                                    if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, actionObjectiveRegistry, activeQuest.currentStageIndex, "tick", objective.objectiveId)) continue;

                                    if (!actionObjectiveRegistry.TryGetValue(objective.id, out var impl) || impl == null) continue;
                                    var objectiveImplementation = impl as TemporalStormObjective;
                                    objectiveImplementation?.OnTick(serverPlayer, activeQuest, objective, sapi);
                                }
                            }
                        }
                    }

                    if (shouldCheckPassive) TryFirePassiveActionObjectiveCompletions(serverPlayer, activeQuest, questDef, actionObjectiveRegistry, sapi, passiveThrottle);
                }

                // Mark dirty once per player per tick if needed
                if (playerDirty)
                {
                    markPlayerDirty?.Invoke(serverPlayer.PlayerUID);
                }
            }
        }

        private static void TryFirePassiveActionObjectiveCompletions(IServerPlayer player, ActiveQuest activeQuest, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, ICoreServerAPI sapi, double passiveCompletionThrottleHours)
        {
            if (player == null || activeQuest == null || questDef == null) return;
            if (sapi == null || actionObjectiveRegistry == null) return;
            
            sapi.Logger.Debug($"[QuestTickUtil] TryFirePassiveActionObjectiveCompletions for quest {activeQuest.questId} stage {activeQuest.currentStageIndex}");

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string throttleKey = $"alegacyvsquest:ao:lastcheck:{activeQuest.questId}";
            double now = sapi.World.Calendar.TotalHours;
            double last = wa.GetDouble(throttleKey, -999999);
			double throttle = passiveCompletionThrottleHours;
			if (throttle <= 0) throttle = 1.0 / 3600.0;
			// Reduce throttle for inland objectives to check more frequently
			throttle = 1.0 / 3600.0; // Check every second
			if (now - last < throttle) return;
            wa.SetDouble(throttleKey, now);
            // Only mark dirty if value actually changed to avoid unnecessary network sync
            if (Math.Abs(now - last) > 0.001)
            {
                wa.MarkPathDirty(throttleKey);
            }

            // Use stage-aware action objectives
            var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
            if (stageActionObjectives == null) return;

            for (int i = 0; i < stageActionObjectives.Count; i++)
            {
                var ao = stageActionObjectives[i];
                if (ao == null) continue;
                
                if (string.IsNullOrWhiteSpace(ao.onCompleteActions)) continue;

                // Skip event-driven objectives; they fire completion in their own hooks.
                if (ao.id == "walkdistance") continue;
                if (ao.id == "temporalstorm") continue;
                if (ao.id == "killnear") continue;
                if (ao.id == "randomkill") continue;
                if (ao.id == "interactwithentity") continue;
                if (ao.id == "interactat") continue;
                if (ao.id == "interactcount") continue;
                // [EXPERIMENTAL] Skip event-driven experimental objectives
                if (ao.id == "killwithweapon") continue;
                if (ao.id == "mineblock") continue;
                if (ao.id == "placeblock") continue;
                if (ao.id == "harvestcrop") continue;
                if (ao.id == "fishcatch") continue;
                if (ao.id == "craftitem") continue;
                if (ao.id == "smeltitem") continue;

                sapi.Logger.Debug($"[QuestTickUtil] Processing action objective: {ao.id}");
                if (!actionObjectiveRegistry.TryGetValue(ao.id, out var impl) || impl == null) 
                {
                    sapi.Logger.Debug($"[QuestTickUtil] No implementation found for {ao.id}");
                    continue;
                }

                bool ok;
                try
                {
                    ok = impl.IsCompletable(player, ao.args);
                    sapi.Logger.Debug($"[QuestTickUtil] {ao.id} IsCompletable: {ok}");
                }
                catch (Exception ex)
                {
                    // IsCompletable failed for this objective, skip it
                    sapi.Logger.Debug($"[QuestTickUtil] {ao.id} IsCompletable failed: {ex.Message}");
                    continue;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, ao, ao.objectiveId, ok);
            }
        }
    }
}
