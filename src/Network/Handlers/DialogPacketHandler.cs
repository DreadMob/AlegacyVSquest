using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles dialog and UI packet messages: server info, notifications, commands, block interaction, dialog triggers, boss music.
    /// </summary>
    public class DialogPacketHandler
    {
        private readonly QuestEventHandler eventHandler;
        private readonly QuestNotificationHandler notificationHandler;
        private readonly ServerInfoGuiManager serverInfoGuiManager;
        private readonly ICoreAPI api;

        public DialogPacketHandler(
            QuestEventHandler eventHandler,
            QuestNotificationHandler notificationHandler,
            ServerInfoGuiManager serverInfoGuiManager,
            ICoreAPI api)
        {
            this.eventHandler = eventHandler;
            this.notificationHandler = notificationHandler;
            this.serverInfoGuiManager = serverInfoGuiManager;
            this.api = api;
        }

        // Server-side handlers

        public void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            eventHandler.HandleVanillaBlockInteract(player, message);
        }

        public void OnDialogTriggerMessage(IServerPlayer player, DialogTriggerMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.Trigger)) return;
            if (message.EntityId <= 0) return;

            /* Reuse the action execution pipeline by wrapping dialog triggers as a synthetic quest accept. */
            var qm = new QuestAcceptedMessage { questGiverId = message.EntityId, questId = "dialog-action" };
            ActionStringExecutor.Execute(sapi, qm, player, message.Trigger);
        }

        public void OnClaimReputationRewardsMessage(IServerPlayer player, ClaimReputationRewardsMessage message, ICoreServerAPI sapi)
        {
            var repSystem = sapi?.ModLoader?.GetModSystem<ReputationSystem>();
            repSystem?.OnClaimReputationRewardsMessage(player, message, sapi);
        }

        public void OnClaimQuestCompletionRewardMessage(IServerPlayer player, ClaimQuestCompletionRewardMessage message, ICoreServerAPI sapi)
        {
            var rewardSystem = sapi?.ModLoader?.GetModSystem<QuestCompletionRewardSystem>();
            rewardSystem?.OnClaimQuestCompletionRewardMessage(player, message, sapi);
        }

        // Client-side handlers

        public void OnShowServerInfoMessage(ShowServerInfoMessage message, ICoreClientAPI capi)
        {
            serverInfoGuiManager.HandleShowServerInfoMessage(message, capi);
        }

        public void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleNotificationMessage(message, capi);
        }

        public void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleDiscoveryMessage(message, capi);
        }

        public void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            ClientCommandExecutor.Execute(message, capi);
        }

        public void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            QuestFinalDialogGui.ShowFromMessage(message, capi);
        }

        public void OnPreloadBossMusicMessage(PreloadBossMusicMessage message, ICoreClientAPI capi)
        {
            try
            {
                /* Optional integration: preload only if the music subsystem is present client-side. */
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                sys?.Preload(message?.Url);
            }
            catch (Exception ex)
            {
                api.Logger.Error("[alegacyvsquest] Failed to preload boss music '{0}': {1}", message?.Url, ex.Message);
            }
        }
    }
}
