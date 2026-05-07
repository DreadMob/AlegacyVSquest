using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnEntitiesAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "spawnentities";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException($"Tried to spawn {code} for quest {message.questId} but could not find the entity type!");
                }
                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                var questGiver = sapi.World.GetEntityById(message.questGiverId);
                if (questGiver != null)
                {
                    entity.Pos.SetFrom(questGiver.Pos);
                }
                sapi.World.SpawnEntity(entity);
            }
        }
    }
}
