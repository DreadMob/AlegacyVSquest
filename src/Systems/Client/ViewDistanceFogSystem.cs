using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class ViewDistanceFogSystem : ModSystem
    {
        private const string ModifierKey = "alegacyvsquest:viewdistancefog";
        private ICoreClientAPI capi;
        private long tickListenerId;

        private int tickIntervalMs = 500;
        private float baseDensity = 0.00125f;
        private float fogMinMul = 0.03f;
        private float negativeFogDensityAddMul = 0.006f;
        private float positiveFogDensitySubMul = 0.0009f;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            try
            {
                var qs = api?.ModLoader?.GetModSystem<QuestSystem>();
                var cfg = qs?.CoreConfig?.Client?.ViewDistanceFog;
                if (cfg != null)
                {
                    if (cfg.TickIntervalMs > 0) tickIntervalMs = cfg.TickIntervalMs;
                    baseDensity = cfg.BaseDensity;
                    fogMinMul = cfg.FogMinMul;
                    negativeFogDensityAddMul = cfg.NegativeFogDensityAddMul;
                    positiveFogDensitySubMul = cfg.PositiveFogDensitySubMul;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[ViewDistanceFogSystem] Failed to load ViewDistanceFog config: {0}", ex.Message);
            }

            tickListenerId = api.Event.RegisterGameTickListener(OnTick, 500);
        }

        private void OnTick(float dt)
        {
            if (capi?.World?.Player == null) return;

            float viewDistance = GetViewDistanceAttr(capi.World.Player);
            ApplyFog(viewDistance);
        }

        private float GetViewDistanceAttr(IClientPlayer player)
        {
            IInventory inv = player.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return 0f;

            float total = 0f;
            foreach (ItemSlot slot in inv)
            {
                if (!slot.Empty && slot.Itemstack?.Collectible.GetCollectibleInterface<Vintagestory.API.Common.IWearable>() != null)
                {
                    total += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrViewDistance);
                }
            }

            return total;
        }

        private void ApplyFog(float viewDistance)
        {
            var modifiers = capi?.Ambient?.CurrentModifiers;
            if (modifiers == null) return;

            if (!modifiers.TryGetValue(ModifierKey, out AmbientModifier modifier))
            {
                modifier = new AmbientModifier().EnsurePopulated();
                modifiers[ModifierKey] = modifier;
            }

            float strength = Math.Abs(viewDistance);
            if (strength <= 0f)
            {
                modifier.FogDensity.Weight = 0f;
                modifier.FogMin.Weight = 0f;
                return;
            }

            strength = GameMath.Clamp(strength, 0f, 1f);

            // FogMin controls how close the fog starts.
            // Positive viewDistance => allow the fog to start further away.
            // Negative viewDistance => do not increase FogMin (avoid improving visibility).
            modifier.FogMin.Value = viewDistance > 0f ? strength * fogMinMul : 0f;
            modifier.FogMin.Weight = strength;

            // Negative viewDistance => worse visibility (more fog)
            if (viewDistance < 0f)
            {
                // Important: do not overwrite existing (weather/biome) fog with a smaller base value.
                // If we set a fixed density here, then in heavy fog the player could actually see better.
                // Instead, build on the current effective fog density and only make it worse.
                // Also, avoid a feedback loop: capi.Render.FogDensity already includes our own modifier.
                // If we base calculations on it, fog would get stronger and stronger over time.
                float currentFogDensity = GetFogDensityWithoutSelf(modifiers);
                float addMul = Math.Abs(negativeFogDensityAddMul);
                float minAdd = baseDensity * 2f;
                float add = strength * Math.Max(addMul, minAdd);

                float targetDensity = Math.Max(baseDensity, currentFogDensity) + add;
                modifier.FogDensity.Value = GameMath.Clamp(targetDensity, 0f, 0.25f);
                modifier.FogDensity.Weight = 1f;
                return;
            }

            // Positive viewDistance => better visibility (less fog)
            modifier.FogDensity.Value = Math.Max(0f, baseDensity - strength * positiveFogDensitySubMul);
            modifier.FogDensity.Weight = strength;
        }

        private float GetFogDensityWithoutSelf(Vintagestory.API.Datastructures.OrderedDictionary<string, AmbientModifier> modifiers)
        {
            float blended = capi?.Ambient?.Base?.FogDensity?.Value ?? baseDensity;

            foreach (var kvp in modifiers)
            {
                if (kvp.Key == ModifierKey) continue;

                AmbientModifier mod = kvp.Value;
                if (mod?.FogDensity == null) continue;

                float w = mod.FogDensity.Weight;
                blended = w * w * mod.FogDensity.Value + (1f - w) * (1f - w) * blended;
            }

            return Math.Max(0f, blended);
        }
    }
}
