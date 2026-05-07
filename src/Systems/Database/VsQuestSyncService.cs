using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Database
{
    /// <summary>
    /// Sync service for writing quest progress to MySQL with debounce/batch logic.
    /// </summary>
    public class VsQuestSyncService
    {
        private readonly VsQuestDbClient _client;
        private readonly ICoreServerAPI _sapi;
        private readonly int _debounceSeconds;
        private readonly Timer _syncTimer;
        private readonly ConcurrentDictionary<string, PendingSync> _pendingSyncs;
        private readonly object _lock = new();

        private class PendingSync
        {
            public string PlayerUid { get; set; }
            public string PlayerName { get; set; }
            public Dictionary<string, QuestSyncData> Quests { get; set; } = new();
        }

        private class QuestSyncData
        {
            public string QuestId { get; set; }
            public string PlayerName { get; set; }
            public long? QuestGiverId { get; set; }
            public int CurrentStageIndex { get; set; }
            public List<int> CompletedStageIndices { get; set; } = new();
            public List<int> TrackerProgress { get; set; } = new();
            public string Status { get; set; } = "active";
            public DateTime? StartedAt { get; set; }
        }

        public VsQuestSyncService(VsQuestDbClient client, ICoreServerAPI sapi, int debounceSeconds = 30)
        {
            _client = client;
            _sapi = sapi;
            _debounceSeconds = debounceSeconds;
            _pendingSyncs = new ConcurrentDictionary<string, PendingSync>();

            _syncTimer = new Timer(OnSyncTick, null, TimeSpan.FromSeconds(debounceSeconds), TimeSpan.FromSeconds(debounceSeconds));
        }

        public void QueuePlayerQuest(string playerUid, string playerName, string questId,
            int currentStageIndex, List<int> completedStageIndices,
            List<int> trackerProgress, string status = "active")
        {
            if (!_client.IsEnabled) return;

            var pending = _pendingSyncs.GetOrAdd(playerUid, _ => new PendingSync
            {
                PlayerUid = playerUid,
                PlayerName = playerName,
            });

            lock (pending.Quests)
            {
                pending.Quests[questId] = new QuestSyncData
                {
                    QuestId = questId,
                    PlayerName = playerName,
                    CurrentStageIndex = currentStageIndex,
                    CompletedStageIndices = completedStageIndices ?? new List<int>(),
                    TrackerProgress = trackerProgress ?? new List<int>(),
                    Status = status,
                };
            }
        }

        public void QueueQuestCompletion(string playerUid, string playerName, string questId)
        {
            if (!_client.IsEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    var body = new
                    {
                        player_uid = playerUid,
                        player_name = playerName,
                        quest_id = questId,
                    };

                    var response = await _client.PostAsync("/vsquest/quest-completions", body);
                    if (!response.IsSuccess)
                    {
                        _sapi.Logger.Warning("[VsQuestSync] Failed to record quest completion: {0}", response.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[VsQuestSync] Exception recording quest completion: {0}", ex.Message);
                }
            });
        }

        public void QueueBossKill(string playerUid, string playerName, string bossKey)
        {
            if (!_client.IsEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    var body = new
                    {
                        player_name = playerName,
                    };

                    var encodedPlayerUid = Uri.EscapeDataString(playerUid);
                    var encodedBossKey = Uri.EscapeDataString(bossKey);
                    var response = await _client.PatchAsync($"/vsquest/boss-kills/{encodedPlayerUid}/{encodedBossKey}", body);
                    if (!response.IsSuccess)
                    {
                        _sapi.Logger.Warning("[VsQuestSync] Failed to record boss kill: {0}", response.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[VsQuestSync] Exception recording boss kill: {0}", ex.Message);
                }
            });
        }

        public void QueueNpcReputationSet(string playerUid, string playerName, string npcId, int reputation)
        {
            if (!_client.IsEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    var body = new
                    {
                        player_name = playerName,
                        reputation = reputation,
                    };

                    var encodedPlayerUid = Uri.EscapeDataString(playerUid);
                    var encodedNpcId = Uri.EscapeDataString(npcId);
                    var response = await _client.PutAsync($"/vsquest/npc-reputation/{encodedPlayerUid}/{encodedNpcId}", body);
                    if (!response.IsSuccess)
                    {
                        _sapi.Logger.Warning("[VsQuestSync] Failed to set NPC reputation: {0}", response.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[VsQuestSync] Exception setting NPC reputation: {0}", ex.Message);
                }
            });
        }

        private async void OnSyncTick(object state)
        {
            if (_pendingSyncs.IsEmpty) return;

            var toSync = new List<PendingSync>();
            lock (_lock)
            {
                foreach (var kvp in _pendingSyncs)
                {
                    toSync.Add(kvp.Value);
                }
                _pendingSyncs.Clear();
            }

            foreach (var pending in toSync)
            {
                foreach (var questKvp in pending.Quests)
                {
                    var quest = questKvp.Value;
                    await SyncQuestAsync(pending.PlayerUid, quest);
                }
            }
        }

        private async Task SyncQuestAsync(string playerUid, QuestSyncData quest)
        {
            try
            {
                var body = new
                {
                    player_name = quest.PlayerName,
                    current_stage = quest.CurrentStageIndex,
                    completed_stages = quest.CompletedStageIndices,
                    tracker_values = quest.TrackerProgress,
                    status = quest.Status,
                };

                var encodedPlayerUid = Uri.EscapeDataString(playerUid);
                var encodedQuestId = Uri.EscapeDataString(quest.QuestId);
                var response = await _client.PutAsync($"/vsquest/player-quests/{encodedPlayerUid}/{encodedQuestId}", body);

                if (!response.IsSuccess)
                {
                    _sapi.Logger.Warning("[VsQuestSync] Failed to sync quest {0} for player {1}: {2}",
                        quest.QuestId, playerUid, response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning("[VsQuestSync] Exception syncing quest {0} for player {1}: {2}",
                    quest.QuestId, playerUid, ex.Message);
            }
        }

        public void Flush()
        {
            OnSyncTick(null);
        }

        public void Dispose()
        {
            _syncTimer?.Dispose();
        }
    }
}
