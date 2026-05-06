using System;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class VsQuestNetworkRegistry
    {
        public const string QuestChannelName = "alegacyvsquest";
        public const string ItemActionChannelName = "alegacyvsquest-itemaction";
        public const string BossMusicChannelName = "alegacyvsquestmusic";

        #region Client Registration

        public static void RegisterQuestClient(ICoreClientAPI capi, QuestNetworkHandler handler)
        {
            // Registration order must be identical on client and server for protocol compatibility.
            var channel = capi.Network.RegisterChannel(QuestChannelName);
            channel = RegisterQuestClientMessages(channel, handler, capi);
            channel = RegisterDialogClientMessages(channel, handler, capi);
            channel = RegisterQuizClientMessages(channel, handler, capi);
            channel = RegisterRewardClientMessages(channel, handler, capi);
            channel = RegisterBossMusicClientMessages(channel, handler, capi);
        }

        /// <summary>Quest lifecycle messages: accept, complete, info.</summary>
        private static IClientNetworkChannel RegisterQuestClientMessages(IClientNetworkChannel channel, QuestNetworkHandler handler, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => handler.OnQuestInfoMessage(message, capi));
        }

        /// <summary>UI/dialog messages: server info, commands, notifications, block interaction, dialog triggers.</summary>
        private static IClientNetworkChannel RegisterDialogClientMessages(IClientNetworkChannel channel, QuestNetworkHandler handler, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ShowServerInfoMessage>().SetMessageHandler<ShowServerInfoMessage>(message => handler.OnShowServerInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => handler.OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => handler.OnShowNotificationMessage(message, capi))
                .RegisterMessageType<ShowDiscoveryMessage>().SetMessageHandler<ShowDiscoveryMessage>(message => handler.OnShowDiscoveryMessage(message, capi))
                .RegisterMessageType<ShowQuestDialogMessage>().SetMessageHandler<ShowQuestDialogMessage>(message => handler.OnShowQuestDialogMessage(message, capi))
                .RegisterMessageType<DialogTriggerMessage>();
        }

        /// <summary>Quiz system messages: show, submit, open.</summary>
        private static IClientNetworkChannel RegisterQuizClientMessages(IClientNetworkChannel channel, QuestNetworkHandler handler, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ShowQuizMessage>().SetMessageHandler<ShowQuizMessage>(message => handler.OnShowQuizMessage(message, capi))
                .RegisterMessageType<SubmitQuizAnswerMessage>()
                .RegisterMessageType<OpenQuizMessage>();
        }

        /// <summary>Reward claim messages: reputation and quest completion rewards.</summary>
        private static IClientNetworkChannel RegisterRewardClientMessages(IClientNetworkChannel channel, QuestNetworkHandler handler, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ClaimReputationRewardsMessage>()
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>();
        }

        /// <summary>Boss music preload message (client-side handler).</summary>
        private static IClientNetworkChannel RegisterBossMusicClientMessages(IClientNetworkChannel channel, QuestNetworkHandler handler, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<PreloadBossMusicMessage>().SetMessageHandler<PreloadBossMusicMessage>(message => handler.OnPreloadBossMusicMessage(message, capi));
        }

        #endregion

        #region Server Registration

        public static void RegisterQuestServer(ICoreServerAPI sapi, QuestNetworkHandler handler)
        {
            // Registration order must be identical on client and server for protocol compatibility.
            var channel = sapi.Network.RegisterChannel(QuestChannelName);
            channel = RegisterQuestServerMessages(channel, handler, sapi);
            channel = RegisterDialogServerMessages(channel, handler, sapi);
            channel = RegisterQuizServerMessages(channel, handler, sapi);
            channel = RegisterRewardServerMessages(channel, handler, sapi);
            channel = RegisterBossMusicServerMessages(channel, handler, sapi);
        }

        /// <summary>Quest lifecycle messages: accept, complete, info.</summary>
        private static IServerNetworkChannel RegisterQuestServerMessages(IServerNetworkChannel channel, QuestNetworkHandler handler, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => handler.OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => handler.OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>();
        }

        /// <summary>UI/dialog messages: server info, commands, notifications, block interaction, dialog triggers.</summary>
        private static IServerNetworkChannel RegisterDialogServerMessages(IServerNetworkChannel channel, QuestNetworkHandler handler, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ShowServerInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => handler.OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>()
                .RegisterMessageType<ShowDiscoveryMessage>()
                .RegisterMessageType<ShowQuestDialogMessage>()
                .RegisterMessageType<DialogTriggerMessage>().SetMessageHandler<DialogTriggerMessage>((player, message) => handler.OnDialogTriggerMessage(player, message, sapi));
        }

        /// <summary>Quiz system messages: show, submit, open.</summary>
        private static IServerNetworkChannel RegisterQuizServerMessages(IServerNetworkChannel channel, QuestNetworkHandler handler, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ShowQuizMessage>()
                .RegisterMessageType<SubmitQuizAnswerMessage>().SetMessageHandler<SubmitQuizAnswerMessage>((player, message) => handler.OnSubmitQuizAnswerMessage(player, message, sapi))
                .RegisterMessageType<OpenQuizMessage>().SetMessageHandler<OpenQuizMessage>((player, message) => handler.OnOpenQuizMessage(player, message, sapi));
        }

        /// <summary>Reward claim messages: reputation and quest completion rewards.</summary>
        private static IServerNetworkChannel RegisterRewardServerMessages(IServerNetworkChannel channel, QuestNetworkHandler handler, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ClaimReputationRewardsMessage>().SetMessageHandler<ClaimReputationRewardsMessage>((player, message) => handler.OnClaimReputationRewardsMessage(player, message, sapi))
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>().SetMessageHandler<ClaimQuestCompletionRewardMessage>((player, message) => handler.OnClaimQuestCompletionRewardMessage(player, message, sapi));
        }

        /// <summary>Boss music preload message (server-side, no handler).</summary>
        private static IServerNetworkChannel RegisterBossMusicServerMessages(IServerNetworkChannel channel, QuestNetworkHandler handler, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<PreloadBossMusicMessage>();
        }

        #endregion

        #region Item Action Channel

        public static IServerNetworkChannel RegisterItemActionServer(ICoreServerAPI sapi, ActionItemPacketHandler packetHandler)
        {
            return sapi.Network.RegisterChannel(ItemActionChannelName)
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(packetHandler.HandlePacket);
        }

        public static IClientNetworkChannel RegisterItemActionClient(ICoreClientAPI capi)
        {
            return capi.Network.RegisterChannel(ItemActionChannelName)
                .RegisterMessageType<ExecuteActionItemPacket>();
        }

        #endregion

        #region Boss Music Channel

        public static IClientNetworkChannel RegisterBossMusicClient(ICoreClientAPI capi, NetworkServerMessageHandler<BossMusicUrlMapMessage> handler)
        {
            return capi.Network.RegisterChannel(BossMusicChannelName)
                .RegisterMessageType<BossMusicUrlMapMessage>()
                .SetMessageHandler<BossMusicUrlMapMessage>(handler);
        }

        #endregion
    }
}
