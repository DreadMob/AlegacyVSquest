using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnEntitiesAtPlayerAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "spawnentitiesatplayer";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer?.Entity == null) return;

            var spawnPos = byPlayer.Entity.Pos.Copy();
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException($"Tried to spawn {code} for quest {message.questId} but could not find the entity type!");
                }

                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null) continue;

                entity.Pos.SetFrom(spawnPos);
                sapi.World.SpawnEntity(entity);
            }
        }
    }
}
