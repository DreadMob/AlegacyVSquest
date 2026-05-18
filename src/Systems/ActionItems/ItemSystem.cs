using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using System;

namespace VsQuest
{
    public class ItemSystem : ModSystem
    {
        private float actionItemCastDurationSec = 3f;
        private float actionItemCastSlowdown = -0.5f;
        private string actionItemCastSpeedStatKey = "alegacyvsquest:actionitemcast";
        private string bloodmeterActionItemId = "albase:bosshunt-tracker";
        private string trialTrackerActionItemId = "albase:trial-tracker";
        private float trialTrackerCastDurationSec = 3f;
        private float trialTrackerCastSlowdown = -0.5f;

        private int inventoryScanIntervalMs = 2000;
        private int hotbarEnforceIntervalMs = 1000;

        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private QuestSystem questSystem;

        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        private long inventoryScanListenerId = 0;
        private long hotbarEnforceListenerId = 0;
        private long clientTickListenerId = 0;
        private Action blockTexturesLoadedHandler;

        private ActionItemAttributeResolver attributeResolver;
        private ActionItemCastController castController;
        private ActionItemInputHandler inputHandler;

        private ActionItemCreativeTabInjector creativeTabInjector;
        private ActionItemHotbarEnforcer hotbarEnforcer;
        private ActionItemInventoryScanner inventoryScanner;
        private ActionItemActionExecutor actionExecutor;
        private ActionItemPacketHandler packetHandler;
        private ActionItemSoundConfig soundConfig;
        private ItemQualityService qualityService;
        private RerollService rerollService;

        private readonly Dictionary<string, (string invKey, int slot)> inventoryScanCursorByPlayerUid = new Dictionary<string, (string invKey, int slot)>(StringComparer.Ordinal);

        // Static registry for access from WearableStatsCache without needing ItemSystem instance
        public static Dictionary<string, ActionItem> StaticActionItemRegistry { get; private set; } = new Dictionary<string, ActionItem>();

        public Dictionary<string, ActionItem> ActionItemRegistry { get; private set; } = new Dictionary<string, ActionItem>();

        public ItemQualityService QualityService => qualityService;
        public RerollService RerollService => rerollService;

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            questSystem = api.ModLoader.GetModSystem<QuestSystem>();
        }

