using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnSmokeAction : EntityActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "spawnsmoke";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            SimpleParticleProperties smoke = new SimpleParticleProperties(
                    40, 60,
                    ColorUtil.ToRgba(80, 100, 100, 100),
                    new Vec3d(),
                    new Vec3d(2, 1, 2),
                    new Vec3f(-0.25f, 0f, -0.25f),
                    new Vec3f(0.25f, 0f, 0.25f),
                    0.6f,
                    -0.075f,
                    0.5f,
                    3f,
                    EnumParticleModel.Quad
                );

            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            if (questgiver == null)
            {
                throw new QuestException($"Could not find quest giver with id {message.questGiverId} to spawn smoke for quest {message.questId}");
            }
            smoke.MinPos = questgiver.Pos.XYZ.AddCopy(-1.5, -0.5, -1.5);
            sapi.World.SpawnParticles(smoke);
        }
    }
}
