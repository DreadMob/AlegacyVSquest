using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Resets progress for experimental objectives (mineblock, placeblock, etc).
    /// Args: [0] objectiveType (mineblock|placeblock|harvestcrop|fishcatch|killwithweapon|craftitem|smeltitem)
    ///       [1] questId
    ///       [2] objectiveId
    /// </summary>
    public class ResetExperimentalObjectiveAction : PlayerActionBase
    {
        protected override int MinArgs => 3;
        protected override string ActionName => "resetexperimentalobjective";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            if (player == null || args == null || args.Length < 3) return;

            string objectiveType = args[0];
            string questId = args[1];
            string objectiveId = args[2];

            if (string.IsNullOrWhiteSpace(objectiveType) || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
                return;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string key = objectiveType.ToLowerInvariant() switch
            {
                "mineblock" => MineBlockObjective.HaveKey(questId, objectiveId),
                "placeblock" => PlaceBlockObjective.HaveKey(questId, objectiveId),
                "harvestcrop" => HarvestCropObjective.HaveKey(questId, objectiveId),
                "fishcatch" => FishCatchObjective.HaveKey(questId, objectiveId),
                "killwithweapon" => KillWithWeaponObjective.HaveKey(questId, objectiveId),
                "craftitem" => CraftItemObjective.HaveKey(questId, objectiveId),
                "smeltitem" => SmeltItemObjective.HaveKey(questId, objectiveId),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(key)) return;

            wa.SetInt(key, 0);
            wa.MarkPathDirty(key);
        }
    }
}
