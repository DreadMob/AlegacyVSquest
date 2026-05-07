using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            ApplyCoreConfig();

            entityTracker = new BossEntityTracker(sapi);
            entityTracker.OnDuplicateBossDetected += OnDuplicateBossDetected;

            LoadConfigs();
            LoadState();
            InitializeSchedulesAfterLoad();

            entityTracker.Start();

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 60000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.PlayerJoin += TrySpawnForPlayer;
            sapi.Event.PlayerRespawn += TrySpawnForPlayer;
        }

        public override void Dispose()
        {
            if (entityTracker != null)
            {
                entityTracker.OnDuplicateBossDetected -= OnDuplicateBossDetected;
                entityTracker.Stop();
                entityTracker = null;
            }
            combatStateMachines.Clear();
            CancelAllScheduledCallbacks();

            if (_stateLock != null)
            {
                _stateLock.Dispose();
            }

            if (sapi != null)
            {
                if (tickListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(tickListenerId);
                    tickListenerId = 0;
                }

                sapi.Event.GameWorldSave -= OnWorldSave;
                sapi.Event.OnEntityDeath -= OnEntityDeath;
                sapi.Event.PlayerJoin -= TrySpawnForPlayer;
                sapi.Event.PlayerRespawn -= TrySpawnForPlayer;
            }

            base.Dispose();
        }

        private void LoadConfigs()
        {
            allConfigs.Clear();


            foreach (var mod in sapi.ModLoader.Mods)
            {
                try
                {
                    var assets = sapi.Assets.GetMany<BossHuntConfig>(sapi.Logger, "config/bosshunt", mod.Info.ModID);
                    int assetCount = 0;
                    if (assets != null)
                    {
                        var assetList = assets.ToList();
                        assetCount = assetList.Count;
                        // Load boss configs from this mod
                        
                        foreach (var asset in assetList)
                        {
                            if (asset.Value != null)
                            {
                                // Load boss config
                                allConfigs.Add(asset.Value);
                            }
                        }
                    }
                    else
                    {
                        // No boss configs found in this mod
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[BossHuntSystem.Lifecycle] Failed to load BossHunt configs from mod {0}: {1}", mod.Info.ModID, ex.Message);
                }
            }


            // Auto-register all boss keys from loaded configs
            foreach (var cfg in allConfigs)
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.bossKey))
                {
                    RegisterBossHunt(cfg.bossKey);
                }
            }

            RebuildConfigs();
        }

        private void InitializeSchedulesAfterLoad()
        {
            if (sapi == null || state == null) return;
            if (sapi.World == null || sapi.World.Calendar == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            // Schedule rotation
            if (state.nextBossRotationTotalHours > nowHours)
            {
                ScheduleRotation(state.nextBossRotationTotalHours);
            }
            else
            {
                // Rotation overdue, schedule immediate check
                ScheduleRotation(nowHours);
            }

            // Schedule per-boss callbacks
            if (state.entries != null)
            {
                foreach (var entry in state.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.bossKey)) continue;
                    var cfg = FindConfig(entry.bossKey);
                    if (cfg == null || !cfg.IsValid()) continue;

                    if (entry.nextRelocateAtTotalHours > nowHours)
                    {
                        ScheduleRelocate(entry.bossKey, entry.nextRelocateAtTotalHours);
                    }

                    if (entry.deadUntilTotalHours > nowHours)
                    {
                        ScheduleDeadCooldown(entry.bossKey, entry.deadUntilTotalHours);
                    }
                    else
                    {
                        // Boss may be alive or should be spawned; schedule soft reset if alive
                        var bossEntity = entityTracker?.GetTrackedEntity(entry.bossKey);
                        if (bossEntity != null && bossEntity.Alive)
                        {
                            double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
                            if (!double.IsNaN(lastDamage))
                            {
                                ScheduleSoftReset(entry.bossKey, lastDamage + softResetIdleHours);
                            }
                            else
                            {
                                ScheduleSoftReset(entry.bossKey, nowHours + softResetIdleHours);
                            }
                        }
                    }
                }
            }
        }

        private void TrySpawnForPlayer(IServerPlayer byPlayer)
        {
            if (sapi == null || byPlayer?.Entity?.Pos == null) return;
            if (configs == null || configs.Count == 0) return;
            if (sapi.World == null || sapi.World.Calendar == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;
            var activeCfg = GetActiveBossConfig(nowHours);
            if (activeCfg == null || !activeCfg.IsValid()) return;

            var st = GetOrCreateState(activeCfg.bossKey);
            NormalizeState(activeCfg, st);

            Entity bossEntity = entityTracker?.GetTrackedEntity(activeCfg.bossKey);
            if (bossEntity != null && bossEntity.Alive) return;
            if (st.deadUntilTotalHours > nowHours) return;

            TrySpawnIfPlayerNearby(activeCfg, st, nowHours);
        }

        private void OnDuplicateBossDetected(string bossKey)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(bossKey)) return;
            EnforceSingleLiveBoss(bossKey);
        }
    }
}
