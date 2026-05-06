using Vintagestory.API.Server;

namespace VsQuest
{
    public abstract class PlayerActionBase : IQuestAction
    {
        protected abstract int MinArgs { get; }
        protected abstract string ActionName { get; }

        public virtual void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args != null && args.Length < MinArgs)
            {
                sapi.Logger.Error($"[vsquest] '{ActionName}' action requires at least {MinArgs} arguments but got {args.Length} in quest '{message?.questId}'.");
                return;
            }

            if (byPlayer == null) return;

            Execute(sapi, byPlayer, args);
        }

        protected abstract void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args);
    }
}
