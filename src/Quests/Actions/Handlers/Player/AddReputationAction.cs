using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddReputationAction : PlayerActionBase
    {
        private const string OnceKeysListKey = "alegacyvsquest:rep:oncekeys";

        protected override int MinArgs => 3;
        protected override string ActionName => "addreputation";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            string scopeRaw = args[0]?.ToLowerInvariant();
            string id = args[1];
            if (string.IsNullOrWhiteSpace(scopeRaw) || string.IsNullOrWhiteSpace(id)) return;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null) return;

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                sapi.Logger.Error($"[vsquest] 'addreputation' action scope must be 'npc' or 'faction', got '{scopeRaw}'.");
                return;
            }

            if (!int.TryParse(args[2], out int delta)) delta = 0;

            int? max = null;
            string onceKey = null;

            if (args.Length >= 4)
            {
                var arg3 = args[3];
                if (int.TryParse(arg3, out int parsedMax))
                {
                    max = parsedMax;
                    if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[4]))
                    {
                        onceKey = args[4];
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(arg3))
                    {
                        onceKey = arg3;
                    }
                    if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[4]))
                    {
                        onceKey = args[4];
                    }
                }
            }

            var wa = player.Entity.WatchedAttributes;
            if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
            {
                return;
            }

            int current = repSystem.GetReputationValue(player as IPlayer, scope, id);
            int next = current + delta;

            if (max.HasValue && next > max.Value) next = max.Value;

            repSystem.ApplyReputationChange(sapi, player, scope, id, next, false);

            if (!string.IsNullOrWhiteSpace(onceKey))
            {
                var list = wa.GetStringArray(OnceKeysListKey, null);
                if (list == null)
                {
                    wa.SetStringArray(OnceKeysListKey, new[] { onceKey });
                    wa.MarkPathDirty(OnceKeysListKey);
                }
                else
                {
                    bool exists = false;
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i] == onceKey)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        var nextList = new string[list.Length + 1];
                        for (int i = 0; i < list.Length; i++) nextList[i] = list[i];
                        nextList[list.Length] = onceKey;
                        wa.SetStringArray(OnceKeysListKey, nextList);
                        wa.MarkPathDirty(OnceKeysListKey);
                    }
                }

                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }
    }
}
