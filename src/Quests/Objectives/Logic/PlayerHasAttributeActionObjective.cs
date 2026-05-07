using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class PlayerHasAttributeActionObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            return byPlayer.Entity.WatchedAttributes.GetString(args[0]) == args[1];
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool completed = IsCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0 });
        }
    }
}