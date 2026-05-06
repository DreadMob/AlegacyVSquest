using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        private AlegacyVsQuestConfig.BossHuntCoreConfig coreConfig;

        private readonly HashSet<string> skipBossKeys = new(StringComparer.OrdinalIgnoreCase);

        private ICoreServerAPI sapi;
        private readonly List<BossHuntConfig> configs = new();
        private BossHuntWorldState state;
        private bool stateDirty;

        private long tickListenerId;

        private readonly Dictionary<string, List<BossHuntAnchorPoint>> orderedAnchorsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> orderedAnchorsDirty = new(StringComparer.OrdinalIgnoreCase);

        private BossEntityTracker entityTracker;
        private readonly Dictionary<string, BossCombatStateMachine> combatStateMachines = new(StringComparer.OrdinalIgnoreCase);

        private double nextDebugLogTotalHours;


        private void ApplyCoreConfig()
        {
            if (sapi == null) return;

            AlegacyVsQuestConfig.BossHuntCoreConfig cfg = null;
            try
            {
                var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                cfg = qs?.CoreConfig?.BossHunt;
            }
            catch
            {
                cfg = null;
            }

            if (cfg == null) return;

            coreConfig = cfg;
            debugBossHunt = cfg.Debug;

            softResetIdleHours = cfg.SoftResetIdleHours > 0 ? cfg.SoftResetIdleHours : 1.0;
            softResetAntiSpamHours = cfg.SoftResetAntiSpamHours >= 0 ? cfg.SoftResetAntiSpamHours : 0.25;
            relocatePostponeHours = cfg.RelocatePostponeHours >= 0 ? cfg.RelocatePostponeHours : 0.25;

            debugLogThrottleHours = cfg.DebugLogThrottleHours > 0 ? cfg.DebugLogThrottleHours : 0.02;

            skipBossKeys.Clear();
            if (cfg.SkipBossKeys != null)
            {
                for (int i = 0; i < cfg.SkipBossKeys.Count; i++)
                {
                    var key = cfg.SkipBossKeys[i];
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    skipBossKeys.Add(key);
                }
            }
        }

        private bool IsBossKeySkipped(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return false;
            return skipBossKeys.Contains(bossKey);
        }

        private BossCombatStateMachine GetOrCreateStateMachine(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return null;

            if (!combatStateMachines.TryGetValue(bossKey, out var stateMachine))
            {
                stateMachine = new BossCombatStateMachine();
                stateMachine.SetOutOfCombatThreshold(softResetIdleHours);
                combatStateMachines[bossKey] = stateMachine;
            }

            return stateMachine;
        }


        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (configs == null || configs.Count == 0) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            var activeCfg = GetActiveBossConfig(nowHours);
            if (activeCfg == null) return;

            var cfg = activeCfg;
            if (!cfg.IsValid()) return;

            var bossKey = cfg.bossKey;
            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            EnsureRelocateTimerInitialized(st, cfg, nowHours);

            Entity bossEntity = entityTracker?.GetTrackedEntity(bossKey);
            bool bossAlive = bossEntity != null && bossEntity.Alive;

            // 1) Soft reset out-of-combat bosses
            if (bossAlive && TryProcessSoftReset(cfg, st, ref bossEntity, ref bossAlive, nowHours))
            {
                // soft reset occurred; entity may have been respawned immediately
            }

            // 2) Periodic relocation
            if (nowHours >= st.nextRelocateAtTotalHours)
            {
                ProcessRelocation(cfg, st, ref bossEntity, ref bossAlive, nowHours);
            }

            // 3) Dead cooldown
            if (st.deadUntilTotalHours > nowHours)
            {
                SaveStateIfDirty();
                return;
            }

            // 4) Spawn when player approaches current point
            if (!bossAlive)
            {
                TrySpawnIfPlayerNearby(cfg, st, nowHours);
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
            catch
            {
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
            catch
            {
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
                return;
            }

            int nextIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
            st.currentPointIndex = nextIndex;
            st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
            stateDirty = true;

            if (bossAlive)
            {
                try
                {
                    sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch
                {
                }
            }

            bossEntity = null;
            bossAlive = false;
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

    }
}
