using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Core system managing Hollow Trials: rotation, spawn/despawn, respawn timers.
    /// Structured as partial class following BossHunt pattern.
    /// </summary>
    public partial class HollowTrialSystem : ModSystem
    {
        private const string SaveKey = "alegacyvsquest:hollowtrials:state";

        private ICoreServerAPI sapi;
        private HollowTrialCoreConfig coreConfig;
        private bool debug;

        private readonly List<HollowTrialConfig> allConfigs = new();
        private readonly List<HollowTrialConfig> configsByTier1 = new();
        private readonly List<HollowTrialConfig> configsByTier2 = new();
        private readonly List<HollowTrialConfig> configsByTier3 = new();

        private HollowTrialWorldState state;
        private bool stateDirty;
        private readonly ReaderWriterLockSlim _stateLock = new();

        private BossEntityTracker entityTracker;
        private long tickListenerId;
        private TrialShopNetworkHandler shopNetworkHandler;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            ApplyCoreConfig();
            LoadConfigs();
            LoadState();

            entityTracker = new BossEntityTracker(sapi);
            foreach (var cfg in allConfigs)
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.trialKey))
                {
                    entityTracker.RegisterBossKey(cfg.trialKey);
                }
            }
            entityTracker.Start();

            shopNetworkHandler = new TrialShopNetworkHandler();
            shopNetworkHandler.RegisterServer(sapi);

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 60000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.PlayerJoin += TrySpawnForPlayer;
        }

        public override void Dispose()
        {
            if (entityTracker != null)
            {
                entityTracker.Stop();
                entityTracker = null;
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
            }

            _stateLock?.Dispose();
            base.Dispose();
        }

        private void ApplyCoreConfig()
        {
            if (sapi == null) return;

            try
            {
                var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                coreConfig = qs?.CoreConfig?.HollowTrials;
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Failed to get HollowTrials config: {0}", ex.Message);
            }

            coreConfig ??= new HollowTrialCoreConfig();
            debug = coreConfig.Debug;
        }

        private void LoadConfigs()
        {
            allConfigs.Clear();
            configsByTier1.Clear();
            configsByTier2.Clear();
            configsByTier3.Clear();

            foreach (var mod in sapi.ModLoader.Mods)
            {
                try
                {
                    var assets = sapi.Assets.GetMany<HollowTrialConfig>(sapi.Logger, "config/hollowtrials", mod.Info.ModID);
                    if (assets == null) continue;

                    foreach (var asset in assets)
                    {
                        if (asset.Value == null) continue;

                        if (!asset.Value.IsValid())
                        {
                            sapi.Logger.Warning("[HollowTrialSystem] Invalid trial config in mod {0}: trialKey='{1}' questId='{2}' tier={3}",
                                mod.Info.ModID, asset.Value.trialKey, asset.Value.questId, asset.Value.tier);
                            continue;
                        }

                        allConfigs.Add(asset.Value);
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[HollowTrialSystem] Failed to load trial configs from mod {0}: {1}", mod.Info.ModID, ex.Message);
                }
            }

            // Sort into tier buckets
            foreach (var cfg in allConfigs)
            {
                switch (cfg.tier)
                {
                    case 1: configsByTier1.Add(cfg); break;
                    case 2: configsByTier2.Add(cfg); break;
                    case 3: configsByTier3.Add(cfg); break;
                }
            }

            // Sort each tier alphabetically by trialKey for deterministic rotation
            configsByTier1.Sort((a, b) => string.Compare(a.trialKey, b.trialKey, StringComparison.OrdinalIgnoreCase));
            configsByTier2.Sort((a, b) => string.Compare(a.trialKey, b.trialKey, StringComparison.OrdinalIgnoreCase));
            configsByTier3.Sort((a, b) => string.Compare(a.trialKey, b.trialKey, StringComparison.OrdinalIgnoreCase));

            sapi.Logger.Notification("[HollowTrialSystem] Loaded {0} trial configs (T1:{1} T2:{2} T3:{3})",
                allConfigs.Count, configsByTier1.Count, configsByTier2.Count, configsByTier3.Count);
        }

        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (allConfigs.Count == 0) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            // Check rotation
            CheckRotation(nowHours);

            // Check spawn for each active trial
            if (state?.activeTrialKeys != null)
            {
                foreach (var trialKey in state.activeTrialKeys)
                {
                    var cfg = FindConfig(trialKey);
                    if (cfg == null) continue;

                    var entry = GetOrCreateEntry(trialKey);
                    if (entry.deadUntilTotalHours > nowHours) continue;

                    var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
                    if (bossEntity != null && bossEntity.Alive) continue;

                    TrySpawnIfPlayerNearby(cfg, entry, nowHours);
                }
            }

            SaveStateIfDirty();
        }

        private void TrySpawnForPlayer(IServerPlayer byPlayer)
        {
            if (sapi == null || byPlayer?.Entity?.Pos == null) return;
            if (state?.activeTrialKeys == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            foreach (var trialKey in state.activeTrialKeys)
            {
                var cfg = FindConfig(trialKey);
                if (cfg == null) continue;

                var entry = GetOrCreateEntry(trialKey);
                if (entry.deadUntilTotalHours > nowHours) continue;

                var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
                if (bossEntity != null && bossEntity.Alive) continue;

                TrySpawnIfPlayerNearby(cfg, entry, nowHours);
            }
        }

        public HollowTrialConfig FindConfig(string trialKey)
        {
            if (string.IsNullOrWhiteSpace(trialKey)) return null;

            for (int i = 0; i < allConfigs.Count; i++)
            {
                if (string.Equals(allConfigs[i].trialKey, trialKey, StringComparison.OrdinalIgnoreCase))
                    return allConfigs[i];
            }

            return null;
        }

        private void DebugLog(string message)
        {
            if (!debug || sapi == null) return;
            sapi.Logger.Notification("[HollowTrials] " + message);
        }
    }
}
