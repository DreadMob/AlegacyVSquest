using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return false;

            string coordString = args[0];
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            // Support multiple coordinates separated by | for multiblock structures
            var coords = coordString.Split('|');
            foreach (var coord in coords)
            {
                if (string.IsNullOrWhiteSpace(coord)) continue;
                if (!QuestInteractAtUtil.TryParsePos(coord.Trim(), out int targetX, out int targetY, out int targetZ)) continue;

                if (QuestInteractAtUtil.HasInteraction(byPlayer, targetX, targetY, targetZ))
                {
                    return true;
                }
            }

            return false;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool completed = IsCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0, 1 });
        }
    }
}
