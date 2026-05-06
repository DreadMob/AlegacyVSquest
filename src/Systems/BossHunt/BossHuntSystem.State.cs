using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        private void LoadState()
        {
            try
            {
                state = sapi.WorldManager.SaveGame.GetData<BossHuntWorldState>(SaveKey, new BossHuntWorldState());
            }
            catch
            {
                state = new BossHuntWorldState();
            }

            if (state.entries == null) state.entries = new List<BossHuntStateEntry>();
        }

        private void OnWorldSave()
        {
            SaveStateIfDirty();
        }

        private void SaveStateIfDirty()
        {
            if (!stateDirty) return;
            if (sapi == null) return;

            try
            {
                sapi.WorldManager.SaveGame.StoreData(SaveKey, state);
            }
            catch
            {
            }

            stateDirty = false;
        }

        private BossHuntStateEntry GetOrCreateState(string bossKey)
        {
            if (state == null)
            {
                state = new BossHuntWorldState();
            }

            if (state.entries == null)
            {
                state.entries = new List<BossHuntStateEntry>();
            }

            for (int i = 0; i < state.entries.Count; i++)
            {
                var e = state.entries[i];
                if (e != null && string.Equals(e.bossKey, bossKey, StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }

            var created = new BossHuntStateEntry
            {
                bossKey = bossKey,
                currentPointIndex = 0,
                nextRelocateAtTotalHours = 0,
                deadUntilTotalHours = 0,
                anchorPoints = new List<BossHuntAnchorPoint>()
            };

            state.entries.Add(created);
            stateDirty = true;
            return created;
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
                stateDirty = true;
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

            if (state == null) state = new BossHuntWorldState();
            if (state.entries == null) state.entries = new List<BossHuntStateEntry>();

            if (string.IsNullOrWhiteSpace(state.activeBossKey) || nowHours >= state.nextBossRotationTotalHours)
            {
                string previousQuestId = null;
                BossHuntConfig previousCfg = null;
                if (!string.IsNullOrWhiteSpace(state.activeBossKey))
                {
                    previousCfg = FindConfig(state.activeBossKey);
                    previousQuestId = previousCfg?.questId;
                }

                // If the current boss is alive and was damaged recently, postpone rotation.
                // Otherwise the boss can disappear mid-fight or coexist with the next boss.
                if (previousCfg != null && nowHours >= state.nextBossRotationTotalHours)
                {
                    try
                    {
                        var bossEntity = entityTracker?.GetTrackedEntity(previousCfg.bossKey);
                        if (bossEntity != null && bossEntity.Alive)
                        {
                            double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
                            double lockHours = previousCfg.GetNoRelocateAfterDamageHours();

                            bool shouldPostpone = !double.IsNaN(lastDamage) && lockHours > 0 && nowHours - lastDamage < lockHours;
                            if (shouldPostpone)
                            {
                                state.nextBossRotationTotalHours = nowHours + lockHours;
                                stateDirty = true;
                                return previousCfg;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

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

                var nextCfg = ordered[nextIndex];
                state.activeBossKey = nextCfg.bossKey;

                if (previousCfg != null
                    && !string.Equals(previousCfg.bossKey, nextCfg.bossKey, StringComparison.OrdinalIgnoreCase))
                {
                    TryDespawnBossOnRotation(previousCfg, nowHours);
                }

                double rotationDays = nextCfg.rotationDays > 0 ? nextCfg.rotationDays : 7;
                state.nextBossRotationTotalHours = nowHours + rotationDays * 24.0;
                stateDirty = true;

                if (!string.IsNullOrWhiteSpace(previousQuestId)
                    && !string.Equals(previousQuestId, nextCfg.questId, StringComparison.OrdinalIgnoreCase))
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem != null)
                    {
                        foreach (var player in sapi.World.AllOnlinePlayers)
                        {
                            if (player is not IServerPlayer serverPlayer) continue;
                            QuestSystemAdminUtils.ClearQuestCooldownForPlayer(serverPlayer, previousQuestId);
                        }

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
                            GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, Lang.Get("alegacyvsquest:bosshunt-rotation-reset-chat"), EnumChatType.Notification);
                        }
                    }
                }
            }

            return FindConfig(state.activeBossKey);
        }
    }
}
