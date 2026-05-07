using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace VsQuest
{
    public class QuizSystem
    {
        private readonly QuestSystem questSystem;
        private Dictionary<string, QuizDefinition> quizRegistry = new Dictionary<string, QuizDefinition>(StringComparer.OrdinalIgnoreCase);

        public QuizSystem(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        private static string OptionOrderKey(string quizId, int questionIndex)
        {
            return $"vsquest:quiz:{quizId}:order:{questionIndex}";
        }

        private static int[] ParseOptionOrder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var parts = value.Split(',');
            if (parts.Length != 4) return null;

            var order = new int[4];
            var seen = new bool[5];
            for (int i = 0; i < 4; i++)
            {
                if (!int.TryParse(parts[i], out int parsed)) return null;
                if (parsed < 1 || parsed > 4) return null;
                if (seen[parsed]) return null;
                seen[parsed] = true;
                order[i] = parsed;
            }

            return order;
        }

        private static int[] CreateRandomOptionOrder(ICoreServerAPI sapi)
        {
            var order = new int[] { 1, 2, 3, 4 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                int swap = sapi.World.Rand.Next(i + 1);
                int temp = order[i];
                order[i] = order[swap];
                order[swap] = temp;
            }
            return order;
        }

        private static int[] GetOrCreateOptionOrder(ICoreServerAPI sapi, ITreeAttribute wa, string quizId, int questionIndex)
        {
            string key = OptionOrderKey(quizId, questionIndex);
            var order = ParseOptionOrder(wa?.GetString(key, null));
            if (order != null) return order;

            order = CreateRandomOptionOrder(sapi);
            wa?.SetString(key, string.Join(",", order));
            MarkDirty(wa, key);
            return order;
        }

        private static int[] TryGetOptionOrder(ITreeAttribute wa, string quizId, int questionIndex)
        {
            if (wa == null) return null;
            string key = OptionOrderKey(quizId, questionIndex);
            return ParseOptionOrder(wa.GetString(key, null));
        }

        private static void MarkDirty(ITreeAttribute wa, string key)
        {
            if (wa is SyncedTreeAttribute synced)
            {
                synced.MarkPathDirty(key);
            }
        }

        public void LoadFromAssets(ICoreAPI api)
        {
            if (api == null) return;

            quizRegistry = new Dictionary<string, QuizDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in api.ModLoader.Mods)
            {
                try
                {
                    var quizAssets = api.Assets.GetMany<QuizDefinition>(api.Logger, "config/quizzes", mod.Info.ModID);
                    foreach (var quizAsset in quizAssets)
                    {
                        var def = quizAsset.Value;
                        if (def == null || string.IsNullOrWhiteSpace(def.id)) continue;
                        quizRegistry[def.id] = def;
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Warning("[QuizSystem] Failed to load quiz assets from mod {0}: {1}", mod.Info.ModID, ex.Message);
                }
            }
        }

        public void OnShowQuizMessage(ShowQuizMessage message, ICoreClientAPI capi)
        {
            if (message == null || capi == null) return;
            QuizDialogGui.ShowFromMessage(message, capi);
        }

        public void OnOpenQuizMessage(IServerPlayer player, OpenQuizMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.QuizId)) return;

            StartQuiz(sapi, player, message.QuizId, reset: message.Reset);
        }

        public void OnSubmitQuizAnswerMessage(IServerPlayer player, SubmitQuizAnswerMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.QuizId)) return;

            if (!quizRegistry.TryGetValue(message.QuizId, out var def) || def == null) return;

            if (message.Retry)
            {
                StartQuiz(sapi, player, message.QuizId, reset: true);
                return;
            }

            HandleQuizAnswer(sapi, player, def, message.SelectedOption);
        }

        public void StartQuiz(ICoreServerAPI sapi, IServerPlayer player, string quizId, bool reset)
        {
            if (sapi == null || player?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(quizId)) return;
            if (!quizRegistry.TryGetValue(quizId, out var def) || def == null) return;

            var wa = player.Entity.WatchedAttributes;

            string idxKey = $"vsquest:quiz:{quizId}:index";
            string correctKey = $"vsquest:quiz:{quizId}:correct";
            string wrongKey = $"vsquest:quiz:{quizId}:wrong";

            if (reset)
            {
                wa.SetInt(idxKey, 1);
                wa.SetInt(correctKey, 0);
                wa.SetInt(wrongKey, 0);
                MarkDirty(wa, idxKey);
                MarkDirty(wa, correctKey);
                MarkDirty(wa, wrongKey);

                int total = def.questionCount > 0 ? def.questionCount : (def.correctOptions?.Length ?? 0);
                for (int i = 1; i <= total; i++)
                {
                    string orderKey = OptionOrderKey(quizId, i);
                    wa.RemoveAttribute(orderKey);
                    MarkDirty(wa, orderKey);
                }
            }

            SendQuizQuestion(sapi, player, def);
        }

        private void SendQuizQuestion(ICoreServerAPI sapi, IServerPlayer player, QuizDefinition def)
        {
            if (sapi == null || player?.Entity?.WatchedAttributes == null || def == null) return;

            var wa = player.Entity.WatchedAttributes;
            string quizId = def.id;

            int total = def.questionCount > 0 ? def.questionCount : (def.correctOptions?.Length ?? 0);
            if (total <= 0) return;

            string idxKey = $"vsquest:quiz:{quizId}:index";
            string correctKey = $"vsquest:quiz:{quizId}:correct";
            string wrongKey = $"vsquest:quiz:{quizId}:wrong";

            int idx = wa.GetInt(idxKey, 1);
            int correct = wa.GetInt(correctKey, 0);
            int wrong = wa.GetInt(wrongKey, 0);

            if (idx < 1) idx = 1;
            bool finished = idx > total;

            string questionLangKey = null;
            string bodyLangKey = def.bodyLangKey;
            string aKey = null;
            string bKey = null;
            string cKey = null;
            string dKey = null;

            if (!finished)
            {
                if (!string.IsNullOrWhiteSpace(def.questionLangKeyFormat)) questionLangKey = string.Format(def.questionLangKeyFormat, idx);
                if (!string.IsNullOrWhiteSpace(def.optionALangKeyFormat)) aKey = string.Format(def.optionALangKeyFormat, idx);
                if (!string.IsNullOrWhiteSpace(def.optionBLangKeyFormat)) bKey = string.Format(def.optionBLangKeyFormat, idx);
                if (!string.IsNullOrWhiteSpace(def.optionCLangKeyFormat)) cKey = string.Format(def.optionCLangKeyFormat, idx);
                if (!string.IsNullOrWhiteSpace(def.optionDLangKeyFormat)) dKey = string.Format(def.optionDLangKeyFormat, idx);

                var optionKeys = new[] { aKey, bKey, cKey, dKey };
                var order = GetOrCreateOptionOrder(sapi, wa, quizId, idx);
                if (order != null)
                {
                    aKey = optionKeys[order[0] - 1];
                    bKey = optionKeys[order[1] - 1];
                    cKey = optionKeys[order[2] - 1];
                    dKey = optionKeys[order[3] - 1];
                }
            }
            else
            {
                bodyLangKey = GetResultBodyLangKey(def, correct);
            }

            sapi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName).SendPacket(new ShowQuizMessage
            {
                QuizId = quizId,
                QuestionIndex = Math.Min(idx, total),
                TotalQuestions = total,
                Correct = correct,
                Wrong = wrong,
                NeededCorrect = def.neededCorrect,
                IsFinished = finished,
                TitleLangKey = def.titleLangKey,
                QuestionLangKey = questionLangKey,
                OptionALangKey = aKey,
                OptionBLangKey = bKey,
                OptionCLangKey = cKey,
                OptionDLangKey = dKey,
                BodyLangKey = bodyLangKey,
                ProgressTemplateLangKey = def.progressTemplateLangKey,
                ResultTemplateLangKey = def.resultTemplateLangKey,
                RetryButtonLangKey = def.retryButtonLangKey,
                CloseButtonLangKey = def.closeButtonLangKey
            }, player);
        }

        private static string GetResultBodyLangKey(QuizDefinition def, int correct)
        {
            if (def == null) return null;

            var keys = def.resultBodyLangKeys;
            if (keys == null || keys.Length == 0) return def.bodyLangKey;

            var thresholds = def.resultBodyScoreThresholds;
            if (thresholds == null || thresholds.Length != keys.Length)
            {
                return keys[0];
            }

            string selected = keys[0];
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (correct >= thresholds[i]) selected = keys[i];
            }

            return selected;
        }

        private void HandleQuizAnswer(ICoreServerAPI sapi, IServerPlayer player, QuizDefinition def, int selectedOption)
        {
            if (sapi == null || player?.Entity?.WatchedAttributes == null || def == null) return;
            if (selectedOption < 1 || selectedOption > 4) return;

            var wa = player.Entity.WatchedAttributes;
            string quizId = def.id;

            int total = def.questionCount > 0 ? def.questionCount : (def.correctOptions?.Length ?? 0);
            if (total <= 0) return;

            string idxKey = $"vsquest:quiz:{quizId}:index";
            string correctKey = $"vsquest:quiz:{quizId}:correct";
            string wrongKey = $"vsquest:quiz:{quizId}:wrong";

            int idx = wa.GetInt(idxKey, 1);
            if (idx < 1) idx = 1;
            if (idx > total)
            {
                SendQuizQuestion(sapi, player, def);
                return;
            }

            int correct = wa.GetInt(correctKey, 0);
            int wrong = wa.GetInt(wrongKey, 0);

            int expected = 0;
            if (def.correctOptions != null && idx - 1 >= 0 && idx - 1 < def.correctOptions.Length)
            {
                expected = def.correctOptions[idx - 1];
            }

            int selectedOriginal = selectedOption;
            var order = TryGetOptionOrder(wa, quizId, idx);
            if (order != null && selectedOption >= 1 && selectedOption <= order.Length)
            {
                selectedOriginal = order[selectedOption - 1];
            }

            if (selectedOriginal == expected) correct++;
            else wrong++;

            idx++;

            wa.SetInt(idxKey, idx);
            wa.SetInt(correctKey, correct);
            wa.SetInt(wrongKey, wrong);
            MarkDirty(wa, idxKey);
            MarkDirty(wa, correctKey);
            MarkDirty(wa, wrongKey);

            if (idx > total && !string.IsNullOrWhiteSpace(def.scoreAttributeKey))
            {
                wa.SetInt(def.scoreAttributeKey, correct);
                MarkDirty(wa, def.scoreAttributeKey);

                var actionRegistry = questSystem?.ActionRegistry;
                if (actionRegistry != null && actionRegistry.TryGetValue("checkobjective", out var checkAction) && checkAction != null)
                {
                    checkAction.Execute(sapi, new QuestAcceptedMessage { questGiverId = 0, questId = quizId }, player, new string[0]);
                }
            }

            SendQuizQuestion(sapi, player, def);
        }
    }
}
