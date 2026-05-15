using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class QuestSystem : ModSystem
    {
        // Registries are now managed by QuestRegistryService (instance-based with static fallback)
        private readonly IQuestRegistryService registryService = QuestRegistryService.Instance;
        public Dictionary<string, Quest> QuestRegistry => registryService.QuestRegistry;
        public Dictionary<string, IQuestAction> ActionRegistry => registryService.ActionRegistry;
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry => registryService.ActionObjectiveRegistry;

        private QuizSystem quizSystem;

        private IQuestValidationService validationService;
        private IQuestStateManager stateManager;
        private IQuestPersistenceManager persistenceManager;
        private IQuestLifecycleManager lifecycleManager;
        private QuestEventHandler eventHandler;
        private Systems.Database.VsQuestDbClient dbClient;
        private Systems.Database.VsQuestSyncService dbSyncService;
        private IQuestEventDispatcher activeQuestEventDispatcher;
        private QuestActionRegistry actionRegistry;
        private QuestObjectiveRegistry objectiveRegistry;
        private QuestNetworkChannelRegistry networkChannelRegistry;
        private QuestChatCommandRegistry chatCommandRegistry;

        public QuestConfig Config { get; set; }
        public AlegacyVsQuestConfig CoreConfig { get; private set; } = new AlegacyVsQuestConfig();
        private ICoreAPI api;

        private VsQuestDiscoveryHud discoveryHud;
        private QuestJournalHotkeyHandler journalHotkeyHandler;
        private QuestSelectGuiManager questSelectGuiManager;
        private ServerInfoGuiManager serverInfoGuiManager;
        private QuestNotificationHandler notificationHandler;

        // Packet handlers (extracted from Packet Handler Delegations region)
        private QuestPacketHandler questPacketHandler;
        private DialogPacketHandler dialogPacketHandler;
        private QuizPacketHandler quizPacketHandler;

        // Promo code system
        private PromoCodeSystem promoCodeSystem;
        private PromoCodeNetworkHandler promoCodeNetworkHandler;

        private long lagMonitorListenerId;

        public QuizSystem QuizSystem => quizSystem;
        public PromoCodeSystem PromoCodeSystem => promoCodeSystem;
        public PromoCodeNetworkHandler PromoCodeNetworkHandler => promoCodeNetworkHandler;

        private static T LoadOrCreateModConfig<T>(ICoreAPI api, string filename) where T : class, new()
        {
            T loaded = null;

            try {

                loaded = api.LoadModConfig<T>(filename);

                loaded ??= new T();

                /* Always store after load to ensure the config file exists and stays schema-up-to-date. */
                api.StoreModConfig(loaded, filename);
            } catch (Exception e) {
                api.Logger.Error("[alegacyvsquest] Failed to load mod config {0}: {1}", filename, e);
            }

            return loaded;
        }

        private void LoadConfigs(ICoreAPI api)
        {
            Config = LoadOrCreateModConfig<QuestConfig>(api, "alegacyvsquest/questconfig.json");
            CoreConfig = LoadOrCreateModConfig<AlegacyVsQuestConfig>(api, "alegacyvsquest/alegacy-vsquest-config.json");
            HarmonyPatchSwitches.ApplyFromConfig(CoreConfig);
            
            // Initialize performance config
            Systems.Performance.PerformanceConfig.Initialize(CoreConfig.Performance);
            api.Logger.Notification("[vsquest] Performance config initialized (optimizations: {0})", 
                CoreConfig.Performance.EnablePerformanceOptimizations);
        }

        public bool TryReloadConfigs(out string resultMessage)
        {
            resultMessage = null;
            if (api == null)
            {
                resultMessage = "Core API not available.";
                return false;
            }

            try
            {
                LoadConfigs(api);

                resultMessage = "Reloaded mod configs (questconfig.json, alegacy-vsquest-config.json).";
                return true;
            }
            catch (Exception e)
            {

                api.Logger.Error("[alegacyvsquest] Failed to reload configs: {0}", e);
                resultMessage = $"Reload failed: {e.Message}";
                return false;
            }
        }

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            base.StartPre(api);
            ModClassRegistry.RegisterAll(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            /* Initialize the centralized registry service. */
            QuestRegistryService.Initialize(api);

            quizSystem = new QuizSystem(this);

            var harmony = new HarmonyLib.Harmony("alegacyvsquest");
            /* Centralized patch entry-point; individual patches decide whether they activate. */
            harmony.PatchAll();
            
            // Set API reference for ItemMergeChargePatches logging
            VsQuest.Harmony.Items.ItemMergeChargePatches.API = api;

            LocalizationUtils.LoadFromAssets(api);
            LocalizationUtils.LoadNestedLanguageFiles(api);
            LocalizationUtils.SetNestedLocalizationDomains(new[] { "alegacyvsquest", "albase", "ALStory" });

            VsQuest.Harmony.EntityInteractPatch.TryPatch(harmony);

            objectiveRegistry = new QuestObjectiveRegistry(ActionObjectiveRegistry, api);
            objectiveRegistry.Register();

            networkChannelRegistry = new QuestNetworkChannelRegistry(this);

            LoadConfigs(api);

            /* discoveryHud is client-only; it will be replaced in StartClientSide when available. */
            notificationHandler = new QuestNotificationHandler(discoveryHud);
            questSelectGuiManager = new QuestSelectGuiManager(Config);
            serverInfoGuiManager = new ServerInfoGuiManager();
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            ModClassRegistry.RegisterAll(capi);

            LocalizationUtils.LoadFromAssets(capi);
            LocalizationUtils.LoadNestedLanguageFiles(capi);
            LocalizationUtils.SetNestedLocalizationDomains(new[] { "alegacyvsquest", "albase", "ALStory" });

            if (networkChannelRegistry == null)
            {
                capi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartClientSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterClient(capi);

            journalHotkeyHandler = new QuestJournalHotkeyHandler(capi);
            journalHotkeyHandler.Register();

            discoveryHud = QuestClientUiSetup.Initialize(capi);

            notificationHandler = new QuestNotificationHandler(discoveryHud);
            questSelectGuiManager = new QuestSelectGuiManager(Config);
            serverInfoGuiManager = new ServerInfoGuiManager();

            // Initialize client-side packet handlers so network callbacks don't NRE
            questPacketHandler = new QuestPacketHandler(null, questSelectGuiManager, _ => new List<ActiveQuest>());
            dialogPacketHandler = new DialogPacketHandler(null, notificationHandler, serverInfoGuiManager, capi);
            quizPacketHandler = new QuizPacketHandler(quizSystem);

            // Register promo code client network
            promoCodeNetworkHandler = new PromoCodeNetworkHandler();
            promoCodeNetworkHandler.RegisterClient(capi);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            ModClassRegistry.RegisterAll(sapi);

            QuestRegistryService.Initialize(sapi);

            sapi.Logger.VerboseDebug($"[alegacyvsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            /* Server-only wiring: persistence + lifecycle logic + event subscriptions. */
            activeQuestEventDispatcher = new ActiveQuestEventDispatcher(sapi, new InteractPositionCache());
            
            var questContext = new QuestContext(this, sapi);
            validationService = new QuestValidationService(questContext);
            stateManager = new QuestStateManager();
            
            persistenceManager = new QuestPersistenceManager(sapi, stateManager);
            lifecycleManager = new QuestLifecycleManager(api, stateManager, registryService);

            eventHandler = new QuestEventHandler(persistenceManager, sapi, activeQuestEventDispatcher);

            // Initialize database sync
            var dbConfig = sapi.LoadModConfig<Systems.Database.AlegacyVsQuestDbConfig>("alegacyvsquest/db-config.json")
                ?? new Systems.Database.AlegacyVsQuestDbConfig();
            sapi.StoreModConfig(dbConfig, "alegacyvsquest/db-config.json");

            dbClient = new Systems.Database.VsQuestDbClient(dbConfig);
            if (dbClient.IsEnabled)
            {
                dbSyncService = new Systems.Database.VsQuestSyncService(dbClient, sapi);
                eventHandler.SetDbSyncService(dbSyncService);

                // Also set in ReputationSystem if it exists
                var reputationSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
                reputationSystem?.SetDbSyncService(dbSyncService);

                sapi.Logger.Notification("[vsquest] Database sync enabled (event-driven)");
            }
            else
            {
                sapi.Logger.Notification("[vsquest] Database sync disabled");
            }

            // Initialize packet handlers
            questPacketHandler = new QuestPacketHandler(lifecycleManager, questSelectGuiManager, GetPlayerQuests);
            dialogPacketHandler = new DialogPacketHandler(eventHandler, notificationHandler, serverInfoGuiManager, api);
            quizPacketHandler = new QuizPacketHandler(quizSystem);

            if (networkChannelRegistry == null)
            {
                sapi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartServerSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterServer(sapi);

            /* Registers quest actions and binds acceptance to packet handler. */
            actionRegistry = new QuestActionRegistry(ActionRegistry, api, sapi, questPacketHandler.OnQuestAccepted);
            actionRegistry.Register();

            eventHandler.RegisterEventHandlers();

            chatCommandRegistry = new QuestChatCommandRegistry(sapi, api, this);
            chatCommandRegistry.Register();

            // Initialize promo code system
            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
            promoCodeSystem = new PromoCodeSystem(sapi, itemSystem, this);
            promoCodeSystem.Initialize();

            promoCodeNetworkHandler = new PromoCodeNetworkHandler();
            promoCodeNetworkHandler.RegisterServer(sapi, promoCodeSystem);
        }

        public override void Dispose()
        {
            dbSyncService?.Dispose();

            if (api is ICoreServerAPI sapi && lagMonitorListenerId != 0)
            {
                /* Defensive cleanup for optional lag monitor tick listener (if ever enabled). */
                sapi.Event.UnregisterGameTickListener(lagMonitorListenerId);
                lagMonitorListenerId = 0;
             }

            base.Dispose();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            /* Clear caches on asset reload so quest/objective evaluation stays consistent. */
            QuestTimeGateUtil.ClearCache();
            QuestTickUtil.ClearAllCaches();

            MobLocalizationUtils.LoadFromAssets(api);

            LocalizationUtils.LoadFromAssets(api);
            LocalizationUtils.LoadNestedLanguageFiles(api);
            quizSystem?.LoadFromAssets(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                /* Load per-mod quest assets from the standard config/quests folder. */
                var questAssets = api.Assets.GetMany<Quest>(api.Logger, "config/quests", mod.Info.ModID);
                foreach (var questAsset in questAssets)
                {
                    try
                    {
                        TryRegisterQuest(api, questAsset.Value, questAsset.Key);
                    }
                    catch (Exception e)
                    {
                        api.Logger.Error($"Failed to load quest from {questAsset.Key}: {e.Message}");
                    }
                }

                /* Also support bundled config/quests.json or config/quest.json files. */
                LoadQuestAssetsFromFile(api, mod.Info.ModID);
            }
        }

        private void LoadQuestAssetsFromFile(ICoreAPI api, string domain)
        {
            if (api == null || string.IsNullOrWhiteSpace(domain)) return;

            var assets = api.Assets;
            /* Compatibility: allow both quests.json (array) and quest.json (single object). */
            var asset = assets.TryGet(new AssetLocation(domain, "config/quests.json"))
                ?? assets.TryGet(new AssetLocation(domain, "config/quest.json"));

            if (asset == null) return;

            try
            {
                var root = asset.ToObject<JsonObject>();
                if (root == null) return;

                if (root.IsArray())
                {
                    /* File contains a quest list. */
                    var array = root.AsArray();
                    if (array == null) return;

                    foreach (var entry in array)
                    {
                        if (entry == null || !entry.Exists) continue;
                        var quest = entry.AsObject<Quest>();
                        TryRegisterQuest(api, quest, asset.Location?.ToString() ?? "config/quests.json");
                    }

                    return;
                }

                /* File contains a single quest definition. */
                var singleQuest = root.AsObject<Quest>();
                TryRegisterQuest(api, singleQuest, asset.Location?.ToString() ?? "config/quests.json");
            }
            catch (Exception e)
            {
                api.Logger.Error($"Failed to load quests from {asset.Location}: {e.Message}");
            }
        }

        private void TryRegisterQuest(ICoreAPI api, Quest quest, string source)
        {
            if (quest == null) return;
            if (string.IsNullOrWhiteSpace(quest.id)) return;

            // Validate quest before registration
            if (validationService != null)
            {
                var errors = validationService.ValidateQuest(quest);
                if (errors.Any())
                {
                    api.Logger.Warning("[QuestSystem] Quest '{0}' has validation errors: {1}", quest.id, string.Join(", ", errors));
                }
            }

            QuestRegistryService.RegisterQuest(quest, source);
        }

        public List<ActiveQuest> GetPlayerQuests(string playerUID)
        {
            return persistenceManager.GetPlayerQuests(playerUID);
        }

        public void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests)
        {
            /* Persistence is write-behind; MarkDirty schedules an autosave via QuestPersistenceManager. */
            persistenceManager.MarkDirty(playerUID);
        }

        internal string NormalizeQuestId(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return questId;
            if (QuestRegistry == null) return questId;

            if (QuestRegistry.ContainsKey(questId)) return questId;

            /* Try case-insensitive lookup to tolerate older content / manual edits. */
            foreach (var kvp in QuestRegistry)
            {
                if (string.Equals(kvp.Key, questId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return questId;
        }

        internal string[] GetNormalizedCompletedQuestIds(IPlayer player)
        {
            if (player?.Entity?.WatchedAttributes == null) return new string[0];
            if (QuestRegistry == null) return new string[0];

            var wa = player.Entity.WatchedAttributes;
            /* Stored as raw strings in watched attributes; normalize and filter to currently-registered quests. */
            var codes = wa.GetStringArray("alegacyvsquest:playercompleted");
            if (codes == null || codes.Length == 0) return new string[0];

            var normalized = new List<string>();
            foreach (var raw in codes)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string n = NormalizeQuestId(raw);
                if (QuestRegistry.ContainsKey(n))
                {
                    normalized.Add(n);
                }
            }

            return normalized.ToArray();
        }

        internal bool ForceCompleteQuestInternal(IServerPlayer player, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            return lifecycleManager.ForceCompleteQuest(player, message, sapi, GetPlayerQuests);
        }

        internal void DispatchBlockUsedEvent(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (activeQuest == null || activeQuestEventDispatcher == null) return;
            activeQuestEventDispatcher.OnBlockUsed(activeQuest, blockCode, position, byPlayer, context);
        }

        internal IQuestStateManager GetStateManager() => stateManager;
        internal Systems.Database.VsQuestSyncService GetDbSyncService() => dbSyncService;

        // Packet handler accessors for network registration
        internal QuestPacketHandler QuestPacketHandler => questPacketHandler;
        internal DialogPacketHandler DialogPacketHandler => dialogPacketHandler;
        internal QuizPacketHandler QuizPacketHandler => quizPacketHandler;

        /// <summary>
        /// Get the database client for external access (e.g., admin commands).
        /// </summary>
        public VsQuest.Systems.Database.VsQuestDbClient GetDbClient()
        {
            return dbClient;
        }
    }
}
