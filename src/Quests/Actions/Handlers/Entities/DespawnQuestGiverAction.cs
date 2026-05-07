using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DespawnQuestGiverAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "despawnquestgiver";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (int.TryParse(args[0], out int delayMs))
            {
                sapi.World.RegisterCallback(dt => sapi.World.GetEntityById(message.questGiverId)?.Die(EnumDespawnReason.Removed), delayMs);
            }
            else
            {
                sapi.World.GetEntityById(message.questGiverId)?.Die(EnumDespawnReason.Removed);
            }
        }
    }
}
