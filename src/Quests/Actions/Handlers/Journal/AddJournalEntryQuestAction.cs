using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddJournalEntryQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "addjournalentry";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
