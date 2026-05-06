using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public abstract class EntityBehaviorBossBase : EntityBehavior
    {
        protected ICoreServerAPI Sapi;

        public EntityBehaviorBossBase(Entity entity) : base(entity)
        {
            Sapi = entity?.Api as ICoreServerAPI;
        }

        protected bool TryGetHealthFraction(out float fraction)
        {
            fraction = 1f;
            if (entity == null) return false;
            var health = entity.GetBehavior<EntityBehaviorHealth>();
            if (health == null || health.MaxHealth <= 0) return false;
            fraction = health.Health / health.MaxHealth;
            return true;
        }

        protected void TryPlayAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;
            entity?.AnimManager?.StartAnimation(animation);
        }

        protected void TryStopAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;
            entity?.AnimManager?.StopAnimation(animation);
        }

        protected void TryPlaySound(string sound, float range = 24f, int startMs = 0, float volume = 1f)
        {
            if (Sapi == null || string.IsNullOrWhiteSpace(sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float vol = volume <= 0f ? 1f : volume;
            float rng = range <= 0f ? 24f : range;

            if (startMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    if (entity?.Alive == true)
                    {
                        float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                        Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, rng, vol);
                    }
                }, startMs);
            }
            else
            {
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, rng, vol);
            }
        }

        protected void UnregisterCallbackSafe(ref long id)
        {
            if (id != 0 && Sapi != null)
            {
                Sapi.Event.UnregisterCallback(id);
                id = 0;
            }
        }

        protected void UnregisterGameTickListenerSafe(ref long id)
        {
            if (id != 0 && Sapi != null)
            {
                Sapi.Event.UnregisterGameTickListener(id);
                id = 0;
            }
        }
    }
}
