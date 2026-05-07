using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class RandomKillObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return false;
            string questId = args[0];
            var wa = byPlayer.Entity.WatchedAttributes;

            int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(questId), 0);
            if (slots <= 0) return false;

            for (int slot = 0; slot < slots; slot++)
            {
                int need = wa.GetInt(RandomKillQuestUtils.SlotNeedKey(questId, slot), 0);
                int have = wa.GetInt(RandomKillQuestUtils.SlotHaveKey(questId, slot), 0);
                if (have < need) return false;
            }

            return true;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return new List<int> { 0, 0 };
            string questId = args[0];
            var wa = byPlayer.Entity.WatchedAttributes;

            int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(questId), 0);
            if (slots <= 0) return new List<int> { 0, 0 };

            int totalHave = 0;
            int totalNeed = 0;

            for (int slot = 0; slot < slots; slot++)
            {
                totalNeed += wa.GetInt(RandomKillQuestUtils.SlotNeedKey(questId, slot), 0);
                totalHave += wa.GetInt(RandomKillQuestUtils.SlotHaveKey(questId, slot), 0);
            }

            return new List<int> { totalHave, totalNeed };
        }
    }
}
