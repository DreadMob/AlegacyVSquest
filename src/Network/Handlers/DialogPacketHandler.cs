using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

        public void OnShowRerollDialogMessage(ShowRerollDialogMessage message, ICoreClientAPI capi)
        {
            // Close any open NPC dialog before showing reroll dialog
            CloseAllDialogs(capi);
            RerollDialogGui.ShowFromMessage(message, capi);
        }

        private void CloseAllDialogs(ICoreClientAPI capi)
        {
            try
            {
                // Close all opened GuiDialogs
                var guis = capi.Gui.OpenedGuis?.ToArray();
                if (guis != null)
                {
                    foreach (var gui in guis)
                    {
                        if (gui is GuiDialog dialog && dialog.IsOpened())
                        {
                            dialog.TryClose();
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when closing dialogs
            }
        }

        public void OnStartRerollAnimationMessage(StartRerollAnimationMessage message, ICoreClientAPI capi)
        {
            RerollAnimationGui.ShowFromMessage(message, capi);
        }

        public void OnExecuteRerollMessage(IServerPlayer player, ExecuteRerollMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;
            if (string.IsNullOrWhiteSpace(message.GroupId)) return;

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var rerollService = itemSystem?.RerollService;
            if (rerollService == null) return;

            var result = rerollService.ExecuteReroll(player, message.GroupId);
            if (result.Success)
            {
                // Send animation message to client
                sapi.Network.GetChannel(VsQuestNetworkRegistry.RerollChannelName).SendPacket(new StartRerollAnimationMessage
                {
                    ItemIds = result.AllItemIds,
                    ItemNames = result.AllItemNames,
                    ItemCodes = result.AllItemCodes,
                    ResultItemId = result.ResultItemId,
                    ResultItemName = result.ResultItemName,
                    ResultItemCode = result.ResultItemCode,
                    AnimationType = result.AnimationType,
                    GroupId = result.GroupId
                }, player);
                // Chat message will be sent when player claims the reward
            }
            else
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, 
                    LocalizationUtils.GetSafe("alegacyvsquest:reroll-failed"), 
                    EnumChatType.Notification);
            }
        }

        public void OnClaimRerollRewardMessage(IServerPlayer player, ClaimRerollRewardMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null) return;

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var rerollService = itemSystem?.RerollService;
            if (rerollService == null) return;

            var reward = rerollService.ClaimReward(player);
            if (reward != null)
            {
                // Broadcast to all players (like quest completion)
                string playerName = ChatFormatUtil.Font(player.PlayerName, "#ffd75e");
                string itemName = ChatFormatUtil.Font(reward.RewardItemName, "#77ddff");
                string text = ChatFormatUtil.PrefixAlert($"{playerName} обрёл дар судьбы: {itemName}");

                // Discord message
                string discordText = LocalizationUtils.GetSafe("alegacyvsquest:discord-reroll");
                if (string.IsNullOrWhiteSpace(discordText) || string.Equals(discordText, "alegacyvsquest:discord-reroll", StringComparison.OrdinalIgnoreCase))
                {
                    discordText = $"{player.PlayerName} обрёл дар судьбы: {reward.RewardItemName}";
                }
                else
                {
                    discordText = discordText.Replace("{0}", player.PlayerName).Replace("{1}", reward.RewardItemName);
                }

                GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                    sapi, text, discordText, EnumChatType.Notification, DiscordBroadcastKind.Reroll);
            }
        }
    }
}
