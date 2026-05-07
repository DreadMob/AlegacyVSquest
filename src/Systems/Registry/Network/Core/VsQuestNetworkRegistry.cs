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

        public static void RegisterQuestClient(ICoreClientAPI capi, QuestSystem questSystem)
        {
            // Registration order must be identical on client and server for protocol compatibility.
            var channel = capi.Network.RegisterChannel(QuestChannelName);
            channel = RegisterQuestClientMessages(channel, questSystem, capi);
            channel = RegisterDialogClientMessages(channel, questSystem, capi);
            channel = RegisterQuizClientMessages(channel, questSystem, capi);
            channel = RegisterRewardClientMessages(channel, questSystem, capi);
            channel = RegisterBossMusicClientMessages(channel, questSystem, capi);
        }

        /// <summary>Quest lifecycle messages: accept, complete, info.</summary>
        private static IClientNetworkChannel RegisterQuestClientMessages(IClientNetworkChannel channel, QuestSystem questSystem, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => questSystem.OnQuestInfoMessage(message, capi));
        }

        /// <summary>UI/dialog messages: server info, commands, notifications, block interaction, dialog triggers.</summary>
        private static IClientNetworkChannel RegisterDialogClientMessages(IClientNetworkChannel channel, QuestSystem questSystem, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ShowServerInfoMessage>().SetMessageHandler<ShowServerInfoMessage>(message => questSystem.OnShowServerInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => questSystem.OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => questSystem.OnShowNotificationMessage(message, capi))
                .RegisterMessageType<ShowDiscoveryMessage>().SetMessageHandler<ShowDiscoveryMessage>(message => questSystem.OnShowDiscoveryMessage(message, capi))
                .RegisterMessageType<ShowQuestDialogMessage>().SetMessageHandler<ShowQuestDialogMessage>(message => questSystem.OnShowQuestDialogMessage(message, capi))
                .RegisterMessageType<DialogTriggerMessage>();
        }

        /// <summary>Quiz system messages: show, submit, open.</summary>
        private static IClientNetworkChannel RegisterQuizClientMessages(IClientNetworkChannel channel, QuestSystem questSystem, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ShowQuizMessage>().SetMessageHandler<ShowQuizMessage>(message => questSystem.OnShowQuizMessage(message, capi))
                .RegisterMessageType<SubmitQuizAnswerMessage>()
                .RegisterMessageType<OpenQuizMessage>();
        }

        /// <summary>Reward claim messages: reputation and quest completion rewards.</summary>
        private static IClientNetworkChannel RegisterRewardClientMessages(IClientNetworkChannel channel, QuestSystem questSystem, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<ClaimReputationRewardsMessage>()
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>();
        }

        /// <summary>Boss music preload message (client-side handler).</summary>
        private static IClientNetworkChannel RegisterBossMusicClientMessages(IClientNetworkChannel channel, QuestSystem questSystem, ICoreClientAPI capi)
        {
            return channel
                .RegisterMessageType<PreloadBossMusicMessage>().SetMessageHandler<PreloadBossMusicMessage>(message => questSystem.OnPreloadBossMusicMessage(message, capi));
        }

        #endregion

        #region Server Registration

        public static void RegisterQuestServer(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            // Registration order must be identical on client and server for protocol compatibility.
            var channel = sapi.Network.RegisterChannel(QuestChannelName);
            channel = RegisterQuestServerMessages(channel, questSystem, sapi);
            channel = RegisterDialogServerMessages(channel, questSystem, sapi);
            channel = RegisterQuizServerMessages(channel, questSystem, sapi);
            channel = RegisterRewardServerMessages(channel, questSystem, sapi);
            channel = RegisterBossMusicServerMessages(channel, questSystem, sapi);
        }

        /// <summary>Quest lifecycle messages: accept, complete, info.</summary>
        private static IServerNetworkChannel RegisterQuestServerMessages(IServerNetworkChannel channel, QuestSystem questSystem, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => questSystem.OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => questSystem.OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>();
        }

        /// <summary>UI/dialog messages: server info, commands, notifications, block interaction, dialog triggers.</summary>
        private static IServerNetworkChannel RegisterDialogServerMessages(IServerNetworkChannel channel, QuestSystem questSystem, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ShowServerInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => questSystem.OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>()
                .RegisterMessageType<ShowDiscoveryMessage>()
                .RegisterMessageType<ShowQuestDialogMessage>()
                .RegisterMessageType<DialogTriggerMessage>().SetMessageHandler<DialogTriggerMessage>((player, message) => questSystem.OnDialogTriggerMessage(player, message, sapi));
        }

        /// <summary>Quiz system messages: show, submit, open.</summary>
        private static IServerNetworkChannel RegisterQuizServerMessages(IServerNetworkChannel channel, QuestSystem questSystem, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ShowQuizMessage>()
                .RegisterMessageType<SubmitQuizAnswerMessage>().SetMessageHandler<SubmitQuizAnswerMessage>((player, message) => questSystem.OnSubmitQuizAnswerMessage(player, message, sapi))
                .RegisterMessageType<OpenQuizMessage>().SetMessageHandler<OpenQuizMessage>((player, message) => questSystem.OnOpenQuizMessage(player, message, sapi));
        }

        /// <summary>Reward claim messages: reputation and quest completion rewards.</summary>
        private static IServerNetworkChannel RegisterRewardServerMessages(IServerNetworkChannel channel, QuestSystem questSystem, ICoreServerAPI sapi)
        {
            return channel
                .RegisterMessageType<ClaimReputationRewardsMessage>().SetMessageHandler<ClaimReputationRewardsMessage>((player, message) => questSystem.OnClaimReputationRewardsMessage(player, message, sapi))
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>().SetMessageHandler<ClaimQuestCompletionRewardMessage>((player, message) => questSystem.OnClaimQuestCompletionRewardMessage(player, message, sapi));
        }

        /// <summary>Boss music preload message (server-side, no handler).</summary>
        private static IServerNetworkChannel RegisterBossMusicServerMessages(IServerNetworkChannel channel, QuestSystem questSystem, ICoreServerAPI sapi)
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
