using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            ApplyCoreConfig();

            entityTracker = new BossEntityTracker(sapi);

            LoadConfigs();
            LoadState();

            entityTracker.Start();

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 10000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
        }

        public override void Dispose()
        {
            entityTracker?.Stop();
            entityTracker = null;
            combatStateMachines.Clear();

            if (sapi != null)
            {
                if (tickListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(tickListenerId);
                    tickListenerId = 0;
                }

                sapi.Event.GameWorldSave -= OnWorldSave;
                sapi.Event.OnEntityDeath -= OnEntityDeath;
            }

            base.Dispose();
        }

        private void LoadConfigs()
        {
            configs.Clear();

            foreach (var mod in sapi.ModLoader.Mods)
            {
                try
                {
                    var assets = sapi.Assets.GetMany<BossHuntConfig>(sapi.Logger, "config/bosshunt", mod.Info.ModID);
                    foreach (var asset in assets)
                    {
                        if (asset.Value != null)
                        {
                            if (IsBossKeySkipped(asset.Value.bossKey)) continue;
                            configs.Add(asset.Value);

                            // Register boss key with entity tracker
                            entityTracker?.RegisterBossKey(asset.Value.bossKey);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
