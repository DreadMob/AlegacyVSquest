using Vintagestory.API.Server;

namespace VsQuest
{
    public class ResetWalkDistanceQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "resetwalkdistance";

        public override void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            // We override Execute to pass message.questId if args are empty
            if (player == null) return;
            ExecuteInternal(sapi, message?.questId, player, args);
        }

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Not used due to override
        }

        private void ExecuteInternal(ICoreServerAPI api, string messageQuestId, IServerPlayer player, string[] args)
        {
            string questId = null;
            if (args != null && args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0])) questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) questId = messageQuestId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            int slots = 1;
            if (args != null && args.Length >= 2 && int.TryParse(args[1], out int parsedSlots)) slots = parsedSlots;
            if (slots < 1) slots = 1;
            if (slots > 32) slots = 32;

            var wa = player.Entity.WatchedAttributes;

            for (int slot = 0; slot < slots; slot++)
            {
                string haveKey = WalkDistanceObjective.HaveKey(questId, slot);

                wa.SetFloat(haveKey, 0f);
                wa.MarkPathDirty(haveKey);
            }

            WalkDistanceObjective.ClearPlayerCache(player.PlayerUID);
        }
    }
}
