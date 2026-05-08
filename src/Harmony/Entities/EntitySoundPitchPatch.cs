using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "PlayEntitySound")]
    public static class EntitySoundPitchPatch
    {
        public static bool Prefix(Entity __instance, string type, IPlayer dualCallByPlayer)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntitySoundPitchEnabled(HarmonyPatchSwitches.EntitySoundPitch_Entity_PlayEntitySound)) return true;
                if (__instance?.Properties?.Sounds == null) return true;
                if (!__instance.Properties.Sounds.TryGetValue(type, out var sound) || sound.Location == null) return true;

                float mult = 1f;
                try
                {
                    mult = __instance.Properties.Attributes?["vsquestSoundPitchMul"].AsFloat(1f) ?? 1f;
                }
                catch
                {
                }

                if (mult <= 0f || Math.Abs(mult - 1f) < 0.0001f)
                {
                    mult = 1f;
                }

                if (__instance.Properties.ResolvedSounds == null || !__instance.Properties.ResolvedSounds.TryGetValue(type, out var locations) || locations.Length == 0)
                {
                    return true;
                }

                var location = locations[__instance.World.Rand.Next(locations.Length)];
                
                // Always apply pitch randomization for entity sounds
                float pitch = (float)__instance.World.Rand.NextDouble() * 0.5f + 0.75f;
                if (mult != 1f)
                {
                    pitch *= mult;
                }

                // Use entity-relative PlaySoundAt with proper range (min 32 blocks)
                float range = 40f;
                if (__instance.Properties.Attributes?["vsquestSoundRange"].Exists == true)
                {
                    range = __instance.Properties.Attributes["vsquestSoundRange"].AsFloat(40f);
                }
                __instance.World.PlaySoundAt(location, __instance, dualCallByPlayer, pitch, range, 1.5f);
                return false;
            }
            catch
            {
                // On any error, let vanilla handle it
                return true;
            }
        }
    }
}
