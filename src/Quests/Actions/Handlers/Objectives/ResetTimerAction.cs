using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Resets/starts the timer for a timer objective.
    /// Args: [0] questId, [1] objectiveId
    /// </summary>
    public class ResetTimerAction : PlayerActionBase
    {
        protected override int MinArgs => 2;
        protected override string ActionName => "resettimer";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            if (player == null || args == null || args.Length < 2) return;

            string questId = args[0];
            string objectiveId = args[1];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return;

            TimerObjective.StartTimer(player, questId, objectiveId, sapi);
        }
    }
}
