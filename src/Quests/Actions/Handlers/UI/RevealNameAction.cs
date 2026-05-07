using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class RevealNameAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            if (args != null && args.Length > 0)
            {
                string selector = string.Join(" ", args).Trim();
                if (!string.IsNullOrWhiteSpace(selector) && selector.StartsWith("e[", StringComparison.OrdinalIgnoreCase))
                {
                    var parser = new EntitiesArgParser("selector", sapi, true);
                    var callingArgs = new TextCommandCallingArgs
                    {
                        Caller = new Caller { Player = byPlayer },
                        RawArgs = new CmdArgs(selector)
                    };

                    parser.PreProcess(callingArgs);
                    if (parser.TryProcess(callingArgs) == EnumParseResult.Good)
                    {
                        if (parser.GetValue() is Entity[] entities)
                        {
                            for (int i = 0; i < entities.Length; i++)
                            {
                                entities[i]?.GetBehavior<EntityBehaviorNameTag>()?.SetNameRevealedFor(byPlayer.PlayerUID);
                            }
                        }
                        return;
                    }

                    sapi.Logger.Warning("[alegacyvsquest] RevealNameAction: invalid selector '{0}'.", selector);
                    return;
                }
            }

            if (message == null) return;

            var entity = sapi.World.GetEntityById(message.questGiverId);
            entity?.GetBehavior<EntityBehaviorNameTag>()?.SetNameRevealedFor(byPlayer.PlayerUID);
        }
    }
}
