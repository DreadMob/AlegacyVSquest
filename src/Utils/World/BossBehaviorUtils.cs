using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Utility methods for boss behaviors that require ICoreServerAPI as the primary parameter
    /// or are not suitable as Entity extension methods.
    /// Entity-centric methods have been moved to <see cref="EntityExtensions"/>.
    /// </summary>
    public static class BossBehaviorUtils
    {
        public const string HasTargetKey = "alegacyvsquest:boss:hasTarget";

        private static readonly object SoundLimiterLock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, long> SoundLastMsByKey = new System.Collections.Generic.Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public static bool ShouldPlaySoundLimited(string key, int cooldownMs)
        {
            if (string.IsNullOrWhiteSpace(key)) return true;
            if (cooldownMs <= 0) return true;

            long now = Environment.TickCount64;
            lock (SoundLimiterLock)
            {
                if (SoundLastMsByKey.TryGetValue(key, out long last))
                {
                    if (now - last < cooldownMs)
                    {
                        return false;
                    }
                }

                SoundLastMsByKey[key] = now;
                return true;
            }
        }

        public static bool ShouldPlaySoundLimited(Entity entity, string sound, int cooldownMs)
        {
            string key = $"ent:{entity?.EntityId ?? 0}:{sound ?? ""}";
            return ShouldPlaySoundLimited(key, cooldownMs);
        }

        public static bool TryGetHealth(Entity entity, out ITreeAttribute healthTree, out float currentHealth, out float maxHealth) => entity.TryGetHealth(out healthTree, out currentHealth, out maxHealth);

        public static void StopAiAndFreeze(Entity entity) => entity.StopAiAndFreeze();

        public static void ApplyRotationLock(Entity entity, ref bool yawLocked, ref float lockedYaw) => entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);

        public static void UnregisterCallbackSafe(ICoreServerAPI sapi, ref long callbackId)
        {
            if (sapi != null && callbackId != 0)
            {
                sapi.Event.UnregisterCallback(callbackId);
                callbackId = 0;
            }
        }

        public static void UnregisterGameTickListenerSafe(ICoreServerAPI sapi, ref long listenerId)
        {
            if (sapi != null && listenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(listenerId);
                listenerId = 0;
            }
        }

        public static bool IsCooldownReady(ICoreServerAPI sapi, Entity entity, string lastStartKey, float cooldownSeconds)
        {
            if (sapi == null || entity == null) return false;
            if (string.IsNullOrWhiteSpace(lastStartKey)) return true;

            if (cooldownSeconds <= 0f) return true;

            long cooldownMs = (long)Math.Round(cooldownSeconds * 1000.0);

            if (cooldownMs <= 0) return true;

            long nowMs = sapi.World.ElapsedMilliseconds;
            long lastStartMs = entity.WatchedAttributes?.GetLong(lastStartKey, 0) ?? 0;

            return nowMs - lastStartMs >= cooldownMs;
        }

        public static void MarkCooldownStart(ICoreServerAPI sapi, Entity entity, string lastStartKey)
        {
            if (sapi == null || entity == null) return;
            if (string.IsNullOrWhiteSpace(lastStartKey)) return;

            entity.WatchedAttributes.SetLong(lastStartKey, sapi.World.ElapsedMilliseconds);
            entity.WatchedAttributes.MarkPathDirty(lastStartKey);
        }

        /// <inheritdoc cref="EntityExtensions.UpdatePlayerWalkSpeed"/>
        public static bool UpdatePlayerWalkSpeed(EntityPlayer player, float epsilon = 0.001f) => player.UpdatePlayerWalkSpeed(epsilon);

        /// <summary>
        /// Finds the closest valid player target for a boss entity.
        /// NOTE: Prefer BossTargetingSystem.TryFindTarget for new code. This static method is retained
        /// only if external callers need it. Currently unused – marked for future removal.
        /// </summary>
        // TODO: Remove TryFindTarget if no external callers emerge after migration.
        // public static bool TryFindTarget(Entity entity, ICoreServerAPI sapi, float minRange, float maxRange, out EntityPlayer target, out float distance)

        public sealed class LoopSound : IDisposable
        {
            private long listenerId;
            private ICoreServerAPI sapi;
            private Entity entity;
            private AssetLocation soundLoc;
            private float range;
            private float volume;

            public void Start(ICoreServerAPI sapi, Entity entity, string sound, float range, int intervalMs)
            {
                Start(sapi, entity, sound, range, intervalMs, 1f);
            }

            public void Start(ICoreServerAPI sapi, Entity entity, string sound, float range, int intervalMs, float volume)
            {
                Stop();

                if (sapi == null || entity == null || string.IsNullOrWhiteSpace(sound)) return;

                this.sapi = sapi;
                this.entity = entity;
                this.range = range;
                this.volume = volume;

                if (this.volume <= 0f) this.volume = 1f;

                int interval = Math.Max(250, intervalMs);
                soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
                if (soundLoc == null) return;

                var self = this;
                listenerId = sapi.Event.RegisterGameTickListener(_ =>
                {
                    try
                    {
                        if (self.entity == null || !self.entity.Alive)
                        {
                            self.Stop();
                            return;
                        }

                        float pitch = (float)self.sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                        self.sapi?.World?.PlaySoundAt(self.soundLoc, self.entity, null, pitch, self.range, self.volume);
                    }
                    catch (Exception)
                    {
                        // Sound playback failed - entity may have been removed
                    }
                }, interval);
            }

            public void Stop()
            {
                if (listenerId != 0)
                {
                    try
                    {
                        sapi?.Event?.UnregisterGameTickListener(listenerId);
                    }
                    catch (Exception)
                    {
                        // Unregister failed - listener may already be disposed
                    }

                    listenerId = 0;
                }

                sapi = null;
                entity = null;
                soundLoc = null;
                volume = 0f;
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
