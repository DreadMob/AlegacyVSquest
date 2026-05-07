using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class EntityShiverStrokePatch
    {
        private const double TestStrokeChancePerTick = 0.25;
        private const string DespairStageKey = "alegacyvsquest:shiverdespairstage";

        [HarmonyPatch(typeof(EntityShiver), "OnGameTick")]
        public static class EntityShiver_OnGameTick_StrokeFreqPatch
        {
            public static bool Prefix(EntityShiver __instance, float dt)
            {
                try
                {
                    if (!HarmonyPatchSwitches.EntityShiverStrokeEnabled(HarmonyPatchSwitches.EntityShiverStroke_EntityShiver_OnGameTick_StrokeFreq)) return true;
                    if (__instance?.Api == null) return true;
                    if (__instance.Api.Side != EnumAppSide.Server) return true;

                    // Only apply to our boss
                    if (__instance.Code == null || !string.Equals(__instance.Code.ToShortString(), "alstory:bloodhand-clawchief", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (!__instance.Alive) return true;

                    if (__instance.HasBehavior<VsQuest.EntityBehaviorBossDespair>()) return true;

                    // Don't interfere if despair already active
                    if (__instance.AnimManager == null) return true;
                    if (__instance.AnimManager.IsAnimationActive("despair")) return true;

                    // Use private fields via reflection
                    var strokeActiveField = AccessTools.Field(typeof(EntityShiver), "strokeActive");
                    var aiTaskManagerField = AccessTools.Field(typeof(EntityShiver), "aiTaskManager");

                    if (strokeActiveField == null || aiTaskManagerField == null) return true;

                    bool strokeActive = (bool)strokeActiveField.GetValue(__instance);
                    if (strokeActive) return true;

                    int nextStage = GetNextDespairStage(__instance);
                    if (nextStage < 0)
                    {
                        return true;
                    }

                    strokeActiveField.SetValue(__instance, true);

                    var aiTaskManager = aiTaskManagerField.GetValue(__instance) as AiTaskManager;
                    aiTaskManager?.StopTasks();
                    FreezeEntity(__instance);

                    __instance.AnimManager.StartAnimation("despair");

                    // Vanilla duration: (rand*3 + 3) * 1000 ms. Make it 3x longer.
                    int baseSeconds = (int)(__instance.Api.World.Rand.NextDouble() * 3.0 + 3.0);
                    int durationMs = baseSeconds * 1000 * 3;

                    __instance.Api.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            __instance.AnimManager.StopAnimation("despair");
                        }
                        catch (Exception ex)
                        {
                            __instance?.Api?.Logger?.Warning("[EntityShiverStrokePatch] Failed to stop despair animation: {0}", ex.Message);
                        }

                        __instance.Api.Event.RegisterCallback(__ =>
                        {
                            try
                            {
                                strokeActiveField.SetValue(__instance, false);
                                UnfreezeEntity(__instance);
                            }
                            catch (Exception ex)
                            {
                                __instance?.Api?.Logger?.Warning("[EntityShiverStrokePatch] Failed to unfreeze entity: {0}", ex.Message);
                            }
                        }, 200);

                    }, durationMs);

                    // We handled the special case; skip vanilla OnGameTick to avoid double-trigger.
                    return false;
                }
                catch (Exception ex)
                {
                    __instance?.Api?.Logger?.Warning("[EntityShiverStrokePatch] Failed in Prefix: {0}", ex.Message);
                    return true;
                }
            }

            private static int GetNextDespairStage(EntityShiver shiver)
            {
                if (shiver?.WatchedAttributes == null) return -1;

                var healthTree = shiver.WatchedAttributes.GetTreeAttribute("health");
                if (healthTree == null) return -1;

                float maxHealth = healthTree.GetFloat("maxhealth", 0f);
                if (maxHealth <= 0f)
                {
                    maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
                }

                float curHealth = healthTree.GetFloat("currenthealth", 0f);
                if (maxHealth <= 0f || curHealth <= 0f) return -1;

                int stage = shiver.WatchedAttributes.GetInt(DespairStageKey, 0);
                float healthFrac = curHealth / maxHealth;

                if (stage == 0 && healthFrac <= 0.75f)
                {
                    shiver.WatchedAttributes.SetInt(DespairStageKey, 1);
                    shiver.WatchedAttributes.MarkPathDirty(DespairStageKey);
                    return 1;
                }
                if (stage == 1 && healthFrac <= 0.50f)
                {
                    shiver.WatchedAttributes.SetInt(DespairStageKey, 2);
                    shiver.WatchedAttributes.MarkPathDirty(DespairStageKey);
                    return 2;
                }
                if (stage == 2 && healthFrac <= 0.25f)
                {
                    shiver.WatchedAttributes.SetInt(DespairStageKey, 3);
                    shiver.WatchedAttributes.MarkPathDirty(DespairStageKey);
                    return 3;
                }

                return -1;
            }

            private static void FreezeEntity(EntityShiver shiver)
            {
                if (shiver == null) return;
                shiver.Pos.Motion.Set(0, 0, 0);
                shiver.Controls.StopAllMovement();
            }

            private static void UnfreezeEntity(EntityShiver shiver)
            {
                if (shiver == null) return;
                shiver.Pos.Motion.Set(0, 0, 0);
            }
        }
    }
}
