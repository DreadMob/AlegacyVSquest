using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        /// <summary>
        /// Reload anchor points by scanning loaded chunks around online players.
        /// </summary>
        /// <param name="radiusBlocks">Radius in blocks to scan around each player.</param>
        /// <param name="clearedAnchors">Number of anchors cleared from state.</param>
        /// <param name="reRegisteredAnchors">Number of anchors re-registered.</param>
        /// <param name="details">Detailed information about the operation.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool TryReloadAnchors(int radiusBlocks, out int clearedAnchors, out int reRegisteredAnchors, out string details)
        {
            clearedAnchors = 0;
            reRegisteredAnchors = 0;
            details = null;

            if (sapi == null)
            {
                details = "Server API not available.";
                return false;
            }

            if (radiusBlocks <= 0) radiusBlocks = 512;

            try
            {
                // Clear saved anchors in world state
                if (state?.entries != null)
                {
                    for (int i = 0; i < state.entries.Count; i++)
                    {
                        var e = state.entries[i];
                        if (e?.anchorPoints == null) continue;
                        clearedAnchors += e.anchorPoints.Count;
                        e.anchorPoints.Clear();
                    }
                }

                stateDirty = true;

                // Reset caches
                orderedAnchorsCache.Clear();
                orderedAnchorsDirty.Clear();

                // Reset entity tracker - next tick should re-evaluate
                entityTracker?.ForceScan();

                // Re-register anchors from already loaded chunks by scanning around online players
                int scannedChunks = 0;
                int anchorsFound = 0;

                var blockAccessor = sapi.World?.BlockAccessor;
                if (blockAccessor == null)
                {
                    details = "World.BlockAccessor not available.";
                    return false;
                }

                int chunksize = GlobalConstants.ChunkSize;
                foreach (var p in sapi.World.AllOnlinePlayers)
                {
                    if (p?.Entity == null) continue;

                    var pos = p.Entity.Pos;
                    var center = new Vec3i((int)pos.X, (int)pos.Y, (int)pos.Z);

                    for (int x = center.X - radiusBlocks; x <= center.X + radiusBlocks; x += chunksize)
                    {
                        for (int y = center.Y - radiusBlocks; y <= center.Y + radiusBlocks; y += chunksize)
                        {
                            for (int z = center.Z - radiusBlocks; z <= center.Z + radiusBlocks; z += chunksize)
                            {
                                var chunk = blockAccessor.GetChunkAtBlockPos(new BlockPos(x, y, z, 0));
                                if (chunk == null) continue;
                                scannedChunks++;

                                foreach (var be in chunk.BlockEntities.Values)
                                {
                                    if (be is not BlockEntityBossHuntAnchor anchorBe) continue;
                                    anchorsFound++;
                                    if (anchorBe.TryForceRegisterAnchorServerSide())
                                    {
                                        reRegisteredAnchors++;
                                    }
                                }
                            }
                        }
                    }
                }

                SaveStateIfDirty();

                details = $"Scanned chunks: {scannedChunks}, anchors in loaded chunks: {anchorsFound}.";
                return true;
            }
            catch (Exception e)
            {
                details = e.Message;
                return false;
            }
        }

        /// <summary>
        /// Get the current position of the active boss.
        /// </summary>
        /// <param name="bossKey">The boss key to look up.</param>
        /// <param name="pos">The position of the boss.</param>
        /// <param name="dimension">The dimension of the boss.</param>
        /// <param name="isLiveEntity">True if the boss is a live entity, false if it's a spawn point.</param>
        /// <returns>True if the boss was found, false otherwise.</returns>
        public bool TryGetBossPosition(string bossKey, out Vec3d pos, out int dimension, out bool isLiveEntity)
        {
            pos = null;
            dimension = 0;
            isLiveEntity = false;

            if (sapi == null) return false;
            if (string.IsNullOrWhiteSpace(bossKey)) return false;

            if (!string.Equals(GetActiveBossKey(), bossKey, StringComparison.OrdinalIgnoreCase)) return false;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return false;

            var st = GetOrCreateState(cfg.bossKey);
            NormalizeState(cfg, st);

            var bossEntity = entityTracker?.GetTrackedEntity(cfg.bossKey);
            if (bossEntity != null && bossEntity.Alive)
            {
                pos = new Vec3d(bossEntity.Pos.X, bossEntity.Pos.Y, bossEntity.Pos.Z);
                dimension = bossEntity.Pos.Dimension;
                isLiveEntity = true;
                return true;
            }

            if (!TryGetPoint(cfg, st, st.currentPointIndex, out var p, out int dim)) return false;
            pos = p;
            dimension = dim;
            isLiveEntity = false;
            return true;
        }

        /// <summary>
        /// Get the currently active boss key.
        /// </summary>
        /// <returns>The active boss key, or null if none.</returns>
        public string GetActiveBossKey()
        {
            return state?.activeBossKey;
        }

        /// <summary>
        /// Get the quest ID for the currently active boss.
        /// </summary>
        /// <returns>The quest ID, or null if no active boss or quest.</returns>
        public string GetActiveBossQuestId()
        {
            if (!HasAnyRegisteredAnchors()) return null;

            var activeBossKey = GetActiveBossKey();
            if (!HasRegisteredAnchorsForBoss(activeBossKey)) return null;

            var cfg = FindConfig(activeBossKey);
            return cfg?.questId;
        }

        /// <summary>
        /// Force rotation to the next boss immediately.
        /// </summary>
        /// <param name="bossKey">The boss key of the new active boss.</param>
        /// <param name="questId">The quest ID of the new active boss.</param>
        /// <returns>True if rotation succeeded, false otherwise.</returns>
        public bool ForceRotateToNext(out string bossKey, out string questId)
        {
            bossKey = null;
            questId = null;

            if (sapi == null) return false;
            if (!HasAnyRegisteredAnchors()) return false;

            double nowHours = sapi.World.Calendar.TotalHours;

            if (state == null) state = new BossHuntWorldState();
            if (state.entries == null) state.entries = new System.Collections.Generic.List<BossHuntStateEntry>();

            string currentBossKey = state.activeBossKey;
            if (!string.IsNullOrWhiteSpace(currentBossKey))
            {
                var currentCfg = FindConfig(currentBossKey);
                if (currentCfg != null)
                {
                    TryDespawnBossEntity(currentCfg);
                }
            }

            TryReloadAnchors(512, out _, out _, out _);

            state.nextBossRotationTotalHours = nowHours - 0.01;
            stateDirty = true;

            entityTracker?.ForceScan();

            var cfg = GetActiveBossConfig(nowHours);
            if (cfg == null) return false;

            var st = GetOrCreateState(cfg.bossKey);
            st.currentPointIndex = 0;
            st.deadUntilTotalHours = 0;
            st.lastSoftResetAtTotalHours = 0;
            st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
            stateDirty = true;

            bossKey = cfg.bossKey;
            questId = cfg.questId;
            return true;
        }

        /// <summary>
        /// Get the current boss hunt status.
        /// </summary>
        /// <param name="bossKey">The current active boss key.</param>
        /// <param name="questId">The current active quest ID.</param>
        /// <param name="hoursUntilRotation">Hours until the next boss rotation.</param>
        /// <returns>True if status retrieved, false otherwise.</returns>
        public bool TryGetBossHuntStatus(out string bossKey, out string questId, out double hoursUntilRotation)
        {
            bossKey = null;
            questId = null;
            hoursUntilRotation = 0;

            if (sapi == null) return false;
            if (!HasAnyRegisteredAnchors()) return false;

            double nowHours = sapi.World.Calendar.TotalHours;
            var cfg = GetActiveBossConfig(nowHours);
            if (cfg == null) return false;

            bossKey = cfg.bossKey;
            questId = cfg.questId;
            hoursUntilRotation = state?.nextBossRotationTotalHours > nowHours
                ? state.nextBossRotationTotalHours - nowHours
                : 0;
            return true;
        }
    }
}
