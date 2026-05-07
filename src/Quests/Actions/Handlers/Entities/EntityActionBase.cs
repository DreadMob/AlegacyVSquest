using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public abstract class EntityActionBase : IQuestAction
    {
        protected abstract int MinArgs { get; }
        protected abstract string ActionName { get; }

        public virtual void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (args != null && args.Length < MinArgs)
            {
                sapi.Logger.Error($"[vsquest] '{ActionName}' action requires {MinArgs} arguments but got {args.Length} in quest '{message?.questId}'.");
                return;
            }

            ExecuteAction(sapi, message, player, args);
        }

        protected abstract void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
    }
}
