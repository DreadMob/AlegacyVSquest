using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public partial class BossHuntSystem : ModSystem
    {
        public const string LastBossDamageTotalHoursKey = "alegacyvsquest:bosshunt:lastBossDamageTotalHours";
        public const string BossSpawnedAtTotalHoursKey = "alegacyvsquest:bosshunt:spawnedAtTotalHours";

        private const string SaveKey = "alegacyvsquest:bosshunt:state";
        private bool debugBossHunt;

        private double softResetIdleHours = 3.0;
        private double softResetAntiSpamHours = 0.25;
        private double relocatePostponeHours = 0.25;
        private double debugLogThrottleHours = 0.02;

        private BossHuntCoreConfig coreConfig;

        private readonly HashSet<string> allowedBossKeys = new(StringComparer.OrdinalIgnoreCase);

        private ICoreServerAPI sapi;
        private readonly List<BossHuntConfig> allConfigs = new();
        private readonly List<BossHuntConfig> configs = new();
        private bool configsDirty;
        private BossHuntWorldState state;
        private bool stateDirty;
        private readonly ReaderWriterLockSlim _stateLock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, BossHuntStateEntry> stateEntriesCache = new(StringComparer.OrdinalIgnoreCase);

        private long tickListenerId;

        private static readonly List<BossHuntAnchorPoint> EmptyAnchorList = new(0);
        private readonly Dictionary<string, List<BossHuntAnchorPoint>> orderedAnchorsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> orderedAnchorsDirty = new(StringComparer.OrdinalIgnoreCase);

        private BossEntityTracker entityTracker;
        private readonly ConcurrentDictionary<string, BossCombatStateMachine> combatStateMachines = new(StringComparer.OrdinalIgnoreCase);

        private double nextDebugLogTotalHours;

        private readonly Dictionary<string, long> scheduledSoftResetCallbacks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> scheduledRelocateCallbacks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> scheduledDeadCooldownCallbacks = new(StringComparer.OrdinalIgnoreCase);
        private long scheduledRotationCallback;

        private void ApplyCoreConfig()
        {
            if (sapi == null) return;

            BossHuntCoreConfig cfg = null;
            try
            {
                var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                cfg = qs?.CoreConfig?.BossHunt;
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem] Failed to get BossHunt config: {0}", ex.Message);
                cfg = null;
            }

            if (cfg == null) return;

            coreConfig = cfg;
            debugBossHunt = cfg.Debug;

            softResetIdleHours = cfg.SoftResetIdleHours > 0 ? cfg.SoftResetIdleHours : 1.0;
            softResetAntiSpamHours = cfg.SoftResetAntiSpamHours >= 0 ? cfg.SoftResetAntiSpamHours : 0.25;
            relocatePostponeHours = cfg.RelocatePostponeHours >= 0 ? cfg.RelocatePostponeHours : 0.25;

            debugLogThrottleHours = cfg.DebugLogThrottleHours > 0 ? cfg.DebugLogThrottleHours : 0.02;
        }

        private bool IsBossKeyAllowed(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return false;
            if (allowedBossKeys.Count == 0) return true;
            return allowedBossKeys.Contains(bossKey);
        }

        /// <summary>
        /// Register a boss key as allowed for boss hunt system.
        /// </summary>
        /// <param name="bossKey">The boss key to register.</param>
        public void RegisterBossHunt(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            if (allowedBossKeys.Add(bossKey))
            {
                configsDirty = true;
            }
        }

        private void RebuildConfigs()
        {
            configs.Clear();
            if (allConfigs == null) return;

            for (int i = 0; i < allConfigs.Count; i++)
            {
                var cfg = allConfigs[i];
                if (cfg == null) continue;
                if (!IsBossKeyAllowed(cfg.bossKey)) continue;
                configs.Add(cfg);
                entityTracker?.RegisterBossKey(cfg.bossKey);
            }

            configsDirty = false;
        }

        private BossCombatStateMachine GetOrCreateStateMachine(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return null;

            return combatStateMachines.GetOrAdd(bossKey, key =>
            {
                var sm = new BossCombatStateMachine();
                sm.SetOutOfCombatThreshold(softResetIdleHours);
                return sm;
            });
        }


        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (configsDirty) RebuildConfigs();
            if (configs == null || configs.Count == 0) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            var activeCfg = GetActiveBossConfig(nowHours);
            if (activeCfg != null && activeCfg.IsValid())
            {
                var bossKey = activeCfg.bossKey;
                var st = GetOrCreateState(bossKey);
                NormalizeState(activeCfg, st);

                Entity bossEntity = entityTracker?.GetTrackedEntity(bossKey);
                bool bossAlive = bossEntity != null && bossEntity.Alive;

                // Fallback spawn check: if dead cooldown passed and no entity, check for nearby players
                if (!bossAlive && st.deadUntilTotalHours <= nowHours)
                {
                    TrySpawnIfPlayerNearby(activeCfg, st, nowHours);
                }
            }

            SaveStateIfDirty();
        }

        private void EnsureRelocateTimerInitialized(BossHuntStateEntry st, BossHuntConfig cfg, double nowHours)
        {
            if (st.nextRelocateAtTotalHours <= 0)
            {
                st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
                stateDirty = true;
            }
        }

        /// <summary>
        /// Checks combat timeout via BossCombatStateMachine and performs soft reset if needed.
        /// Returns true if a soft reset was attempted.
        /// </summary>
        private bool TryProcessSoftReset(BossHuntConfig cfg, BossHuntStateEntry st, ref Entity bossEntity, ref bool bossAlive, double nowHours)
        {
            var stateMachine = GetOrCreateStateMachine(cfg.bossKey);

            try
            {
                double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
                if (!double.IsNaN(lastDamage) && lastDamage > stateMachine.LastDamageTimeHours)
                {
                    stateMachine.OnDamageReceived(lastDamage);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem] Failed to read boss damage from WatchedAttributes: {0}", ex.Message);
            }

            if (!stateMachine.OnCombatTimeout(nowHours))
                return false;

            // Boss transitioned from InCombat to OutOfCombat - despawn for soft reset
            bool antiSpamOk = st.lastSoftResetAtTotalHours <= 0 || (nowHours - st.lastSoftResetAtTotalHours >= softResetAntiSpamHours);
            if (!antiSpamOk)
                return false;

            st.lastSoftResetAtTotalHours = nowHours;
            st.deadUntilTotalHours = 0;
            stateDirty = true;

            try
            {
                sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem] Failed to despawn boss during soft reset: {0}", ex.Message);
            }

            stateMachine.OnDespawn();
            bossEntity = null;
            bossAlive = false;

            // If there is any player nearby, spawn immediately to avoid a visible "missing boss".
            if (TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim, out var anchorPoint)
                && AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange(coreConfig)))
            {
                TrySpawnBoss(cfg, point, pointDim, anchorPoint);
            }

            return true;
        }

        private void ProcessRelocation(BossHuntConfig cfg, BossHuntStateEntry st, ref Entity bossEntity, ref bool bossAlive, double nowHours)
        {
            if (bossAlive && !IsSafeToRelocate(cfg, bossEntity, nowHours))
            {
                st.nextRelocateAtTotalHours = nowHours + relocatePostponeHours;
                stateDirty = true;
                ScheduleRelocate(cfg.bossKey, st.nextRelocateAtTotalHours);
                return;
            }

            int nextIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
            st.currentPointIndex = nextIndex;
            st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
            stateDirty = true;
            ScheduleRelocate(cfg.bossKey, st.nextRelocateAtTotalHours);

            if (bossAlive)
            {
                try
                {
                    sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[BossHuntSystem] Failed to despawn boss during relocation: {0}", ex.Message);
                }
            }

            bossEntity = null;
            bossAlive = false;

            // Broadcast relocation message
            try
            {
                string bossName = GetBossDisplayNameFromConfig(cfg);
                string chatMsg = LocalizationUtils.GetSafe("alegacyvsquest:bosshunt-relocate-chat", bossName);
                string discordMsg = LocalizationUtils.GetSafe("alegacyvsquest:bosshunt-relocate-discord", bossName);
                if (!string.IsNullOrWhiteSpace(chatMsg))
                {
                    GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                        sapi, chatMsg, discordMsg,
                        EnumChatType.Notification, DiscordBroadcastKind.BossHuntEvent);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Debug("[BossHuntSystem] Relocation broadcast failed: {0}", ex.Message);
            }
        }

        private string GetBossDisplayNameFromConfig(BossHuntConfig cfg)
        {
            if (cfg == null) return "?";
            string entityCode = cfg.GetBossEntityCode();
            if (string.IsNullOrWhiteSpace(entityCode)) return cfg.bossKey ?? "?";
            return MobLocalizationUtils.GetMobDisplayName(entityCode) ?? cfg.bossKey ?? "?";
        }

        private void TrySpawnIfPlayerNearby(BossHuntConfig cfg, BossHuntStateEntry st, double nowHours)
        {
            if (!TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim, out var anchorPoint))
                return;

            if (!AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange(coreConfig)))
                return;

            DebugLog($"Spawn attempt: bossKey={cfg.bossKey} point={point.X:0.0},{point.Y:0.0},{point.Z:0.0} dim={pointDim} anchors={st.anchorPoints?.Count ?? 0} deadUntil={st.deadUntilTotalHours:0.00} now={nowHours:0.00}");
            TrySpawnBoss(cfg, point, pointDim, anchorPoint);
        }

        // ============= SCHEDULE-DRIVEN CALLBACKS =============

        private void CancelCallback(ref long callbackId)
        {
            if (callbackId != 0 && sapi != null)
            {
                sapi.Event.UnregisterCallback(callbackId);
            }
            callbackId = 0;
        }

        private void CancelScheduledCallbacks(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            _stateLock.EnterWriteLock();
            try
            {
                if (scheduledSoftResetCallbacks.TryGetValue(bossKey, out var softId))
                {
                    CancelCallback(ref softId);
                    scheduledSoftResetCallbacks.Remove(bossKey);
                }
                if (scheduledRelocateCallbacks.TryGetValue(bossKey, out var relocId))
                {
                    CancelCallback(ref relocId);
                    scheduledRelocateCallbacks.Remove(bossKey);
                }
                if (scheduledDeadCooldownCallbacks.TryGetValue(bossKey, out var deadId))
                {
                    CancelCallback(ref deadId);
                    scheduledDeadCooldownCallbacks.Remove(bossKey);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void CancelScheduledDeadCooldown(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            _stateLock.EnterWriteLock();
            try
            {
                if (scheduledDeadCooldownCallbacks.TryGetValue(bossKey, out var deadId))
                {
                    CancelCallback(ref deadId);
                    scheduledDeadCooldownCallbacks.Remove(bossKey);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void CancelAllScheduledCallbacks()
        {
            _stateLock.EnterWriteLock();
            try
            {
                foreach (var bossKey in scheduledSoftResetCallbacks.Keys.ToList())
                {
                    if (scheduledSoftResetCallbacks.TryGetValue(bossKey, out var id))
                    {
                        CancelCallback(ref id);
                    }
                    scheduledSoftResetCallbacks.Remove(bossKey);
                }

                foreach (var bossKey in scheduledRelocateCallbacks.Keys.ToList())
                {
                    if (scheduledRelocateCallbacks.TryGetValue(bossKey, out var id))
                    {
                        CancelCallback(ref id);
                    }
                    scheduledRelocateCallbacks.Remove(bossKey);
                }

                foreach (var bossKey in scheduledDeadCooldownCallbacks.Keys.ToList())
                {
                    if (scheduledDeadCooldownCallbacks.TryGetValue(bossKey, out var id))
                    {
                        CancelCallback(ref id);
                    }
                    scheduledDeadCooldownCallbacks.Remove(bossKey);
                }

                CancelCallback(ref scheduledRotationCallback);
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void ScheduleCallback(Dictionary<string, long> dict, string bossKey, double atHours, Action<string> callback)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;

            _stateLock.EnterWriteLock();
            try
            {
                long existing = 0;
                dict.TryGetValue(bossKey, out existing);
                CancelCallback(ref existing);

                double delayHours = atHours - sapi.World.Calendar.TotalHours;
                int delayMs = (int)(delayHours * 3600 * 1000);
                if (delayMs < 0) delayMs = 0;
                dict[bossKey] = sapi.Event.RegisterCallback(_ => callback(bossKey), delayMs);
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void CancelScheduledSoftReset(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            _stateLock.EnterWriteLock();
            try
            {
                if (scheduledSoftResetCallbacks.TryGetValue(bossKey, out var id))
                {
                    CancelCallback(ref id);
                    scheduledSoftResetCallbacks.Remove(bossKey);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        private void ScheduleSoftReset(string bossKey, double atHours)
        {
            ScheduleCallback(scheduledSoftResetCallbacks, bossKey, atHours, OnSoftResetCallback);
        }

        private void OnSoftResetCallback(string bossKey)
        {
            scheduledSoftResetCallbacks.Remove(bossKey);
            if (sapi == null) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            var bossEntity = entityTracker?.GetTrackedEntity(bossKey);
            bool bossAlive = bossEntity != null && bossEntity.Alive;
            double nowHours = sapi.World.Calendar.TotalHours;

            if (bossAlive && TryProcessSoftReset(cfg, st, ref bossEntity, ref bossAlive, nowHours))
            {
                // soft reset occurred
            }
            SaveStateIfDirty();
        }

        private void ScheduleRelocate(string bossKey, double atHours)
        {
            ScheduleCallback(scheduledRelocateCallbacks, bossKey, atHours, OnRelocateCallback);
        }

        private void OnRelocateCallback(string bossKey)
        {
            scheduledRelocateCallbacks.Remove(bossKey);
            if (sapi == null) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            var bossEntity = entityTracker?.GetTrackedEntity(bossKey);
            bool bossAlive = bossEntity != null && bossEntity.Alive;
            double nowHours = sapi.World.Calendar.TotalHours;

            ProcessRelocation(cfg, st, ref bossEntity, ref bossAlive, nowHours);
            SaveStateIfDirty();
        }

        private void ScheduleDeadCooldown(string bossKey, double atHours)
        {
            ScheduleCallback(scheduledDeadCooldownCallbacks, bossKey, atHours, OnDeadCooldownCallback);
        }

        private void OnDeadCooldownCallback(string bossKey)
        {
            scheduledDeadCooldownCallbacks.Remove(bossKey);
            if (sapi == null) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);
            double nowHours = sapi.World.Calendar.TotalHours;

            TrySpawnIfPlayerNearby(cfg, st, nowHours);
            SaveStateIfDirty();
        }

        private void ScheduleRotation(double atHours)
        {
            if (sapi == null) return;
            CancelCallback(ref scheduledRotationCallback);

            double delayHours = atHours - sapi.World.Calendar.TotalHours;
            int delayMs = (int)(delayHours * 3600 * 1000);
            if (delayMs < 0) delayMs = 0;
            scheduledRotationCallback = sapi.Event.RegisterCallback(_ => OnRotationCallback(), delayMs);
        }

        private void OnRotationCallback()
        {
            scheduledRotationCallback = 0;
            if (sapi == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;
            var cfg = GetActiveBossConfig(nowHours);
            if (cfg != null && cfg.IsValid())
            {
                var bossKey = cfg.bossKey;
                var st = GetOrCreateState(bossKey);
                NormalizeState(cfg, st);
                EnsureRelocateTimerInitialized(st, cfg, nowHours);
                ScheduleRelocate(bossKey, st.nextRelocateAtTotalHours);
            }
            if (state?.nextBossRotationTotalHours > nowHours)
            {
                ScheduleRotation(state.nextBossRotationTotalHours);
            }
            SaveStateIfDirty();
        }

        /// <summary>
        /// Called when a boss receives damage. Updates combat state and schedules soft reset.
        /// </summary>
        /// <param name="bossKey">The boss key that was damaged.</param>
        /// <param name="damageTimeHours">The in-game time when damage occurred.</param>
        public void OnBossDamaged(string bossKey, double damageTimeHours)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(bossKey)) return;

            var stateMachine = GetOrCreateStateMachine(bossKey);
            if (stateMachine != null)
            {
                stateMachine.OnDamageReceived(damageTimeHours);
            }
            ScheduleSoftReset(bossKey, damageTimeHours + softResetIdleHours);
        }

        /// <summary>
        /// Called when a boss completes rebirth (phase transition).
        /// Updates combat state and reschedules callbacks.
        /// </summary>
        /// <param name="bossKey">The boss key that completed rebirth.</param>
        /// <param name="newEntity">The new entity for the next phase.</param>
        public void OnBossRebirthComplete(string bossKey, Entity newEntity)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(bossKey) || newEntity == null) return;

            var stateMachine = GetOrCreateStateMachine(bossKey);
            if (stateMachine != null)
            {
                double nowHours = sapi.World.Calendar.TotalHours;
                stateMachine.OnRebirthComplete(nowHours);
            }

            // Force scan so the new phase entity is tracked immediately
            entityTracker?.ForceScan();

            // Schedule next soft reset and relocate for the reborn boss
            double now = sapi.World.Calendar.TotalHours;
            var st = GetOrCreateState(bossKey);
            var cfg = FindConfig(bossKey);
            if (cfg != null)
            {
                EnsureRelocateTimerInitialized(st, cfg, now);
                ScheduleRelocate(bossKey, st.nextRelocateAtTotalHours);
                ScheduleSoftReset(bossKey, now + softResetIdleHours);
                CancelScheduledDeadCooldown(bossKey);
            }
        }

        /// <summary>
        /// Called when a boss respawns via ability.
        /// Resets dead cooldown and reschedules callbacks.
        /// </summary>
        /// <param name="bossKey">The boss key that respawned.</param>
        /// <param name="newEntity">The respawned entity.</param>
        public void OnBossRespawnedByAbility(string bossKey, Entity newEntity)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(bossKey) || newEntity == null) return;

            var st = GetOrCreateState(bossKey);
            var cfg = FindConfig(bossKey);
            double nowHours = sapi.World.Calendar.TotalHours;

            // Reset dead cooldown so BossHuntSystem does not try to spawn another copy
            st.deadUntilTotalHours = 0;
            stateDirty = true;

            var stateMachine = GetOrCreateStateMachine(bossKey);
            stateMachine?.OnSpawn(nowHours);

            entityTracker?.ForceScan();
            CancelScheduledDeadCooldown(bossKey);

            if (cfg != null)
            {
                EnsureRelocateTimerInitialized(st, cfg, nowHours);
                ScheduleRelocate(bossKey, st.nextRelocateAtTotalHours);
                ScheduleSoftReset(bossKey, nowHours + softResetIdleHours);
            }
        }
    }
}
