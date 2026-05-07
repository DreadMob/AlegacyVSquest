using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossClearCorpseCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public BossClearCorpseCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            int radiusBlocks = 0;
            try
            {
                if (args?.Parsers != null && args.Parsers.Count > 0)
                {
                    radiusBlocks = (int)args.Parsers[0].GetValue();
                }
            }
            catch
            {
                radiusBlocks = 0;
            }

            if (sapi?.World?.LoadedEntities == null)
            {
                return TextCommandResult.Error("World entities not available.");
            }

            Vec3d callerPos = null;
            int callerDim = 0;
            if (args.Caller?.Entity?.Pos != null)
            {
                callerPos = args.Caller.Entity.Pos.XYZ;
                callerDim = args.Caller.Entity.Pos.Dimension;
            }

            int clearedCount = 0;
            double radiusSq = radiusBlocks > 0 ? radiusBlocks * (double)radiusBlocks : 0;

            try
            {
                foreach (var entity in sapi.World.LoadedEntities.Values)
                {
                    if (entity == null) continue;
                    if (entity.Alive) continue;

                    var bossMarker = entity.GetBehavior<EntityBehaviorBossCombatMarker>();
                    if (bossMarker == null) continue;

                    if (radiusBlocks > 0 && callerPos != null)
                    {
                        if (entity.Pos?.Dimension != callerDim) continue;

                        double dx = entity.Pos.X - callerPos.X;
                        double dy = entity.Pos.Y - callerPos.Y;
                        double dz = entity.Pos.Z - callerPos.Z;

                        if (dx * dx + dy * dy + dz * dz > radiusSq) continue;
                    }

                    try
                    {
                        sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        clearedCount++;
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[BossClearCorpseCommandHandler] Failed to despawn boss corpse: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossClearCorpseCommandHandler] Failed to clear boss corpses: {0}", ex.Message);
            }

            string radiusMsg = radiusBlocks > 0 ? $" within {radiusBlocks} blocks" : " in all loaded chunks";
            return TextCommandResult.Success($"Cleared {clearedCount} boss corpse(s){radiusMsg}.");
        }
    }
}
