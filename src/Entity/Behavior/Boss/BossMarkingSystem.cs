using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// System for managing boss marks on players.
    /// Handles marking, unmarking, and mark expiration.
    /// </summary>
    public class BossMarkingSystem
    {
        private readonly ICoreServerAPI sapi;
        private readonly Entity bossEntity;
        private readonly Dictionary<long, MarkData> markedPlayers = new Dictionary<long, MarkData>();

        public BossMarkingSystem(ICoreServerAPI sapi, Entity bossEntity)
        {
            this.sapi = sapi;
            this.bossEntity = bossEntity;
        }

        /// <summary>
        /// Data structure for player marks.
        /// </summary>
        public class MarkData
        {
            public long PlayerId { get; set; }
            public long ExpiryTime { get; set; }
            public string MarkType { get; set; }
            public TreeAttribute CustomData { get; set; }

            public MarkData(long playerId, long expiryTime, string markType = "default")
            {
                PlayerId = playerId;
                ExpiryTime = expiryTime;
                MarkType = markType;
                CustomData = new TreeAttribute();
            }

            public bool IsExpired(long currentTime) => currentTime >= ExpiryTime;
        }

        /// <summary>
        /// Mark a player with specified type and duration.
        /// </summary>
        public void MarkPlayer(EntityPlayer player, string markType, int durationMs, TreeAttribute customData = null)
        {
            if (player == null || sapi == null) return;

            long now = sapi.World.ElapsedMilliseconds;
            long expiryTime = now + durationMs;

            var markData = new MarkData(player.EntityId, expiryTime, markType);
            if (customData != null)
            {
                markData.CustomData = customData;
            }

            markedPlayers[player.EntityId] = markData;

            // Store mark on player for persistence
            try
            {
                string markKey = $"alegacyvsquest:bossmark:{markType}";
                player.WatchedAttributes.SetBool(markKey, true);
                player.WatchedAttributes.SetLong($"{markKey}:expiry", expiryTime);
                player.WatchedAttributes.SetLong($"{markKey}:bossId", bossEntity.EntityId);
                player.WatchedAttributes.MarkPathDirty(markKey);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[vsquest] Exception marking player {player.EntityId}: {ex}");
            }
        }

        /// <summary>
        /// Remove mark from player.
        /// </summary>
        public void UnmarkPlayer(EntityPlayer player, string markType = null)
        {
            if (player == null) return;

            if (string.IsNullOrWhiteSpace(markType))
            {
                // Remove all marks for this player
                markedPlayers.Remove(player.EntityId);
                
                // Clear all boss marks from player
                var keysToRemove = new List<string>();
                foreach (var key in player.WatchedAttributes.Keys)
                {
                    if (key.StartsWith("alegacyvsquest:bossmark:"))
                    {
                        keysToRemove.Add(key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    player.WatchedAttributes.RemoveAttribute(key);
                    player.WatchedAttributes.RemoveAttribute($"{key}:expiry");
                    player.WatchedAttributes.RemoveAttribute($"{key}:bossId");
                }
                
                if (keysToRemove.Count > 0)
                {
                    player.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossmark");
                }
            }
            else
            {
                // Remove specific mark type
                markedPlayers.Remove(player.EntityId);
                
                string markKey = $"alegacyvsquest:bossmark:{markType}";
                player.WatchedAttributes.RemoveAttribute(markKey);
                player.WatchedAttributes.RemoveAttribute($"{markKey}:expiry");
                player.WatchedAttributes.RemoveAttribute($"{markKey}:bossId");
                player.WatchedAttributes.MarkPathDirty(markKey);
            }
        }

        /// <summary>
        /// Check if player is marked with specific type.
        /// </summary>
        public bool IsPlayerMarked(EntityPlayer player, string markType = null)
        {
            if (player == null) return false;

            if (string.IsNullOrWhiteSpace(markType))
            {
                return markedPlayers.ContainsKey(player.EntityId);
            }

            return markedPlayers.TryGetValue(player.EntityId, out var markData) && 
                   markData.MarkType == markType;
        }

        /// <summary>
        /// Get mark data for player.
        /// </summary>
        public MarkData GetPlayerMark(EntityPlayer player, string markType = null)
        {
            if (player == null) return null;

            if (!markedPlayers.TryGetValue(player.EntityId, out var markData)) return null;
            
            if (!string.IsNullOrWhiteSpace(markType) && markData.MarkType != markType) return null;

            return markData;
        }

        /// <summary>
        /// Get all marked players.
        /// </summary>
        public IEnumerable<EntityPlayer> GetMarkedPlayers(string markType = null)
        {
            var result = new List<EntityPlayer>();
            long now = sapi.World.ElapsedMilliseconds;

            foreach (var kvp in markedPlayers)
            {
                if (kvp.Value.IsExpired(now))
                {
                    // Remove expired mark
                    markedPlayers.Remove(kvp.Key);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(markType) && kvp.Value.MarkType != markType) continue;

                var player = sapi.World.GetEntityById(kvp.Key) as EntityPlayer;
                if (player != null && player.Alive)
                {
                    result.Add(player);
                }
            }

            return result;
        }

        /// <summary>
        /// Clean up expired marks and update from player attributes.
        /// </summary>
        public void CleanupExpiredMarks()
        {
            long now = sapi.World.ElapsedMilliseconds;
            var toRemove = new List<long>();

            foreach (var kvp in markedPlayers)
            {
                if (kvp.Value.IsExpired(now))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var playerId in toRemove)
            {
                var player = sapi.World.GetEntityById(playerId) as EntityPlayer;
                if (player != null)
                {
                    UnmarkPlayer(player);
                }
                else
                {
                    markedPlayers.Remove(playerId);
                }
            }
        }

        /// <summary>
        /// Clear all marks.
        /// </summary>
        public void ClearAllMarks()
        {
            var playersToUnmark = new List<EntityPlayer>();
            
            foreach (var kvp in markedPlayers)
            {
                var player = sapi.World.GetEntityById(kvp.Key) as EntityPlayer;
                if (player != null)
                {
                    playersToUnmark.Add(player);
                }
            }

            foreach (var player in playersToUnmark)
            {
                UnmarkPlayer(player);
            }

            markedPlayers.Clear();
        }

        /// <summary>
        /// Spawn particles on marked players.
        /// </summary>
        public void SpawnParticlesOnMarkedPlayers(string markType, int particleCount, int colorRgba, Vec3f velocityMin, Vec3f velocityMax, float size, float gravityEffect, EnumParticleModel model)
        {
            long now = sapi.World.ElapsedMilliseconds;

            foreach (var kvp in markedPlayers)
            {
                if (kvp.Value.IsExpired(now)) continue;
                if (!string.IsNullOrWhiteSpace(markType) && kvp.Value.MarkType != markType) continue;

                var player = sapi.World.GetEntityById(kvp.Key) as EntityPlayer;
                if (player?.Pos == null) continue;

                var targetPos = player.Pos.XYZ;
                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        particleCount, particleCount + 4,
                        colorRgba,
                        targetPos.Add(-0.2, 0.5, -0.2),
                        targetPos.Add(0.2, 1.5, 0.2),
                        velocityMin,
                        velocityMax,
                        size,
                        0f,
                        gravityEffect,
                        gravityEffect,
                        model
                    )
                );
            }
        }

        /// <summary>
        /// Get count of marked players by type.
        /// </summary>
        public int GetMarkedPlayerCount(string markType = null)
        {
            long now = sapi.World.ElapsedMilliseconds;
            int count = 0;

            foreach (var kvp in markedPlayers)
            {
                if (kvp.Value.IsExpired(now)) continue;
                if (!string.IsNullOrWhiteSpace(markType) && kvp.Value.MarkType != markType) continue;
                count++;
            }

            return count;
        }
    }
}
