using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

        private HollowTrialWorldState state;
        private bool stateDirty;
        private readonly ReaderWriterLockSlim _stateLock = new();

        private BossEntityTracker entityTracker;
        private long tickListenerId;
        private long armorScanListenerId;
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
            armorScanListenerId = sapi.Event.RegisterGameTickListener(OnArmorScanTick, 1000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.PlayerJoin += TrySpawnForPlayer;
            sapi.Event.PlayerDeath += OnPlayerDeathHandler;
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

                if (armorScanListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(armorScanListenerId);
                    armorScanListenerId = 0;
                }

                sapi.Event.GameWorldSave -= OnWorldSave;
                sapi.Event.OnEntityDeath -= OnEntityDeath;
                sapi.Event.PlayerJoin -= TrySpawnForPlayer;
                sapi.Event.PlayerDeath -= OnPlayerDeathHandler;
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
                            sapi.Logger.Warning("[HollowTrialSystem] Invalid trial config in mod {0}: trialKey='{1}'",
                                mod.Info.ModID, asset.Value.trialKey);
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

            // Sort alphabetically by trialKey for deterministic rotation
            allConfigs.Sort((a, b) => string.Compare(a.trialKey, b.trialKey, StringComparison.OrdinalIgnoreCase));

            sapi.Logger.Notification("[HollowTrialSystem] Loaded {0} trial boss configs (each with up to 3 tiers)", allConfigs.Count);
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
                    if (bossEntity != null && bossEntity.Alive)
                    {
                        // Check soft reset (no damage for softResetIdleHours)
                        TryProcessSoftReset(cfg, entry, bossEntity, nowHours);
                        continue;
                    }

                    // Auto-spawn disabled — boss is summoned via RMB on anchor block
                    // TrySpawnIfPlayerNearby(cfg, entry, nowHours);
                }
            }

            SaveStateIfDirty();
        }

        /// <summary>
        /// If boss has not received damage for softResetIdleHours, despawn and reset combat tracker.
        /// Boss will respawn fresh when player approaches.
        /// </summary>
        private void TryProcessSoftReset(HollowTrialConfig cfg, HollowTrialStateEntry entry, Entity bossEntity, double nowHours)
        {
            if (cfg == null || bossEntity == null || !bossEntity.Alive) return;

            double idleThresholdHours = cfg.GetSoftResetIdleHours(coreConfig);
            if (idleThresholdHours <= 0) return;

            double lastDamageHours = bossEntity.WatchedAttributes.GetDouble(BossHuntSystem.LastBossDamageTotalHoursKey, double.NaN);
            if (double.IsNaN(lastDamageHours) || lastDamageHours <= 0) return;

            if ((nowHours - lastDamageHours) < idleThresholdHours) return;

            DebugLog($"Soft reset for trial '{cfg.trialKey}' (idle for {nowHours - lastDamageHours:0.00}h)");

            try
            {
                sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Soft reset despawn failed for '{0}': {1}", cfg.trialKey, ex.Message);
            }

            // Reset combat tracker so next attempt is clean
            var tracker = GetCombatTracker(cfg.trialKey);
            tracker?.Reset();

            entry.deadUntilTotalHours = 0;
            stateDirty = true;
        }

        private void TrySpawnForPlayer(IServerPlayer byPlayer)
        {
            // Auto-spawn disabled — boss is summoned via RMB on anchor block
        }

        private void NotifyPlayerOfActiveModifier(IServerPlayer player)
        {
            if (player == null || state == null) return;

            var modType = (TrialModifierType)state.activeModifier;
            if (modType == TrialModifierType.None) return;

            string modName = LocalizationUtils.GetSafe(TrialWeeklyModifierUtils.GetNameKey(modType));
            string msg = LocalizationUtils.GetSafe("albase:trial-modifier-active", modName);
            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
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
