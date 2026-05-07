using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles quest lifecycle packet messages: accept, complete, info.
    /// </summary>
    public class QuestPacketHandler
    {
        private readonly IQuestLifecycleManager lifecycleManager;
        private readonly QuestSelectGuiManager questSelectGuiManager;
        private readonly Func<string, List<ActiveQuest>> getPlayerQuests;

        public QuestPacketHandler(
            IQuestLifecycleManager lifecycleManager,
            QuestSelectGuiManager questSelectGuiManager,
            Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            this.lifecycleManager = lifecycleManager;
            this.questSelectGuiManager = questSelectGuiManager;
            this.getPlayerQuests = getPlayerQuests;
        }

        // Server-side handlers

        public void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestAccepted(fromPlayer, message, sapi, getPlayerQuests);
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestCompleted(fromPlayer, message, sapi, getPlayerQuests);
        }

        // Client-side handlers

        public void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            questSelectGuiManager.HandleQuestInfoMessage(message, capi);
        }
    }
}
