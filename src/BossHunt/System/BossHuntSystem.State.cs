using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using ProtoBuf;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        private void LoadState()
        {
            try
            {
                var dto = sapi.WorldManager.SaveGame.GetData<BossHuntWorldStateDto>(SaveKey, new BossHuntWorldStateDto());
                state = dto?.ToDomain() ?? new BossHuntWorldState();
            }
            catch (ProtoException)
            {
                try
                {
                    state = sapi.WorldManager.SaveGame.GetData<BossHuntWorldState>(SaveKey, new BossHuntWorldState());

                    try
                    {
                        sapi.WorldManager.SaveGame.StoreData(SaveKey, BossHuntWorldStateDto.FromDomain(state));
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[BossHuntSystem.State] Failed to migrate legacy BossHunt state: {0}", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[BossHuntSystem.State] Failed to load BossHunt state: {0}", ex.Message);
                    state = new BossHuntWorldState();
                }
            }

            state.entries ??= new List<BossHuntStateEntry>();
            RebuildStateEntriesCache();
        }

        private void OnWorldSave()
        {
            SaveStateIfDirty();
        }

        private void SaveStateIfDirty()
        {
            if (!stateDirty) return;
            if (sapi == null) return;

            _stateLock.EnterReadLock();
            try
            {
                sapi.WorldManager.SaveGame.StoreData(SaveKey, BossHuntWorldStateDto.FromDomain(state));
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem.State] Failed to save BossHunt state: {0}", ex.Message);
            }
            finally
            {
                _stateLock.ExitReadLock();
            }

            stateDirty = false;
        }

        private BossHuntStateEntry GetOrCreateState(string bossKey)
        {
            _stateLock.EnterUpgradeableReadLock();
            try
            {
                if (state == null)
                {
                    _stateLock.EnterWriteLock();
                    try
                    {
                        if (state == null)
                        {
                            state = new BossHuntWorldState();
                        }
                    }
                    finally
                    {
                        _stateLock.ExitWriteLock();
                    }
                }

                if (state.entries == null)
                {
                    _stateLock.EnterWriteLock();
                    try
                    {
                        if (state.entries == null)
                        {
                            state.entries = new List<BossHuntStateEntry>();
                        }
                    }
                    finally
                    {
                        _stateLock.ExitWriteLock();
                    }
                }

                if (stateEntriesCache.TryGetValue(bossKey, out var cachedEntry) && cachedEntry != null)
                {
                    return cachedEntry;
                }

                _stateLock.EnterWriteLock();
                try
                {
                    var created = new BossHuntStateEntry
                    {
                        bossKey = bossKey,
                        currentPointIndex = 0,
                        nextRelocateAtTotalHours = 0,
                        deadUntilTotalHours = 0,
                        anchorPoints = new List<BossHuntAnchorPoint>()
                    };

                    state.entries.Add(created);
                    stateEntriesCache[bossKey] = created;
                    stateDirty = true;
                    return created;
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }
            }
            finally
            {
                _stateLock.ExitUpgradeableReadLock();
            }
        }

        private void RebuildStateEntriesCache()
        {
            _stateLock.EnterWriteLock();
            try
            {
                stateEntriesCache.Clear();
                if (state?.entries == null) return;

                foreach (var entry in state.entries)
                {
                    if (entry?.bossKey != null)
                    {
                        stateEntriesCache[entry.bossKey] = entry;
                    }
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void NormalizeState(BossHuntConfig cfg, BossHuntStateEntry st)
        {
            if (cfg == null || st == null) return;

            st.anchorPoints ??= new List<BossHuntAnchorPoint>();

            int count = GetPointCount(cfg, st);
            if (count <= 0)
            {
                st.currentPointIndex = 0;
                return;
            }

            if (st.currentPointIndex < 0 || st.currentPointIndex >= count)
            {
                st.currentPointIndex = 0;
                _stateLock.EnterWriteLock();
                try
                {
                    stateDirty = true;
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }
            }
        }

        private BossHuntConfig FindConfig(string bossKey)
        {
            if (configs == null) return null;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;
                if (string.Equals(cfg.bossKey, bossKey, StringComparison.OrdinalIgnoreCase)) return cfg;
            }

            return null;
        }

        private BossHuntConfig GetActiveBossConfig(double nowHours)
        {
            if (configs == null || configs.Count == 0) return null;

            state ??= new BossHuntWorldState();
            state.entries ??= new List<BossHuntStateEntry>();

            if (string.IsNullOrWhiteSpace(state.activeBossKey) || nowHours >= state.nextBossRotationTotalHours)
            {
                if (!TryRotateBoss(nowHours)) return null;
            }

            return FindConfig(state.activeBossKey);
        }

        private bool TryRotateBoss(double nowHours)
        {
            string previousQuestId = null;
            BossHuntConfig previousCfg = null;
            if (!string.IsNullOrWhiteSpace(state.activeBossKey))
            {
                previousCfg = FindConfig(state.activeBossKey);
                previousQuestId = previousCfg?.questId;
            }

            if (ShouldPostponeRotation(previousCfg, nowHours))
            {
                return true;
            }

            var nextCfg = SelectNextBossConfig();
            if (nextCfg == null) return false;

            ActivateBossConfig(nextCfg, previousCfg);
            HandleQuestRotation(previousQuestId, nextCfg.questId);

            return true;
        }

        private bool ShouldPostponeRotation(BossHuntConfig previousCfg, double nowHours)
        {
            if (previousCfg == null || nowHours < state.nextBossRotationTotalHours) return false;

            try
            {
                var bossEntity = entityTracker?.GetTrackedEntity(previousCfg.bossKey);
                if (bossEntity == null || !bossEntity.Alive) return false;

                double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
                double lockHours = previousCfg.GetNoRelocateAfterDamageHours();

                bool shouldPostpone = !double.IsNaN(lastDamage) && lockHours > 0 && nowHours - lastDamage < lockHours;
                if (shouldPostpone)
                {
                    state.nextBossRotationTotalHours = nowHours + lockHours;
                    stateDirty = true;
                    ScheduleRotation(state.nextBossRotationTotalHours);
                    return true;
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Debug("[BossHuntSystem.State] Rotation postponement check failed: {0}", ex.Message);
            }

            return false;
        }

        private BossHuntConfig SelectNextBossConfig()
        {
            var ordered = new List<BossHuntConfig>();
            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || !cfg.IsValid()) continue;
                if (!HasRegisteredAnchorsForBoss(cfg.bossKey)) continue;
                ordered.Add(cfg);
            }

            if (ordered.Count == 0) return null;

            ordered.Sort((a, b) => string.Compare(a.bossKey, b.bossKey, StringComparison.OrdinalIgnoreCase));

            int nextIndex = 0;
            if (!string.IsNullOrWhiteSpace(state.activeBossKey))
            {
                int currentIndex = ordered.FindIndex(c => string.Equals(c.bossKey, state.activeBossKey, StringComparison.OrdinalIgnoreCase));
                if (currentIndex >= 0)
                {
                    nextIndex = (currentIndex + 1) % ordered.Count;
                }
            }

            return ordered[nextIndex];
        }

        private void ActivateBossConfig(BossHuntConfig nextCfg, BossHuntConfig previousCfg)
        {
            state.activeBossKey = nextCfg.bossKey;

            if (previousCfg != null && !string.Equals(previousCfg.bossKey, nextCfg.bossKey, StringComparison.OrdinalIgnoreCase))
            {
                TryDespawnBossEntity(previousCfg);
            }

            if (sapi?.World?.Calendar == null) return;
            double nowHours = sapi.World.Calendar.TotalHours;
            double rotationDays = nextCfg.rotationDays > 0 ? nextCfg.rotationDays : 7;
            state.nextBossRotationTotalHours = nowHours + rotationDays * 24.0;
            stateDirty = true;
            ScheduleRotation(state.nextBossRotationTotalHours);
        }

        private void HandleQuestRotation(string previousQuestId, string nextQuestId)
        {
            if (string.IsNullOrWhiteSpace(previousQuestId) || string.IsNullOrWhiteSpace(nextQuestId)) return;
            if (string.Equals(previousQuestId, nextQuestId, StringComparison.OrdinalIgnoreCase)) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            ClearQuestCooldownsForAllPlayers(previousQuestId);
            ResetOutdatedQuestsAndBroadcast(questSystem);
        }

        private void ClearQuestCooldownsForAllPlayers(string questId)
        {
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is not IServerPlayer serverPlayer) continue;
                QuestSystemAdminUtils.ClearQuestCooldownForPlayer(serverPlayer, questId);
            }
        }

        private void ResetOutdatedQuestsAndBroadcast(QuestSystem questSystem)
        {
            bool anyReset = false;
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is not IServerPlayer serverPlayer) continue;

                if (QuestSystemAdminUtils.ForgetOutdatedQuestsForPlayer(questSystem, serverPlayer, sapi) > 0)
                {
                    anyReset = true;
                }
            }

            if (anyReset)
            {
                string chatMsg = Lang.Get("alegacyvsquest:bosshunt-rotation-reset-chat");
                string discordMsg = LocalizationUtils.GetSafe("alegacyvsquest:bosshunt-rotation-discord");
                GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                    sapi, chatMsg, discordMsg,
                    EnumChatType.Notification, DiscordBroadcastKind.BossHuntEvent);
            }
        }
    }
}
