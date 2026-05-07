using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestStartCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestStartCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string questId = (string)args[0];
            string playerName = (string)args[1];

            if (!questSystem.QuestRegistry.ContainsKey(questId))
            {
                return TextCommandResult.Error($"Quest '{questId}' not found.");
            }

            var target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            long questGiverId = 0;
            try
            {
                var hub = sapi.World.LoadedEntities?.Values
                    ?.FirstOrDefault(e => e != null && e.Code != null && e.Code.Path != null && e.Code.Path.Equals("hub", StringComparison.OrdinalIgnoreCase));

                if (hub != null)
                {
                    questGiverId = hub.EntityId;
                }
            }
            catch (Exception ex)
            {
                sapi?.Logger?.Error($"[vsquest] Exception in finding hub: {ex}");
            }

            var msg = new QuestAcceptedMessage()
            {
                questGiverId = questGiverId,
                questId = questId
            };

            questSystem.QuestPacketHandler.OnQuestAccepted(target, msg, sapi);
            questSystem.SavePlayerQuests(target.PlayerUID, questSystem.GetPlayerQuests(target.PlayerUID));

            return TextCommandResult.Success($"Quest '{questId}' was started for '{target.PlayerName}'.");
        }
    }
}
