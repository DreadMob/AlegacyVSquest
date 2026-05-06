using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        private bool HasAnyRegisteredAnchors()
        {
            if (state?.entries == null || state.entries.Count == 0) return false;

            for (int i = 0; i < state.entries.Count; i++)
            {
                var e = state.entries[i];
                if (e?.anchorPoints != null && e.anchorPoints.Count > 0) return true;
            }

            return false;
        }

        private bool HasRegisteredAnchorsForBoss(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return false;
            if (state?.entries == null || state.entries.Count == 0) return false;

            for (int i = 0; i < state.entries.Count; i++)
            {
                var e = state.entries[i];
                if (e == null) continue;
                if (!string.Equals(e.bossKey, bossKey, StringComparison.OrdinalIgnoreCase)) continue;
                return e.anchorPoints != null && e.anchorPoints.Count > 0;
            }

            return false;
        }

        public string[] GetKnownBossKeys()
        {
            if (configs == null || configs.Count == 0) return Array.Empty<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;
                if (string.IsNullOrWhiteSpace(cfg.bossKey)) continue;
                set.Add(cfg.bossKey);
            }

            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        public void SetAnchorPoint(string bossKey, string anchorId, int pointOrder, BlockPos pos, float leashRange, float outOfCombatLeashRange, float yOffset)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            if (pos == null) return;
            if (string.IsNullOrWhiteSpace(anchorId)) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            bossKey = cfg.bossKey;

            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            st.anchorPoints ??= new List<BossHuntAnchorPoint>();

            BossHuntAnchorPoint existing = null;
            for (int i = 0; i < st.anchorPoints.Count; i++)
            {
                var ap = st.anchorPoints[i];
                if (ap == null) continue;
                if (string.Equals(ap.anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    existing = ap;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new BossHuntAnchorPoint();
                st.anchorPoints.Add(existing);
            }

            existing.anchorId = anchorId;
            existing.order = pointOrder;
            existing.x = pos.X;
            existing.y = pos.Y;
            existing.z = pos.Z;
            existing.dim = pos.dimension;
            existing.leashRange = leashRange;
            existing.outOfCombatLeashRange = outOfCombatLeashRange;
            existing.yOffset = yOffset;

            stateDirty = true;
            orderedAnchorsDirty.Add(bossKey);
            DebugLog($"Anchor registered: bossKey={bossKey} id={anchorId} order={pointOrder} pos={pos.X},{pos.Y},{pos.Z} dim={pos.dimension}", force: true);
        }

        public void UnsetAnchorPoint(string bossKey, string anchorId, BlockPos pos)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            if (pos == null) return;
            if (string.IsNullOrWhiteSpace(anchorId)) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            bossKey = cfg.bossKey;

            var st = GetOrCreateState(bossKey);
            if (st.anchorPoints == null || st.anchorPoints.Count == 0) return;

            for (int i = st.anchorPoints.Count - 1; i >= 0; i--)
            {
                var cur = st.anchorPoints[i];
                if (cur == null) continue;

                if (!string.Equals(cur.anchorId, anchorId, StringComparison.OrdinalIgnoreCase)) continue;

                if (cur.x == pos.X && cur.y == pos.Y && cur.z == pos.Z && cur.dim == pos.dimension)
                {
                    st.anchorPoints.RemoveAt(i);
                    stateDirty = true;
                    orderedAnchorsDirty.Add(bossKey);
                    break;
                }
            }
        }

        public bool TryGetActiveBossAnchor(out Vec3d pos, out int dimension, out string anchorId)
        {
            pos = null;
            dimension = 0;
            anchorId = null;

            if (sapi == null) return false;

            var bossKey = GetActiveBossKey();
            if (string.IsNullOrWhiteSpace(bossKey)) return false;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return false;

            var st = GetOrCreateState(cfg.bossKey);
            NormalizeState(cfg, st);

            if (st.anchorPoints == null || st.anchorPoints.Count == 0) return false;

            var ordered = GetOrderedAnchorsCached(st);
            if (ordered == null || ordered.Count == 0) return false;
            if (st.currentPointIndex < 0 || st.currentPointIndex >= ordered.Count) return false;

            var ap = ordered[st.currentPointIndex];
            if (ap == null) return false;

            anchorId = ap.anchorId;
            dimension = ap.dim;
            pos = GetAnchorPointPosition(ap);
            return true;
        }

        private int GetPointCount(BossHuntConfig cfg, BossHuntStateEntry st)
        {
            return st?.anchorPoints?.Count ?? 0;
        }

        private bool TryGetPoint(BossHuntConfig cfg, BossHuntStateEntry st, int index, out Vec3d point, out int dim)
        {
            return TryGetPoint(cfg, st, index, out point, out dim, out _);
        }

        private bool TryGetPoint(BossHuntConfig cfg, BossHuntStateEntry st, int index, out Vec3d point, out int dim, out BossHuntAnchorPoint anchorPoint)
        {
            point = null;
            dim = 0;
            anchorPoint = null;

            if (st?.anchorPoints == null || st.anchorPoints.Count == 0) return false;

            var ordered = GetOrderedAnchorsCached(st);
            if (index < 0 || index >= ordered.Count) return false;

            var ap = ordered[index];
            if (ap == null) return false;

            anchorPoint = ap;
            dim = ap.dim;
            point = GetAnchorPointPosition(ap);
            return true;
        }

        private Vec3d GetAnchorPointPosition(BossHuntAnchorPoint ap)
        {
            if (ap == null) return null;

            double y = ap.y;
            if (ap.yOffset != 0f && sapi?.World?.BlockAccessor != null)
            {
                try
                {
                    int surfaceY = sapi.World.BlockAccessor.GetRainMapHeightAt(ap.x, ap.z);
                    y = surfaceY + ap.yOffset;
                }
                catch
                {
                    y = ap.y + ap.yOffset;
                }
            }

            return new Vec3d(ap.x, y, ap.z);
        }

        private List<BossHuntAnchorPoint> GetOrderedAnchorsCached(BossHuntStateEntry st)
        {
            if (st == null || string.IsNullOrWhiteSpace(st.bossKey) || st.anchorPoints == null || st.anchorPoints.Count == 0)
            {
                return new List<BossHuntAnchorPoint>();
            }

            if (!orderedAnchorsCache.TryGetValue(st.bossKey, out var ordered) || ordered == null || orderedAnchorsDirty.Contains(st.bossKey))
            {
                ordered = GetOrderedAnchors(st.anchorPoints);
                orderedAnchorsCache[st.bossKey] = ordered;
                orderedAnchorsDirty.Remove(st.bossKey);
            }

            return ordered;
        }

        private List<BossHuntAnchorPoint> GetOrderedAnchors(List<BossHuntAnchorPoint> anchors)
        {
            if (anchors == null || anchors.Count == 0) return new List<BossHuntAnchorPoint>();

            var list = new List<BossHuntAnchorPoint>();
            for (int i = 0; i < anchors.Count; i++)
            {
                var ap = anchors[i];
                if (ap == null) continue;
                list.Add(ap);
            }

            list.Sort((a, b) =>
            {
                int c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                return string.Compare(a.anchorId, b.anchorId, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }
    }
}
