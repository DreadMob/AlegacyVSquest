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
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, IQuestAction> ActionRegistry { get; private set; } = new Dictionary<string, IQuestAction>();
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActionObjectiveBase>();

        private QuizSystem quizSystem;

        private QuestNetworkHandler networkHandler;
        private QuestPersistenceManager persistenceManager;
        private QuestLifecycleManager lifecycleManager;
        private QuestEventHandler eventHandler;
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

        private long lagMonitorListenerId;

        public QuizSystem QuizSystem => quizSystem;
        public QuestNetworkHandler NetworkHandler => networkHandler;

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
            Config = LoadOrCreateModConfig<QuestConfig>(api, "questconfig.json");
            CoreConfig = LoadOrCreateModConfig<AlegacyVsQuestConfig>(api, "alegacy-vsquest-config.json");
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

            networkHandler = new QuestNetworkHandler(this);

            /* QuestSystem cache (used by other subsystems to safely resolve the active instance). */
            QuestSystemCache.Initialize(api);

            MobLocalizationUtils.LoadFromAssets(api);

            quizSystem = new QuizSystem(this);

            var harmony = new HarmonyLib.Harmony("alegacyvsquest");
            /* Centralized patch entry-point; individual patches decide whether they activate. */
            harmony.PatchAll();
            
            // Set API reference for ItemMergeChargePatches logging
            VsQuest.Harmony.Items.ItemMergeChargePatches.API = api;

            LocalizationUtils.LoadFromAssets(api);
            LocalizationUtils.LoadNestedLanguageFiles(api);

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

            LocalizationUtils.LoadFromAssets(capi);
            LocalizationUtils.LoadNestedLanguageFiles(capi);

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
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            QuestSystemCache.Initialize(sapi);

            sapi.Logger.VerboseDebug($"[alegacyvsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            /* Server-only wiring: persistence + lifecycle logic + event subscriptions. */
            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            if (networkChannelRegistry == null)
            {
                sapi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartServerSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterServer(sapi);

            /* Registers quest actions and binds acceptance to lifecycleManager. */
            actionRegistry = new QuestActionRegistry(ActionRegistry, api, sapi, OnQuestAccepted);
            actionRegistry.Register();

            eventHandler.RegisterEventHandlers();

            chatCommandRegistry = new QuestChatCommandRegistry(sapi, api, this);
            chatCommandRegistry.Register();
        }

        public override void Dispose()
        {

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

            if (QuestRegistry.ContainsKey(quest.id)) return;

            QuestRegistry.Add(quest.id, quest);
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

        #region Packet Handler Delegations

        // TODO: [Architecture] Packet handler methods below are simple 1-2 line delegations to
        // subsystem managers (lifecycleManager, eventHandler, quizSystem, etc.). If they grow
        // in complexity, extract into dedicated handler classes under src/Systems/Handlers/:
        //   - QuestPacketHandler.cs  (OnQuestAccepted, OnQuestCompleted, OnQuestInfoMessage)
        //   - DialogPacketHandler.cs (OnShowQuestDialogMessage, OnDialogTriggerMessage)
        //   - QuizPacketHandler.cs   (OnShowQuizMessage, OnOpenQuizMessage, OnSubmitQuizAnswerMessage)
        // Each handler would accept its required dependencies in the constructor.

        internal void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestAccepted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestCompleted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            questSelectGuiManager.HandleQuestInfoMessage(message, capi);
        }

        internal void OnShowServerInfoMessage(ShowServerInfoMessage message, ICoreClientAPI capi)
        {
            serverInfoGuiManager.HandleShowServerInfoMessage(message, capi);
        }

        internal void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleNotificationMessage(message, capi);
        }

        internal void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleDiscoveryMessage(message, capi);
        }

        internal void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            ClientCommandExecutor.Execute(message, capi);
        }

        internal void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            eventHandler.HandleVanillaBlockInteract(player, message);
        }

        internal void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            QuestFinalDialogGui.ShowFromMessage(message, capi);
        }

        internal void OnShowQuizMessage(ShowQuizMessage message, ICoreClientAPI capi)
        {
            quizSystem?.OnShowQuizMessage(message, capi);
        }

        internal void OnOpenQuizMessage(IServerPlayer player, OpenQuizMessage message, ICoreServerAPI sapi)
        {
            quizSystem?.OnOpenQuizMessage(player, message, sapi);
        }

        internal void OnSubmitQuizAnswerMessage(IServerPlayer player, SubmitQuizAnswerMessage message, ICoreServerAPI sapi)
        {
            quizSystem?.OnSubmitQuizAnswerMessage(player, message, sapi);
        }

        internal void OnPreloadBossMusicMessage(PreloadBossMusicMessage message, ICoreClientAPI capi)
        {
            try
            {
                /* Optional integration: preload only if the music subsystem is present client-side. */
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                sys?.Preload(message?.Url);
            }
            catch
            {
                api.Logger.Error("[alegacyvsquest] Failed to preload boss music: {0}", message?.Url);
            }
        }

        internal void OnDialogTriggerMessage(IServerPlayer player, DialogTriggerMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.Trigger)) return;
            if (message.EntityId <= 0) return;

            /* Reuse the action execution pipeline by wrapping dialog triggers as a synthetic quest accept. */
            var qm = new QuestAcceptedMessage { questGiverId = message.EntityId, questId = "dialog-action" };
            ActionStringExecutor.Execute(sapi, qm, player, message.Trigger);
        }

        internal void OnClaimReputationRewardsMessage(IServerPlayer player, ClaimReputationRewardsMessage message, ICoreServerAPI sapi)
        {
            var repSystem = sapi?.ModLoader?.GetModSystem<ReputationSystem>();
            repSystem?.OnClaimReputationRewardsMessage(player, message, sapi);
        }

        internal void OnClaimQuestCompletionRewardMessage(IServerPlayer player, ClaimQuestCompletionRewardMessage message, ICoreServerAPI sapi)
        {
            var rewardSystem = sapi?.ModLoader?.GetModSystem<QuestCompletionRewardSystem>();
            rewardSystem?.OnClaimQuestCompletionRewardMessage(player, message, sapi);
        }

        #endregion
    }
}
