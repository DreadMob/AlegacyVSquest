using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Database
{
    /// <summary>
    /// Sync service for writing quest data to MySQL immediately on lifecycle events.
    /// No periodic polling — syncs happen only when meaningful progress occurs.
    /// </summary>
    public class VsQuestSyncService
    {
        private readonly VsQuestDbClient _client;
        private readonly ICoreServerAPI _sapi;

        public VsQuestSyncService(VsQuestDbClient client, ICoreServerAPI sapi)
        {
            _client = client;
            _sapi = sapi;
        }

        public void QueuePlayerQuest(string playerUid, string playerName, string questId,
            int currentStageIndex, List<int> completedStageIndices,
            List<int> trackerProgress, string status = "active",
            string questTitle = null)
        {
            if (!_client.IsEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    var body = new
                    {
                        player_name = playerName,
                        current_stage = currentStageIndex,
                        completed_stages = completedStageIndices ?? new List<int>(),
                        tracker_values = trackerProgress ?? new List<int>(),
                        status = status,
                        quest_title = questTitle,
                    };

                    var encodedPlayerUid = Uri.EscapeDataString(playerUid);
                    var encodedQuestId = Uri.EscapeDataString(questId);
                    var response = await _client.PutAsync($"/vsquest/player-quests/{encodedPlayerUid}/{encodedQuestId}", body);

                    if (!response.IsSuccess)
                    {
                        _sapi.Logger.Warning("[VsQuestSync] Failed to sync quest {0} for player {1}: {2}",
                            questId, playerUid, response.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning("[VsQuestSync] Exception syncing quest {0} for player {1}: {2}",
                        questId, playerUid, ex.Message);
                }
            });
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

        public void QueueBossKill(string playerUid, string playerName, string bossKey, string bossDisplayName = null)
        {
            if (!_client.IsEnabled) return;

            Task.Run(async () =>
            {
                try
                {
                    var body = new
                    {
                        player_name = playerName,
                        boss_display_name = bossDisplayName,
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

        public void QueueNpcReputationSet(string playerUid, string playerName, string npcId, int reputation, string npcDisplayName = null)
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
                        npc_display_name = npcDisplayName,
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

        public void Dispose()
        {
            // Nothing to dispose — all operations are fire-and-forget Task.Run
        }
    }
}
