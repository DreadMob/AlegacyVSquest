using System;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class RandomKillQuestUtils
    {
        private static string LocalizeMobName(string code)
        {
            return MobLocalizationUtils.GetMobDisplayName(code);
        }


        public static string SlotsKey(string questId) => $"alegacyvsquest:randkill:{questId}:slots";
        public static string SlotCodeKey(string questId, int slot) => $"alegacyvsquest:randkill:{questId}:slot{slot}:code";
        public static string SlotNeedKey(string questId, int slot) => $"alegacyvsquest:randkill:{questId}:slot{slot}:need";
        public static string SlotHaveKey(string questId, int slot) => $"alegacyvsquest:randkill:{questId}:slot{slot}:have";
        public static string OnProgressKey(string questId) => $"alegacyvsquest:randkill:{questId}:onprogress";
        public static string OnCompleteKey(string questId) => $"alegacyvsquest:randkill:{questId}:oncomplete";

        public static bool TryHandleKill(ICoreServerAPI sapi, IServerPlayer serverPlayer, ActiveQuest activeQuest, string killedCode)
        {
            if (sapi == null || serverPlayer == null || activeQuest == null) return false;
            if (string.IsNullOrWhiteSpace(killedCode)) return false;

            var wa = serverPlayer.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string questId = activeQuest.questId;
            if (string.IsNullOrWhiteSpace(questId)) return false;


            // Multi-slot keys
            int slots = wa.GetInt(SlotsKey(questId), 0);
            if (slots <= 0) return false;

            bool any = false;
            for (int slot = 0; slot < slots; slot++)
            {
                any |= TryHandleSlotKill(sapi, serverPlayer, activeQuest, killedCode, slot);
            }
            return any;
        }


        private static bool TryHandleSlotKill(ICoreServerAPI sapi, IServerPlayer serverPlayer, ActiveQuest activeQuest, string killedCode, int slot)
        {
            var wa = serverPlayer.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string questId = activeQuest.questId;
            string codeKey = SlotCodeKey(questId, slot);
            if (!wa.HasAttribute(codeKey)) return false;

            string targetCode = wa.GetString(codeKey, null);
            if (string.IsNullOrWhiteSpace(targetCode)) return false;
            if (!LocalizationUtils.MobCodeMatches(targetCode, killedCode)) return false;

            string needKey = SlotNeedKey(questId, slot);
            string haveKey = SlotHaveKey(questId, slot);

            int need = wa.GetInt(needKey, 0);
            if (need <= 0) return false;

            int have = wa.GetInt(haveKey, 0);
            if (have >= need) return false;

            have++;
            if (have > need) have = need;
            wa.SetInt(haveKey, have);
            wa.MarkPathDirty(haveKey);

            FireActions(sapi, serverPlayer, activeQuest, have >= need);

            if (have >= need)
            {
                TryFireObjectiveCompletedActions(sapi, serverPlayer, activeQuest);
            }
            return true;
        }

        private static void TryFireObjectiveCompletedActions(ICoreServerAPI sapi, IServerPlayer player, ActiveQuest activeQuest)
        {
            if (sapi == null || player == null || activeQuest == null) return;
            
            if (!QuestRegistryService.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null) return;
            if (!QuestRegistryService.ActionObjectiveRegistry.TryGetValue("randomkill", out var impl) || impl == null) return;

            // Use stage-aware action objectives
            var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
            if (stageActionObjectives == null) return;

            for (int i = 0; i < stageActionObjectives.Count; i++)
            {
                var ao = stageActionObjectives[i];
                if (ao?.id != "randomkill") continue;

                bool ok;
                try
                {
                    ok = impl.IsCompletable(player, ao.args);
                }
                catch (Exception)
                {
                    // IsCompletable failed for randomkill objective, treat as not complete
                    ok = false;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, ao, ao.objectiveId, ok);
                break;
            }
        }

        internal static void FireActions(ICoreServerAPI sapi, IServerPlayer serverPlayer, ActiveQuest activeQuest, bool completedThisTick)
        {
            var wa = serverPlayer.Entity?.WatchedAttributes;
            if (wa == null) return;

            string questId = activeQuest.questId;
            var msg = new QuestAcceptedMessage { questGiverId = activeQuest.questGiverId, questId = questId };

            string progressActions = wa.GetString(OnProgressKey(questId), null);
            if (!string.IsNullOrWhiteSpace(progressActions))
            {
                ActionStringExecutor.Execute(sapi, msg, serverPlayer, progressActions);
            }

            if (completedThisTick)
            {
                string completeActions = wa.GetString(OnCompleteKey(questId), null);
                if (!string.IsNullOrWhiteSpace(completeActions))
                {
                    ActionStringExecutor.Execute(sapi, msg, serverPlayer, completeActions);
                }
            }
        }

        public static void ParseRollArgsSingle(string[] args, out int minCount, out int maxCount, out string template, out string progressActions, out string completeActions, out int mobListStartIndex)
        {
            if (args == null || args.Length < 4)
            {
                throw new QuestException("The 'rollkillobjective' action requires at least 4 arguments: minCount, maxCount, messageTemplate, and at least 1 entityCode.");
            }

            if (!int.TryParse(args[0], out minCount)) minCount = 1;
            if (!int.TryParse(args[1], out maxCount)) maxCount = minCount;
            if (minCount < 1) minCount = 1;
            if (maxCount < minCount) maxCount = minCount;

            template = args[2];

            progressActions = null;
            completeActions = null;
            mobListStartIndex = 3;

            if (args.Length >= 6)
            {
                progressActions = string.IsNullOrWhiteSpace(args[3]) ? null : args[3];
                completeActions = string.IsNullOrWhiteSpace(args[4]) ? null : args[4];
                mobListStartIndex = 5;
            }
        }

        public static void ParseRollArgsMulti(string[] args, out int objectiveCount, out int minCount, out int maxCount, out string template, out string progressActions, out string completeActions, out int mobListStartIndex)
        {
            if (args == null || args.Length < 5)
            {
                throw new QuestException("The 'rollkillobjectives' action requires at least 5 arguments: objectiveCount, minCount, maxCount, messageTemplate, and at least 1 entityCode.");
            }

            if (!int.TryParse(args[0], out objectiveCount)) objectiveCount = 1;
            if (objectiveCount < 1) objectiveCount = 1;

            if (!int.TryParse(args[1], out minCount)) minCount = 1;
            if (!int.TryParse(args[2], out maxCount)) maxCount = minCount;
            if (minCount < 1) minCount = 1;
            if (maxCount < minCount) maxCount = minCount;

            template = args[3];

            progressActions = null;
            completeActions = null;
            mobListStartIndex = 4;

            if (args.Length >= 7)
            {
                progressActions = string.IsNullOrWhiteSpace(args[4]) ? null : args[4];
                completeActions = string.IsNullOrWhiteSpace(args[5]) ? null : args[5];
                mobListStartIndex = 6;
            }
        }

        public static List<string> ReadMobCodes(string[] args, int startIndex)
        {
            var mobCodes = new List<string>();
            if (args == null) return mobCodes;

            for (int i = startIndex; i < args.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(args[i])) mobCodes.Add(args[i]);
            }

            return mobCodes;
        }

        public static void StoreQuestActionStrings(IServerPlayer byPlayer, string questId, string progressActions, string completeActions)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            string progressActionsKey = OnProgressKey(questId);
            string completeActionsKey = OnCompleteKey(questId);

            if (progressActions != null) byPlayer.Entity.WatchedAttributes.SetString(progressActionsKey, progressActions);
            else byPlayer.Entity.WatchedAttributes.RemoveAttribute(progressActionsKey);

            if (completeActions != null) byPlayer.Entity.WatchedAttributes.SetString(completeActionsKey, completeActions);
            else byPlayer.Entity.WatchedAttributes.RemoveAttribute(completeActionsKey);

            byPlayer.Entity.WatchedAttributes.MarkPathDirty(progressActionsKey);
            byPlayer.Entity.WatchedAttributes.MarkPathDirty(completeActionsKey);
        }

        public static void SendRollNotification(ICoreServerAPI api, IServerPlayer byPlayer, string template, int need, string code)
        {
            api.Network.GetChannel("alegacyvsquest").SendPacket(new ShowNotificationMessage()
            {
                Template = template,
                Need = need,
                MobCode = code,
                Notification = null
            }, byPlayer);
        }
    }
}
