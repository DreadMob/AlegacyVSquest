using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// AI task that seeks a specific target player by UID stored in entity attributes.
    /// Used by player clones to prioritize chasing their original player.
    /// Falls back to seeking any nearby player if target is not found.
    /// </summary>
    public class AiTaskSeekTargetPlayer : AiTaskBase
    {
        private const string TargetPlayerUidKey = "alegacyvsquest:bossplayerclone:playeruid";

        private float movespeed;
        private float seekingRange;
        private float maxFollowTimeSec;

        private Entity targetEntity;
        private long lastSearchMs;
        private int searchIntervalMs = 1000;
        private float followTime;

        public AiTaskSeekTargetPlayer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            movespeed = taskConfig["movespeed"].AsFloat(0.04f);
            seekingRange = taskConfig["seekingRange"].AsFloat(25f);
            maxFollowTimeSec = taskConfig["maxFollowTime"].AsFloat(120f);
            searchIntervalMs = taskConfig["searchIntervalMs"].AsInt(1000);
            // Animation is loaded automatically by AiTaskBase from taskConfig["animation"] and taskConfig["animationSpeed"]
        }

        public override bool ShouldExecute()
        {
            if (world.ElapsedMilliseconds - lastSearchMs < searchIntervalMs) return false;

            lastSearchMs = world.ElapsedMilliseconds;

            // Get target player UID from attributes
            string targetPlayerUid = entity.WatchedAttributes?.GetString(TargetPlayerUidKey);

            if (!string.IsNullOrEmpty(targetPlayerUid))
            {
                // Try to find the specific target player
                var sapi = world.Api as ICoreServerAPI;
                if (sapi != null)
                {
                    var targetPlayer = sapi.World.PlayerByUid(targetPlayerUid);
                    if (targetPlayer?.Entity != null && targetPlayer.Entity.Alive)
                    {
                        double dist = entity.Pos.DistanceTo(targetPlayer.Entity.Pos);
                        if (dist <= seekingRange)
                        {
                            targetEntity = targetPlayer.Entity;
                            return true;
                        }
                    }
                }
            }

            // Fallback: find any nearby player
            var players = world.AllOnlinePlayers;
            if (players == null) return false;

            foreach (var player in players)
            {
                if (player?.Entity == null || !player.Entity.Alive) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = entity.Pos.DistanceTo(player.Entity.Pos);
                if (dist <= seekingRange)
                {
                    targetEntity = player.Entity;
                    return true;
                }
            }

            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            followTime = 0;

            if (targetEntity != null && pathTraverser != null)
            {
                // Start navigation to target
                pathTraverser.NavigateTo_Async(targetEntity.Pos.XYZ, movespeed, 1.5f, OnGoalReached, OnStuck, null, 1000, 1);
            }

            // Start animation
            if (animMeta != null)
            {
                entity.AnimManager?.StartAnimation(animMeta);
            }
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetEntity == null || !targetEntity.Alive)
            {
                return false;
            }

            followTime += dt;
            if (followTime > maxFollowTimeSec)
            {
                return false;
            }

            // Check if still in range
            double dist = entity.Pos.DistanceTo(targetEntity.Pos);
            if (dist > seekingRange * 1.5)
            {
                return false;
            }

            // Update path to target periodically
            if (pathTraverser != null)
            {
                pathTraverser.NavigateTo_Async(targetEntity.Pos.XYZ, movespeed, 1.5f, OnGoalReached, OnStuck, null, 1000, 1);
            }

            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            targetEntity = null;

            // Stop animation
            if (animMeta != null)
            {
                entity.AnimManager?.StopAnimation(animMeta.Code);
            }

            pathTraverser?.Stop();
        }

        private void OnGoalReached()
        {
            // Target reached, continue seeking
        }

        private void OnStuck()
        {
            // Path stuck, try to find new path
            if (targetEntity != null && pathTraverser != null)
            {
                pathTraverser.NavigateTo_Async(targetEntity.Pos.XYZ, movespeed, 1.5f, OnGoalReached, OnStuck, null, 1000, 1);
            }
        }
    }
}
