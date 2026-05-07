using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class OpenQuestsAction : EntityActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "openquests";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questGiver = sapi.World.GetEntityById(message.questGiverId);
            if (questGiver == null) return;

            var questGiverBehavior = questGiver.GetBehavior<EntityBehaviorQuestGiver>();
            if (questGiverBehavior == null) return;

            if (byPlayer.Entity is EntityPlayer entityPlayer)
            {
                questGiverBehavior.SendQuestInfoMessageToClient(sapi, entityPlayer);
            }
        }
    }
}