        private void ApplyCoreConfig()
        {
            try
            {
                var cfg = questSystem?.CoreConfig?.ActionItems;
                if (cfg == null) return;

                if (cfg.BossHuntTrackerCastDurationSec > 0f) actionItemCastDurationSec = cfg.BossHuntTrackerCastDurationSec;
                actionItemCastSlowdown = cfg.BossHuntTrackerCastSlowdown;
                if (!string.IsNullOrWhiteSpace(cfg.BossHuntTrackerCastSpeedStatKey)) actionItemCastSpeedStatKey = cfg.BossHuntTrackerCastSpeedStatKey;
                if (!string.IsNullOrWhiteSpace(cfg.BossHuntTrackerActionItemId)) bloodmeterActionItemId = cfg.BossHuntTrackerActionItemId;

                if (!string.IsNullOrWhiteSpace(cfg.TrialTrackerActionItemId)) trialTrackerActionItemId = cfg.TrialTrackerActionItemId;
                if (cfg.TrialTrackerCastDurationSec > 0f) trialTrackerCastDurationSec = cfg.TrialTrackerCastDurationSec;
                trialTrackerCastSlowdown = cfg.TrialTrackerCastSlowdown;

                if (cfg.InventoryScanIntervalMs > 0) inventoryScanIntervalMs = cfg.InventoryScanIntervalMs;
                if (cfg.HotbarEnforceIntervalMs > 0) hotbarEnforceIntervalMs = cfg.HotbarEnforceIntervalMs;
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[ItemSystem] Failed to apply ActionItems config: {0}", ex.Message);
            }
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (api == null) return;

            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ItemConfig>(api.Logger, "config/itemconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        foreach (var actionItem in asset.Value.actionItems)
                        {
                            ActionItemRegistry[actionItem.id] = actionItem;
                            StaticActionItemRegistry[actionItem.id] = actionItem;
                        }
                    }
                }
            }

            // Load quality configurations
            qualityService = new ItemQualityService(api);
            qualityService.LoadConfigs();

            // Load reroll configurations (server-side only)
            if (api.Side == EnumAppSide.Server)
            {
                rerollService = new RerollService(api as ICoreServerAPI);
                rerollService.LoadConfigs();
            }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            // Must run on both sides: creative tab list is synced/validated server-side when moving items.
            if (api == null) return;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;
            if (creativeTabInjector == null)
            {
                creativeTabInjector = new ActionItemCreativeTabInjector(ActionItemRegistry, questSystem);
            }

            creativeTabInjector.Inject(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;

            ApplyCoreConfig();

            attributeResolver = new ActionItemAttributeResolver(ActionItemRegistry);
            actionExecutor = new ActionItemActionExecutor(questSystem, sapi);
            packetHandler = new ActionItemPacketHandler(questSystem, attributeResolver, actionExecutor);

            serverChannel = VsQuestNetworkRegistry.RegisterItemActionServer(api, packetHandler);
            hotbarEnforcer = new ActionItemHotbarEnforcer(api);
            inventoryScanner = new ActionItemInventoryScanner(
                api,
                questSystem,
                attributeResolver,
                inventoryScanCursorByPlayerUid,
                actionExecutor.Execute);

            // Periodically scan inventories for action items that should trigger when added.
            // This avoids relying on right click and allows one-time processing.
            inventoryScanListenerId = api.Event.RegisterGameTickListener(OnInventoryScanTick, inventoryScanIntervalMs);

            // Periodically enforce hotbar-only placement for special quest items (blockEquip action items).
            hotbarEnforceListenerId = api.Event.RegisterGameTickListener(OnHotbarEnforceTick, hotbarEnforceIntervalMs);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;

            ApplyCoreConfig();

            clientChannel = VsQuestNetworkRegistry.RegisterItemActionClient(api);

            attributeResolver = new ActionItemAttributeResolver(ActionItemRegistry);
            soundConfig = new ActionItemSoundConfig();

            var castableItems = new Dictionary<string, CastableItemConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [bloodmeterActionItemId] = new CastableItemConfig
                {
                    ActionItemId = bloodmeterActionItemId,
                    CastDurationSec = actionItemCastDurationSec,
                    CastSlowdown = actionItemCastSlowdown
                },
                [trialTrackerActionItemId] = new CastableItemConfig
                {
                    ActionItemId = trialTrackerActionItemId,
                    CastDurationSec = trialTrackerCastDurationSec,
                    CastSlowdown = trialTrackerCastSlowdown
                }
            };

            castController = new ActionItemCastController(
                capi,
                clientChannel,
                attributeResolver,
                castableItems,
                actionItemCastSpeedStatKey,
                soundConfig.CastLoopSound,
                soundConfig.CastCompleteSound,
                soundConfig.CastSoundVolume,
                soundConfig.CastSoundRange,
                soundConfig.CastCompleteSoundRange,
                soundConfig.CastCompleteSoundVolume);

            inputHandler = new ActionItemInputHandler(capi, clientChannel, attributeResolver, castController);

            api.Event.MouseDown += inputHandler.OnMouseDown;
            api.Event.MouseUp += inputHandler.OnMouseUp;
            clientTickListenerId = api.Event.RegisterGameTickListener(inputHandler.OnClientTick, 20);

            blockTexturesLoadedHandler = () =>
            {
                if (creativeTabInjector == null)
                {
                    creativeTabInjector = new ActionItemCreativeTabInjector(ActionItemRegistry, questSystem);
                }

                creativeTabInjector.Inject(api);
            };
            api.Event.BlockTexturesLoaded += blockTexturesLoadedHandler;
        }

        private void OnHotbarEnforceTick(float dt)
        {
            hotbarEnforcer?.Tick(dt);
        }

        private void OnInventoryScanTick(float dt)
        {
            inventoryScanner?.Tick(dt);
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                if (inventoryScanListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(inventoryScanListenerId);
                    inventoryScanListenerId = 0;
                }

                if (hotbarEnforceListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(hotbarEnforceListenerId);
                    hotbarEnforceListenerId = 0;
                }
            }

            if (capi != null)
            {
                if (inputHandler != null)
                {
                    capi.Event.MouseDown -= inputHandler.OnMouseDown;
                    capi.Event.MouseUp -= inputHandler.OnMouseUp;
                }

                if (clientTickListenerId != 0)
                {
                    capi.Event.UnregisterGameTickListener(clientTickListenerId);
                    clientTickListenerId = 0;
                }

                if (blockTexturesLoadedHandler != null)
                {
                    capi.Event.BlockTexturesLoaded -= blockTexturesLoadedHandler;
                    blockTexturesLoadedHandler = null;
                }
            }

            castController?.Dispose();
            castController = null;
            inputHandler = null;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ExecuteActionItemPacket
    {
    }
}
