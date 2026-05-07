using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossHuntArenaSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        private readonly Dictionary<BlockPos, ArenaConfig> arenasByPos = new Dictionary<BlockPos, ArenaConfig>();

        private readonly Dictionary<string, PendingRespawn> pendingRespawnByUid = new Dictionary<string, PendingRespawn>(StringComparer.Ordinal);

        private class ArenaConfig
        {
            public string claimName;
            public float yOffset;
            public bool keepInventory;
        }

        private class PendingRespawn
        {
            public BlockPos arenaPos;
            public float yOffset;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            sapi.Event.PlayerRespawn += OnPlayerRespawn;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.PlayerRespawn -= OnPlayerRespawn;
            }

            base.Dispose();
        }

        public void RegisterArena(BlockPos pos, float yOffset, bool keepInventory)
        {
            if (pos == null) return;

            var p = new BlockPos(pos.X, pos.Y, pos.Z, pos.dimension);
            string claimName = GetClaimNameAtPos(p);
            arenasByPos[p] = new ArenaConfig
            {
                claimName = claimName,
                yOffset = yOffset,
                keepInventory = keepInventory
            };
        }

        public void UnregisterArena(BlockPos pos)
        {
            if (pos == null) return;

            var p = new BlockPos(pos.X, pos.Y, pos.Z, pos.dimension);
            arenasByPos.Remove(p);
        }

        public bool TryHandlePlayerDeath(EntityPlayer player)
        {
            if (sapi == null || player?.Player == null || player.Pos == null) return false;

            string uid = player.Player.PlayerUID;
            if (string.IsNullOrWhiteSpace(uid)) return false;

            if (!TryFindNearestArena(player.Pos, out var arenaPos, out var cfg))
            {
                return false;
            }

            pendingRespawnByUid[uid] = new PendingRespawn
            {
                arenaPos = arenaPos,
                yOffset = cfg.yOffset
            };

            if (cfg.keepInventory)
            {
                VsQuest.Harmony.QuestItemNoDropOnDeathPatch.SetKeepInventoryOnce(uid);
            }

            return true;
        }

        private void OnPlayerRespawn(IServerPlayer player)
        {
            if (sapi == null || player == null) return;

            string uid = player.PlayerUID;
            if (string.IsNullOrWhiteSpace(uid)) return;

            if (!pendingRespawnByUid.TryGetValue(uid, out var pending) || pending == null) return;

            pendingRespawnByUid.Remove(uid);

            sapi.Event.RegisterCallback(_ => TryTeleportAfterRespawn(player, pending, triesLeft: 10), 0);
        }

        private void TryTeleportAfterRespawn(IServerPlayer player, PendingRespawn pending, int triesLeft)
        {
            if (sapi == null || player == null || pending == null) return;

            var epl = player.Entity;
            if (epl == null) return;

            if (!epl.Alive)
            {
                if (triesLeft > 0)
                {
                    sapi.Event.RegisterCallback(_ => TryTeleportAfterRespawn(player, pending, triesLeft - 1), 50);
                }
                return;
            }

            var pos = pending.arenaPos;
            if (pos == null) return;

            double x = pos.X + 0.5;
            double y = pos.Y + 1 + pending.yOffset;
            double z = pos.Z + 0.5;

            var target = new EntityPos
            {
                X = x,
                Y = y,
                Z = z,
                Dimension = pos.dimension,
                Pitch = epl.Pos.Pitch,
                Yaw = epl.Pos.Yaw
            };

            try
            {
                epl.TeleportTo(target, () => { });
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntArenaSystem] Failed to teleport player to arena: {0}", ex.Message);
            }
        }

        private bool TryFindNearestArena(EntityPos playerPos, out BlockPos pos, out ArenaConfig cfg)
        {
            pos = null;
            cfg = null;
            if (playerPos == null) return false;

            BlockPos nearest = null;
            ArenaConfig nearestCfg = null;
            double nearestDistSq = double.MaxValue;
            const double MaxRangeSq = 300.0 * 300.0;

            foreach (var kvp in arenasByPos)
            {
                var p = kvp.Key;
                if (p == null || p.dimension != playerPos.Dimension) continue;

                var c = kvp.Value;
                if (c == null) continue;

                double dx = p.X + 0.5 - playerPos.X;
                double dy = p.Y + 0.5 - playerPos.Y;
                double dz = p.Z + 0.5 - playerPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = p;
                    nearestCfg = c;
                }
            }

            if (nearest == null || nearestDistSq > MaxRangeSq) return false;

            pos = nearest;
            cfg = nearestCfg;
            return true;
        }

        private string GetClaimNameAtPos(BlockPos pos)
        {
            if (sapi == null || pos == null) return null;

            var claimsApi = sapi.World?.Claims;
            if (claimsApi == null) return null;

            var claims = claimsApi.Get(pos);
            if (claims == null || claims.Length == 0) return null;

            for (int i = 0; i < claims.Length; i++)
            {
                var desc = claims[i]?.Description;
                if (!string.IsNullOrWhiteSpace(desc)) return desc;

                var ownerName = claims[i]?.LastKnownOwnerName;
                if (!string.IsNullOrWhiteSpace(ownerName)) return ownerName;
            }

            return null;
        }
    }
}
