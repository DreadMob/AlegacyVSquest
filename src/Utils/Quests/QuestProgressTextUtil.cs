using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class QuestProgressTextUtil
    {
        /// <summary>
        /// Strips HTML tags from a string.
        /// </summary>
        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            return Regex.Replace(input, "<[^>]+>", string.Empty);
        }
        /// <summary>
        /// Gets the display name for an item code, trying multiple localization patterns.
        /// Also checks action items from the action item registry.
        /// </summary>
        private static string GetItemDisplayName(ICoreAPI api, string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return itemCode;

            // Try to get the item from the item registry
            var item = api.World.GetItem(new AssetLocation(itemCode));
            if (item != null)
            {
                return item.GetHeldItemName(new ItemStack(item));
            }

            // Try block if not an item
            var block = api.World.GetBlock(new AssetLocation(itemCode));
            if (block != null)
            {
                return block.GetHeldItemName(new ItemStack(block));
            }

            // Try action items from the action item registry
            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
            if (itemSystem?.ActionItemRegistry != null && itemSystem.ActionItemRegistry.TryGetValue(itemCode, out var actionItem) && actionItem != null)
            {
                // Strip HTML tags from action item name to avoid color tag conflicts
                string name = actionItem.name ?? itemCode;
                return StripHtmlTags(name);
            }

            // Fallback: try localization keys directly
            string normalized = itemCode.Replace(":", "-");
            string langKey = $"item-{normalized}";
            string result = LocalizationUtils.GetSafe(langKey);
            if (!string.IsNullOrWhiteSpace(result) && !string.Equals(result, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            // Try with game: prefix
            langKey = $"game:item-{normalized}";
            result = LocalizationUtils.GetSafe(langKey);
            if (!string.IsNullOrWhiteSpace(result) && !string.Equals(result, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            // Final fallback: return the code itself
            return itemCode;
        }
        public static string GetActiveQuestText(ICoreAPI api, IPlayer player, ActiveQuest quest)
        {
            var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null || !questSystem.QuestRegistry.TryGetValue(quest.questId, out var questDef))
            {
                return LocalizationUtils.GetSafe(quest.questId + "-desc");
            }

            string progressText = BuildProgressText(api, player, quest, questDef);

            string desc = LocalizationUtils.GetSafe(quest.questId + "-desc");

            // Add stage information for multi-stage quests
            string stageInfo = BuildStageInfo(quest, questDef);
            if (!string.IsNullOrEmpty(stageInfo))
            {
                desc = $"{desc}<br><br><strong>{stageInfo}</strong>";
            }

            if (string.IsNullOrEmpty(progressText))
            {
                return desc;
            }
            else
            {
                return $"{desc}<br><br><strong>{LocalizationUtils.GetSafe("alegacyvsquest:progress-title")}</strong><br>{progressText}";
            }
        }

        /// <summary>
        /// Builds stage header information for multi-stage quests
        /// </summary>
        private static string BuildStageInfo(ActiveQuest activeQuest, Quest questDef)
        {
            if (!questDef.HasStages) return null;

            var stage = questDef.GetStage(activeQuest.currentStageIndex);
            if (stage == null) return null;

            string stageTitle = !string.IsNullOrEmpty(stage.stageTitleLangKey)
                ? LocalizationUtils.GetSafe(stage.stageTitleLangKey)
                : $"Stage {activeQuest.currentStageIndex + 1}/{questDef.StageCount}";

            return $"► {stageTitle}";
        }

        private static string BuildProgressText(ICoreAPI api, IPlayer player, ActiveQuest activeQuest, Quest questDef)
        {
            var lines = new List<string>();
            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return "";

            try
            {
                string ApplyPrefixes(string text, string objectiveId, List<ActionWithArgs> actionObjectivesToCheck)
                {
                    if (string.IsNullOrWhiteSpace(text)) return text;

                    string timeOfDayPrefix = null;
                    string landPrefix = null;

                    if (actionObjectivesToCheck != null)
                    {
                        // timeofday prefix: only apply if there is a gate targeting this objectiveId,
                        // or if the gate has no objectiveId (legacy behavior).
                        foreach (var ao in actionObjectivesToCheck)
                        {
                            if (ao?.id != "timeofday") continue;
                            if (ao.args == null || ao.args.Length == 0) continue;

                            bool applies = false;
                            if (ao.args.Length == 1)
                            {
                                // Legacy: show prefix globally
                                applies = true;
                            }
                            else if (ao.args.Length == 2)
                            {
                                applies = !string.IsNullOrWhiteSpace(objectiveId)
                                    && string.Equals(ao.args[1], objectiveId, StringComparison.OrdinalIgnoreCase);
                            }

                            if (!applies) continue;

                            if (TimeOfDayObjective.TryGetModeLabelKey(ao.args, out string labelKey))
                            {
                                timeOfDayPrefix = LocalizationUtils.GetSafe(labelKey);
                            }
                            break;
                        }

                        // landgate prefix
                        foreach (var ao in actionObjectivesToCheck)
                        {
                            if (ao?.id != "landgate") continue;

                            if (!LandGateObjective.TryParseArgs(ao.args, out _, out string gateObjectiveId, out string prefix, out bool hidePrefix))
                            {
                                continue;
                            }

                            bool applies = string.IsNullOrWhiteSpace(gateObjectiveId)
                                || (!string.IsNullOrWhiteSpace(objectiveId) && string.Equals(gateObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));

                            if (!applies) continue;

                            if (!hidePrefix)
                            {
                                landPrefix = prefix;
                            }
                            break;
                        }
                    }

                    // Order matters: landgate wraps timeofday so that final text becomes "land: time: ..."
                    string[] prefixes = new[] { timeOfDayPrefix, landPrefix };
                    foreach (var p in prefixes)
                    {
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        text = $"{p}: {text}";
                    }

                    return text;
                }

                // randomkill objectives
                int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(activeQuest.questId), 0);
                // Find randomkill objectiveId from quest definition (for timeofday prefix application)
                string randomKillObjectiveId = null;
                
                // Use stage-aware action objectives
                var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
                
                if (stageActionObjectives != null)
                {
                    foreach (var ao in stageActionObjectives)
                    {
                        if (ao?.id == "randomkill")
                        {
                            randomKillObjectiveId = ao.objectiveId;
                            break;
                        }
                    }
                }
                if (slots > 0)
                {
                    for (int slot = 0; slot < slots; slot++)
                    {
                        string code = wa.GetString(RandomKillQuestUtils.SlotCodeKey(activeQuest.questId, slot), "?");
                        int have = wa.GetInt(RandomKillQuestUtils.SlotHaveKey(activeQuest.questId, slot), 0);
                        int need = wa.GetInt(RandomKillQuestUtils.SlotNeedKey(activeQuest.questId, slot), 0);
                        lines.Add($"- {ApplyPrefixes($"{LocalizationUtils.GetMobDisplayName(code)}: {have}/{need}", randomKillObjectiveId, stageActionObjectives)}");
                    }
                }

                // Generic objectives from GetProgress
                var progress = activeQuest.GetProgress(player);
                int progressIndex = 0;

                // Quest-specific progress line (e.g. witness: "Gifts found: {8}/{9}")
                // The template may reference any index in the full progress array.
                try
                {
                    var customKey = activeQuest.questId + "-obj";
                    var custom = LocalizationUtils.GetSafe(customKey, progress.Cast<object>().ToArray());
                    if (!string.IsNullOrWhiteSpace(custom) && !string.Equals(custom, customKey, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add($"- {ApplyPrefixes(custom, null, stageActionObjectives)}");
                        AppendLandClaimInfo(lines, questDef, wa);
                        return string.Join("\n", lines);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        // Fallback for quests that use interactcount: compute have/need directly instead of relying on hardcoded indices.
                        if (stageActionObjectives != null)
                        {
                            foreach (var ao in stageActionObjectives)
                            {
                                if (ao?.id != "interactcount") continue;

                                var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
                                if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactcount", out var impl) && impl != null)
                                {
                                    var prog = impl.GetProgress(player, ao.args);
                                    if (prog != null && prog.Count >= 2)
                                    {
                                        int have = prog[0];
                                        int need = prog[1];

                                        var labelKey = activeQuest.questId + "-obj";
                                        var template = LocalizationUtils.GetSafe(labelKey, have, need);
                                        if (string.IsNullOrWhiteSpace(template) || string.Equals(template, labelKey, StringComparison.OrdinalIgnoreCase))
                                        {
                                            template = $"{have}/{need}";
                                        }

                                        lines.Add($"- {ApplyPrefixes(template, null, stageActionObjectives)}");
                                        AppendLandClaimInfo(lines, questDef, wa);
                                        return string.Join("\n", lines);
                                    }
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Both custom progress and interactcount fallback failed - will use default progress display
                    }
                }

                void AddProgressLines(List<Objective> objectives, bool isGather = false)
                {
                    foreach (var objective in objectives)
                    {
                        if (progressIndex < progress.Count)
                        {
                            int have = progress[progressIndex++];
                            int need = objective.demand;
                            string code = objective.validCodes.FirstOrDefault() ?? "?";
                            string displayName = isGather
                                ? GetItemDisplayName(api, code)
                                : MobLocalizationUtils.GetMobDisplayName(code);
                            lines.Add($"- {ApplyPrefixes($"{displayName}: {have}/{need}", null, stageActionObjectives)}");
                        }
                    }
                }

                // Use current stage objectives for multi-stage quests
                var currentStage = questDef.GetStage(activeQuest.currentStageIndex);
                if (currentStage != null)
                {
                    AddProgressLines(currentStage.gatherObjectives, isGather: true);
                    AddProgressLines(currentStage.killObjectives, isGather: false);
                    AddProgressLines(currentStage.blockPlaceObjectives, isGather: true);
                    AddProgressLines(currentStage.blockBreakObjectives, isGather: true);
                    AddProgressLines(currentStage.interactObjectives, isGather: false);
                }

                // Action objectives - use current stage's action objectives for multi-stage quests
                var actionObjectivesToProcess = questDef.HasStages
                    ? (currentStage?.actionObjectives ?? new List<ActionWithArgs>())
                    : questDef.actionObjectives;

                if (actionObjectivesToProcess != null)
                {
                    var questSystem = api.ModLoader.GetModSystem<QuestSystem>();

                    foreach (var actionObjective in actionObjectivesToProcess)
                    {
                        if (actionObjective == null) continue;
                        if (string.IsNullOrWhiteSpace(actionObjective.id)) continue;

                        // Only show objectives that have an objectiveId (named objectives)
                        if (string.IsNullOrWhiteSpace(actionObjective.objectiveId)) continue;

                        // Do not show gates as progress lines
                        if (actionObjective.id == "timeofday") continue;
                        if (actionObjective.id == "landgate") continue;

                        // Do not show technical wrapper objectives
                        if (actionObjective.id == "sequence") continue;

                        // Show interact-with-entity objectives for debugging
                        // if (actionObjective.id == "interactwithentity") continue;

                        // randomkill already has its own slot lines
                        if (actionObjective.id == "randomkill") continue;

                        var impl = questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue(actionObjective.id, out var objectiveImpl)
                            ? objectiveImpl
                            : null;

                        var prog = impl?.GetProgress(player, actionObjective.args);
                        if (prog == null || prog.Count == 0) continue;

                        // Try custom per-objective progress string first
                        string customKeyBase = activeQuest.questId + "-obj-" + (string.IsNullOrWhiteSpace(actionObjective.objectiveId) ? actionObjective.id : actionObjective.objectiveId);
                        string customProgress = LocalizationUtils.GetSafe(customKeyBase, prog.Cast<object>().ToArray());
                        if (!string.IsNullOrWhiteSpace(customProgress) && !string.Equals(customProgress, customKeyBase, StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add($"- {ApplyPrefixes(customProgress, actionObjective.objectiveId, actionObjectivesToProcess)}");
                            continue;
                        }

                        string objectiveLabel;
                        if (actionObjective.id == "walkdistance")
                        {
                            objectiveLabel = LocalizationUtils.GetSafe("alegacyvsquest:objective-walkdistance");
                        }
                        else
                        {
                            var candidate = LocalizationUtils.GetSafe($"alegacyvsquest:objective-{actionObjective.id}");
                            objectiveLabel = string.Equals(candidate, $"alegacyvsquest:objective-{actionObjective.id}", StringComparison.OrdinalIgnoreCase)
                                ? actionObjective.id
                                : candidate;
                        }

                        if (actionObjective.id == "killactiontarget" && actionObjective.args != null && actionObjective.args.Length >= 3 && prog.Count >= 2)
                        {
                            string targetId = actionObjective.args[2];
                            string targetCode = targetId?.Trim();

                            if (!string.IsNullOrWhiteSpace(targetCode))
                            {
                                int lastColon = targetCode.LastIndexOf(':');
                                if (lastColon >= 0 && lastColon < targetCode.Length - 1)
                                {
                                    targetCode = targetCode.Substring(lastColon + 1);
                                }

                                string targetName = MobLocalizationUtils.GetMobDisplayName(targetCode);
                                if (string.IsNullOrWhiteSpace(targetName))
                                {
                                    targetName = targetCode;
                                }

                                string killLine = Lang.Get("alegacyvsquest:progress-pair", targetName, prog[0], prog[1]);
                                lines.Add($"- {ApplyPrefixes(killLine, actionObjective.objectiveId, actionObjectivesToProcess)}");
                                continue;
                            }
                        }

                        string line;
                        if (actionObjective.id == "walkdistance" && prog.Count >= 2)
                        {
                            var meterUnit = LocalizationUtils.GetSafe("alegacyvsquest:unit-meter-short");
                            line = Lang.Get("alegacyvsquest:progress-walkdistance", objectiveLabel, prog[0], prog[1], meterUnit);
                        }
                        else if (prog.Count >= 2)
                        {
                            line = Lang.Get("alegacyvsquest:progress-pair", objectiveLabel, prog[0], prog[1]);
                        }
                        else
                        {
                            line = Lang.Get("alegacyvsquest:progress-single", objectiveLabel, prog[0]);
                        }

                        lines.Add($"- {ApplyPrefixes(line, actionObjective.objectiveId, actionObjectivesToProcess)}");
                    }
                }

                AppendLandClaimInfo(lines, questDef, wa);
                return string.Join("\n", lines);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[alegacyvsquest] Error building progress text for quest '{activeQuest.questId}': {e}");
                return LocalizationUtils.GetSafe("alegacyvsquest:progress-load-error");
            }
        }

        private static void AppendLandClaimInfo(List<string> lines, Quest questDef, ITreeAttribute wa)
        {
            if (lines == null || wa == null || questDef == null || string.IsNullOrWhiteSpace(questDef.id)) return;

            if (!string.Equals(questDef.id, "albase:treasurer-buy-allowance", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(questDef.id, "albase:treasurer-buy-maxareas", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int allowance = wa.GetInt("landclaimallowance", 0);
            int maxAreas = wa.GetInt("landclaimmaxareas", 0);

            lines.Add($"- {LocalizationUtils.GetSafe("albase:landclaim-extra-allowance", allowance)}");
            lines.Add($"- {LocalizationUtils.GetSafe("albase:landclaim-extra-areas", maxAreas)}");
        }
    }
}
