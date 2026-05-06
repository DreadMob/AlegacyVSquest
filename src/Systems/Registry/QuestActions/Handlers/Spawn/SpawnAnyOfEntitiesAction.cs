using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnAnyOfEntitiesAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "spawnanyofentities";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var code = args[sapi.World.Rand.Next(0, args.Length)];
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
