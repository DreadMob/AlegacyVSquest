using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class QuestLandNotificationSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private long listenerId;

        private List<QuestLandConfig> configs = new List<QuestLandConfig>();
        private QuestSystem questSystem;

        private readonly Dictionary<string, (QuestLandConfig cfg, int prefixLen)> bestConfigByQuestId = new Dictionary<string, (QuestLandConfig cfg, int prefixLen)>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, (int x, int y, int z)> lastBlockPosByPlayerUid = new Dictionary<string, (int x, int y, int z)>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> lastClaimByPlayerUid = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, int> lastDimensionByPlayerUid = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> lastActionMsByPlayerUid = new Dictionary<string, long>(StringComparer.Ordinal);

        // Cache: playerUID -> (questId, config, lastCheckMs) - avoid recalculating every tick
        private readonly Dictionary<string, (string questId, QuestLandConfig config, long checkMs)> playerQuestCache = new Dictionary<string, (string, QuestLandConfig, long)>(StringComparer.Ordinal);
        private const long PlayerCacheValidityMs = 10000; // 10 seconds

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            questSystem = api.ModLoader.GetModSystem<QuestSystem>();

            // Load all questland configs from all mods that provide config/questland.json
            // (domain is owned by each mod; this system stays domain-agnostic)
            configs.Clear();
            bestConfigByQuestId.Clear();
            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = sapi.Assets.GetMany<QuestLandConfig>(sapi.Logger, "config/questland", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        configs.Add(asset.Value);
                    }
                }
            }

            listenerId = sapi.Event.RegisterGameTickListener(OnTick, 5000); // Увеличили с 1000 до 5000мс (5 секунд)
        }

        private void OnTick(float dt)
        {
            var sw = global::VsQuest.QuestProfiler.StartMeasurement("QuestLandNotificationSystem.OnTick");
            try
            {
                OnTickInternal(dt);
            }
            finally
            {
                global::VsQuest.QuestProfiler.EndMeasurement("QuestLandNotificationSystem.OnTick", sw);
            }
        }

        private void OnTickInternal(float dt)
        {
            var players = sapi?.World?.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            long nowMs = sapi.World.ElapsedMilliseconds;

            for (int i = 0; i < players.Length; i++)
            {
                if (!(players[i] is IServerPlayer sp)) continue;

                var pos = sp.Entity?.Pos;
                if (pos == null) continue;

                int curDim = sp.Entity.Pos.Dimension;

                int curX = pos.AsBlockPos.X;
                int curY = pos.AsBlockPos.Y;
                int curZ = pos.AsBlockPos.Z;

                string uid = sp.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                if (lastDimensionByPlayerUid.TryGetValue(uid, out int lastDim) && lastDim != curDim)
                {
                    // During teleports / dimension changes, claim lookup can briefly return stale/null data.
                    // Suppress enter/exit firing in that moment to avoid double actions (which can cause rubberband/jerk).
                    lastDimensionByPlayerUid[uid] = curDim;
                    lastBlockPosByPlayerUid[uid] = (curX, curY, curZ);
                    lastClaimByPlayerUid[uid] = GetCurrentClaimName(sp);
                    continue;
                }

                if (!lastDimensionByPlayerUid.ContainsKey(uid))
                {
                    lastDimensionByPlayerUid[uid] = curDim;
                }

                lastBlockPosByPlayerUid.TryGetValue(uid, out var lastPos);
                int lastX = lastPos.x;
                int lastY = lastPos.y;
                int lastZ = lastPos.z;
                if (curX == lastX && curY == lastY && curZ == lastZ)
                {
                    continue;
                }

                lastBlockPosByPlayerUid[uid] = (curX, curY, curZ);

                // questland is a quest helper: only fire while player has an active quest matching allowed prefixes.
                // Important: don't query claims unless we actually need questland for this player.
                if (!TryGetRelevantQuestAndConfig(sp, out string questId, out var config))
                {
                    continue;
                }

                string currentClaim = GetCurrentClaimName(sp);

                lastClaimByPlayerUid.TryGetValue(uid, out string lastClaim);

                if (string.Equals(currentClaim, lastClaim, StringComparison.Ordinal))
                {
                    continue;
                }

                lastClaimByPlayerUid[uid] = currentClaim;

                // Throttle rapid claim transitions (often happens around teleports / chunk loads).
                // We still update lastClaim above, but avoid firing multiple actions back-to-back.
                if (nowMs > 0 && lastActionMsByPlayerUid.TryGetValue(uid, out long lastActionMs))
                {
                    if (nowMs - lastActionMs < 600)
                    {
                        continue;
                    }
                }

                // Only react to claims explicitly tracked by this config (e.g. BossHunt / Ossuary).
                bool curTracked = IsTrackedQuestLandClaim(config, currentClaim);
                bool lastTracked = IsTrackedQuestLandClaim(config, lastClaim);

                if (curTracked && !lastTracked)
                {
                    FireEnter(sp, questId, config, currentClaim);
                    if (nowMs > 0) lastActionMsByPlayerUid[uid] = nowMs;
                }
                else if (!curTracked && lastTracked)
                {
                    FireExit(sp, questId, config, lastClaim);
                    if (nowMs > 0) lastActionMsByPlayerUid[uid] = nowMs;
                }
                else if (curTracked && lastTracked && !string.Equals(currentClaim, lastClaim, StringComparison.Ordinal))
                {
                    // Transition between two tracked claims: exit then enter.
                    FireExit(sp, questId, config, lastClaim);
                    FireEnter(sp, questId, config, currentClaim);
                    if (nowMs > 0) lastActionMsByPlayerUid[uid] = nowMs;
                }
            }
        }

        private bool IsTrackedQuestLandClaim(QuestLandConfig config, string claimName)
        {
            if (string.IsNullOrWhiteSpace(claimName)) return false;
            if (config?.enterMessages == null || config.enterMessages.Count == 0) return false;

            foreach (var kvp in config.enterMessages)
            {
                if (string.Equals(kvp.Key, claimName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private bool TryGetRelevantQuestAndConfig(IServerPlayer sp, out string questId, out QuestLandConfig config)
        {
            questId = null;
            config = null;
            if (questSystem == null) return false;
            if (configs == null || configs.Count == 0) return false;

            string uid = sp.PlayerUID;
            if (string.IsNullOrWhiteSpace(uid)) return false;

            // Check player-level cache first (valid for 10 seconds)
            long nowMs = sapi.World.ElapsedMilliseconds;
            if (playerQuestCache.TryGetValue(uid, out var cached))
            {
                if (nowMs - cached.checkMs < PlayerCacheValidityMs && cached.config != null)
                {
                    questId = cached.questId;
                    config = cached.config;
                    return !string.IsNullOrWhiteSpace(questId);
                }
            }

            var active = questSystem.GetPlayerQuests(uid);
            if (active == null || active.Count == 0) return false;

            // Prefer the config with the most specific (longest) prefix match.
            string bestQuestId = null;
            QuestLandConfig bestConfig = null;
            int bestPrefixLen = -1;

            foreach (var aq in active)
            {
                var qid = aq?.questId;
                if (string.IsNullOrWhiteSpace(qid)) continue;

                if (TryGetBestConfigForQuestIdCached(qid, out var cachedCfg, out int cachedLen))
                {
                    if (cachedCfg != null && cachedLen > bestPrefixLen)
                    {
                        bestPrefixLen = cachedLen;
                        bestQuestId = qid;
                        bestConfig = cachedCfg;
                    }
                    continue;
                }

                var (localCfg, localLen) = ComputeBestConfigForQuestId(qid);
                bestConfigByQuestId[qid] = (localCfg, localLen);

                if (localCfg != null && localLen > bestPrefixLen)
                {
                    bestPrefixLen = localLen;
                    bestQuestId = qid;
                    bestConfig = localCfg;
                }
            }

            if (bestConfig == null || string.IsNullOrWhiteSpace(bestQuestId)) return false;

            // Cache the result
            playerQuestCache[uid] = (bestQuestId, bestConfig, nowMs);

            questId = bestQuestId;
            config = bestConfig;
            return true;
        }

        private (QuestLandConfig cfg, int prefixLen) ComputeBestConfigForQuestId(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || configs == null || configs.Count == 0)
            {
                return (null, -1);
            }

            QuestLandConfig best = null;
            int bestLen = -1;

            for (int ci = 0; ci < configs.Count; ci++)
            {
                var cfg = configs[ci];
                var prefixes = cfg?.allowedQuestPrefixes;
                if (prefixes == null || prefixes.Length == 0) continue;

                for (int pi = 0; pi < prefixes.Length; pi++)
                {
                    var p = prefixes[pi];
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    if (questId.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        int len = p.Length;
                        if (len > bestLen)
                        {
                            bestLen = len;
                            best = cfg;
                        }
                    }
                }
            }

            return (best, bestLen);
        }

        private bool TryGetBestConfigForQuestIdCached(string questId, out QuestLandConfig cfg, out int prefixLen)
        {
            cfg = null;
            prefixLen = -1;
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (bestConfigByQuestId.TryGetValue(questId, out var cached))
            {
                cfg = cached.cfg;
                prefixLen = cached.prefixLen;
                return true;
            }

            return false;
        }

        private void FireEnter(IServerPlayer sp, string questId, QuestLandConfig config, string claimName)
        {
            string msg = GetEnterMessage(config, claimName);
            FireActionTemplate(sp, questId, config?.enterAction, msg, claimName, null);
            
            // Force check inland objectives when entering a land claim
            TryFireInlandObjectives(sp, questId, claimName);
        }

        private void FireExit(IServerPlayer sp, string questId, QuestLandConfig config, string lastClaimName)
        {
            string msg = GetExitMessage(config, lastClaimName);
            FireActionTemplate(sp, questId, config?.exitAction, msg, null, lastClaimName);
        }

        private void FireActionTemplate(IServerPlayer sp, string questId, string actionTemplate, string message, string claimName, string lastClaimName)
        {
            if (sapi == null || sp == null) return;

            if (string.IsNullOrWhiteSpace(actionTemplate))
            {
                actionTemplate = "notify '{message}'";
            }

            string safeMsg = (message ?? "").Replace("'", "\\'");
            string safeClaim = (claimName ?? "").Replace("'", "\\'");
            string safeLast = (lastClaimName ?? "").Replace("'", "\\'");

            string actionString = actionTemplate
                .Replace("{message}", safeMsg)
                .Replace("{claim}", safeClaim)
                .Replace("{lastclaim}", safeLast);

            var actionStrings = actionString.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (actionStrings.Length == 0) return;

            var msg = new QuestAcceptedMessage { questGiverId = sp.Entity.EntityId, questId = questId };

            // Execute actions with small delays to spread network load and reduce jitter
            if (actionStrings.Length == 1)
            {
                ActionStringExecutor.Execute(sapi, msg, sp, actionStrings[0]);
            }
            else
            {
                ExecuteActionsWithDelay(sp, msg, actionStrings, 0);
            }
        }

        private void ExecuteActionsWithDelay(IServerPlayer sp, QuestMessage msg, string[] actions, int index)
        {
            if (index >= actions.Length) return;

            ActionStringExecutor.Execute(sapi, msg, sp, actions[index]);

            // Small delay between actions to spread network load
            if (index < actions.Length - 1)
            {
                sapi.Event.RegisterCallback(_ => ExecuteActionsWithDelay(sp, msg, actions, index + 1), 50);
            }
        }

        private string GetEnterMessage(QuestLandConfig config, string claimName)
        {
            if (!string.IsNullOrWhiteSpace(claimName) && config?.enterMessages != null)
            {
                foreach (var kvp in config.enterMessages)
                {
                    if (string.Equals(kvp.Key, claimName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value)) return Lang.Get(kvp.Value);
                    }
                }
            }

            return Lang.Get("alegacyvsquest:questland-enter", claimName);
        }

        private string GetExitMessage(QuestLandConfig config, string lastClaimName)
        {
            if (!string.IsNullOrWhiteSpace(config?.defaultExitMessage))
            {
                return Lang.Get(config.defaultExitMessage);
            }

            return Lang.Get("alegacyvsquest:questland-exit", lastClaimName);
        }

        private string GetCurrentClaimName(IServerPlayer sp)
        {
            if (sp?.Entity?.Pos == null) return null;

            var claimsApi = sp.Entity.World?.Claims;
            if (claimsApi == null) return null;

            BlockPos pos = sp.Entity.Pos.AsBlockPos;
            var claims = claimsApi.Get(pos);
            if (claims == null || claims.Length == 0) return null;

            for (int i = 0; i < claims.Length; i++)
            {
                var desc = claims[i]?.Description;
                if (!string.IsNullOrWhiteSpace(desc)) return desc;

                var ownerName = claims[i]?.LastKnownOwnerName;
                if (!string.IsNullOrWhiteSpace(ownerName)) return ownerName;
            }

            return null;
        }

        private void TryFireInlandObjectives(IServerPlayer sp, string questId, string claimName)
        {
            if (sp == null || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(claimName)) return;
            if (questSystem == null) return;

            var activeQuests = questSystem.GetPlayerQuests(sp.PlayerUID);
            if (activeQuests == null) return;

            foreach (var activeQuest in activeQuests)
            {
                if (activeQuest == null || !string.Equals(activeQuest.questId, questId, StringComparison.OrdinalIgnoreCase)) continue;

                if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null) continue;

                // Get action objectives for current stage
                var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
                if (stageActionObjectives == null) continue;

                // Check all inland objectives
                foreach (var ao in stageActionObjectives)
                {
                    if (ao == null || ao.id != "inland") continue;
                    if (string.IsNullOrWhiteSpace(ao.onCompleteActions)) continue;

                    // Check if this inland objective matches the current claim
                    if (questSystem.ActionObjectiveRegistry.TryGetValue("inland", out var impl) && impl != null)
                    {
                        bool ok = impl.IsCompletable(sp, ao.args);
                        if (ok)
                        {
                            sapi.Logger.Debug($"[QuestLandNotificationSystem] Firing inland objective {ao.objectiveId} for quest {questId} in claim {claimName}");
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, sp, activeQuest, ao, ao.objectiveId, true);
                        }
                    }
                }
            }
        }

        private class QuestLandConfig
        {
            public string[] allowedQuestPrefixes { get; set; }
            public string enterAction { get; set; }
            public string exitAction { get; set; }
            public string defaultExitMessage { get; set; }
            public Dictionary<string, string> enterMessages { get; set; }
        }
    }
}
