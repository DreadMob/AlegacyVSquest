using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNetworkHandler
    {
        private readonly QuestSystem questSystem;

        public QuestNetworkHandler(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            questSystem.OnQuestInfoMessage(message, capi);
        }

        public void OnShowServerInfoMessage(ShowServerInfoMessage message, ICoreClientAPI capi)
        {
            questSystem.OnShowServerInfoMessage(message, capi);
        }

        public void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            questSystem.OnShowNotificationMessage(message, capi);
        }

        public void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            questSystem.OnShowDiscoveryMessage(message, capi);
        }

        public void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            questSystem.OnExecutePlayerCommand(message, capi);
        }

        public void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            questSystem.OnShowQuestDialogMessage(message, capi);
        }

        public void OnShowQuizMessage(ShowQuizMessage message, ICoreClientAPI capi)
        {
            questSystem.OnShowQuizMessage(message, capi);
        }

        public void OnPreloadBossMusicMessage(PreloadBossMusicMessage message, ICoreClientAPI capi)
        {
            questSystem.OnPreloadBossMusicMessage(message, capi);
        }

        public void OnQuestAccepted(IServerPlayer player, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnQuestAccepted(player, message, sapi);
        }

        public void OnQuestCompleted(IServerPlayer player, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnQuestCompleted(player, message, sapi);
        }

        public void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnVanillaBlockInteract(player, message, sapi);
        }

        public void OnSubmitQuizAnswerMessage(IServerPlayer player, SubmitQuizAnswerMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnSubmitQuizAnswerMessage(player, message, sapi);
        }

        public void OnOpenQuizMessage(IServerPlayer player, OpenQuizMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnOpenQuizMessage(player, message, sapi);
        }

        public void OnDialogTriggerMessage(IServerPlayer player, DialogTriggerMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnDialogTriggerMessage(player, message, sapi);
        }

        public void OnClaimReputationRewardsMessage(IServerPlayer player, ClaimReputationRewardsMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnClaimReputationRewardsMessage(player, message, sapi);
        }

        public void OnClaimQuestCompletionRewardMessage(IServerPlayer player, ClaimQuestCompletionRewardMessage message, ICoreServerAPI sapi)
        {
            questSystem.OnClaimQuestCompletionRewardMessage(player, message, sapi);
        }
    }
}
