using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestPersistenceManager
    {
        private readonly ConcurrentDictionary<string, List<ActiveQuest>> playerQuests;
        private readonly HashSet<string> dirtyPlayerUIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ICoreServerAPI sapi;
        private long autosaveListenerId = -1;
        private const double AutosaveIntervalMs = 30000; // 30 секунд

        public QuestPersistenceManager(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            this.playerQuests = new ConcurrentDictionary<string, List<ActiveQuest>>();
            
            // Регистрируем автосохранение каждые 30 секунд
            autosaveListenerId = sapi.Event.RegisterGameTickListener(OnAutosaveTick, (int)AutosaveIntervalMs, 5000);
        }

        private void OnAutosaveTick(float dt)
        {
            var sw = global::VsQuest.QuestProfiler.StartMeasurement("QuestPersistenceManager.OnAutosaveTick");
            try
            {
                OnAutosaveTickInternal();
            }
            finally
            {
                global::VsQuest.QuestProfiler.EndMeasurement("QuestPersistenceManager.OnAutosaveTick", sw);
            }
        }

        private void OnAutosaveTickInternal()
        {
            if (dirtyPlayerUIDs.Count == 0) return;

            List<string> playersToSave;
            lock (dirtyPlayerUIDs)
            {
                playersToSave = new List<string>(dirtyPlayerUIDs);
                dirtyPlayerUIDs.Clear();
            }

            foreach (var playerUID in playersToSave)
            {
                try
                {
                    if (playerQuests.TryGetValue(playerUID, out var quests))
                    {
                        var dto = new List<ActiveQuestDto>(quests.Count);
                        for (int i = 0; i < quests.Count; i++)
                        {
                            dto.Add(ActiveQuestDto.FromDomain(quests[i]));
                        }

                        sapi.WorldManager.SaveGame.StoreData<List<ActiveQuestDto>>($"quests-{playerUID}", dto);
                    }
                }
                catch (Exception e)
                {
                    sapi.Logger.Warning("[alegacyvsquest] Failed to auto-save quests for player {0}: {1}", playerUID, e.Message);
                    // Возвращаем в очередь для повторной попытки
                    lock (dirtyPlayerUIDs)
                    {
                        dirtyPlayerUIDs.Add(playerUID);
                    }
                }
            }
        }

        public List<ActiveQuest> GetPlayerQuests(string playerUID)
        {
            return playerQuests.GetOrAdd(playerUID, (val) => LoadPlayerQuests(val));
        }

        public void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests)
        {
            var dto = new List<ActiveQuestDto>(activeQuests?.Count ?? 0);
            if (activeQuests != null)
            {
                for (int i = 0; i < activeQuests.Count; i++)
                {
                    // Export progress before saving
                    activeQuests[i]?.ExportProgress();
                    dto.Add(ActiveQuestDto.FromDomain(activeQuests[i]));
                }
            }

            sapi.WorldManager.SaveGame.StoreData<List<ActiveQuestDto>>($"quests-{playerUID}", dto);
        }

        public void MarkDirty(string playerUID)
        {
            if (string.IsNullOrEmpty(playerUID)) return;
            lock (dirtyPlayerUIDs)
            {
                dirtyPlayerUIDs.Add(playerUID);
            }
        }

        private List<ActiveQuest> LoadPlayerQuests(string playerUID)
        {
            try
            {
                var dto = sapi.WorldManager.SaveGame.GetData<List<ActiveQuestDto>>($"quests-{playerUID}", new List<ActiveQuestDto>());
                var domain = new List<ActiveQuest>(dto?.Count ?? 0);

                if (dto != null)
                {
                    for (int i = 0; i < dto.Count; i++)
                    {
                        domain.Add(dto[i]?.ToDomain());
                    }
                }

                return domain;
            }
            catch (ProtoException)
            {
                try
                {
                    var legacy = sapi.WorldManager.SaveGame.GetData<List<ActiveQuest>>($"quests-{playerUID}", new List<ActiveQuest>());

                    try
                    {
                        var migrated = new List<ActiveQuestDto>(legacy?.Count ?? 0);
                        if (legacy != null)
                        {
                            for (int i = 0; i < legacy.Count; i++)
                            {
                                migrated.Add(ActiveQuestDto.FromDomain(legacy[i]));
                            }
                        }

                        sapi.WorldManager.SaveGame.StoreData<List<ActiveQuestDto>>($"quests-{playerUID}", migrated);
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[alegacyvsquest] Failed to migrate legacy quest save for player {0}: {1}", playerUID, ex.Message);
                    }

                    return legacy;
                }
                catch (ProtoException)
                {
                    sapi.Logger.Error("Could not load quests for player with id {0}, corrupted quests will be deleted.", playerUID);
                    return new List<ActiveQuest>();
                }
            }
        }

        public void SaveAllPlayerQuests()
        {
            foreach (var player in playerQuests)
            {
                SavePlayerQuests(player.Key, player.Value);
            }
        }

        public void UnloadPlayerQuests(string playerUID)
        {
            if (playerQuests.TryGetValue(playerUID, out var activeQuests))
            {
                // Синхронное сохранение при disconnect - важно!
                SavePlayerQuests(playerUID, activeQuests);
                playerQuests.TryRemove(playerUID, out _);
            }
            
            // Убираем из очереди автосохранения
            lock (dirtyPlayerUIDs)
            {
                dirtyPlayerUIDs.Remove(playerUID);
            }
        }

        public void Dispose()
        {
            if (autosaveListenerId >= 0)
            {
                sapi.Event.UnregisterGameTickListener(autosaveListenerId);
                autosaveListenerId = -1;
            }
            
            // Форсированное сохранение при выгрузке
            OnAutosaveTick(0);
        }
    }
}
