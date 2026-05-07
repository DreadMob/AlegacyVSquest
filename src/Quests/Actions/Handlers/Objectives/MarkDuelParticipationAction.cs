using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Marks duel participation in watched attributes for quest tracking
    /// Args: [questId]
    /// Increments alegacy_duel_participations counter
    /// </summary>
    public class MarkDuelParticipationAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "markduelparticipation";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = "alegacy_duel_participations";
            int current = wa.GetInt(key, 0);
            wa.SetInt(key, current + 1);
            wa.MarkPathDirty(key);
        }
    }
}
