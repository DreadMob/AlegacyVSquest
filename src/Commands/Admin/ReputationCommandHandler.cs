using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Command handler for managing NPC/faction reputation.
    /// Usage: /avq rep [playerName] [npc|faction] [id] [amount]
    ///        /avq rep get [playerName] [npc|faction] [id]
    /// </summary>
    public class ReputationCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public ReputationCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        /// <summary>
        /// /avq rep add [playerName] [npc|faction] [id] [amount]
        /// Adds reputation to a player for a specific NPC or faction.
        /// </summary>
        public TextCommandResult HandleAdd(TextCommandCallingArgs args)
        {
            string playerName = args.Parsers[0].GetValue() as string;
            string scopeRaw = args.Parsers[1].GetValue() as string;
            string id = args.Parsers[2].GetValue() as string;
            string amountStr = args.Parsers[3].GetValue() as string;

            var targetPlayer = ResolvePlayer(playerName, args);
            if (targetPlayer == null)
                return TextCommandResult.Error($"Игрок '{playerName}' не найден.");

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null)
                return TextCommandResult.Error("ReputationSystem не загружена.");

            if (!repSystem.TryParseScope(scopeRaw?.ToLowerInvariant(), out var scope))
                return TextCommandResult.Error("Scope должен быть 'npc' или 'faction'.");

            if (string.IsNullOrWhiteSpace(id))
                return TextCommandResult.Error("ID репутации не указан.");

            if (!int.TryParse(amountStr, out int amount))
                return TextCommandResult.Error($"Неверное количество: '{amountStr}'.");

            int current = repSystem.GetReputationValue(targetPlayer as IPlayer, scope, id);
            int newValue = current + amount;
            if (newValue < 0) newValue = 0;

            repSystem.ApplyReputationChange(sapi, targetPlayer, scope, id, newValue, true);

            return TextCommandResult.Success($"Репутация {scope} '{id}' для {targetPlayer.PlayerName}: {current} → {newValue} ({(amount >= 0 ? "+" : "")}{amount})");
        }

        /// <summary>
        /// /avq rep set [playerName] [npc|faction] [id] [value]
        /// Sets reputation to a specific value.
        /// </summary>
        public TextCommandResult HandleSet(TextCommandCallingArgs args)
        {
            string playerName = args.Parsers[0].GetValue() as string;
            string scopeRaw = args.Parsers[1].GetValue() as string;
            string id = args.Parsers[2].GetValue() as string;
            string valueStr = args.Parsers[3].GetValue() as string;

            var targetPlayer = ResolvePlayer(playerName, args);
            if (targetPlayer == null)
                return TextCommandResult.Error($"Игрок '{playerName}' не найден.");

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null)
                return TextCommandResult.Error("ReputationSystem не загружена.");

            if (!repSystem.TryParseScope(scopeRaw?.ToLowerInvariant(), out var scope))
                return TextCommandResult.Error("Scope должен быть 'npc' или 'faction'.");

            if (string.IsNullOrWhiteSpace(id))
                return TextCommandResult.Error("ID репутации не указан.");

            if (!int.TryParse(valueStr, out int value))
                return TextCommandResult.Error($"Неверное значение: '{valueStr}'.");

            if (value < 0) value = 0;

            int current = repSystem.GetReputationValue(targetPlayer as IPlayer, scope, id);
            repSystem.ApplyReputationChange(sapi, targetPlayer, scope, id, value, true);

            return TextCommandResult.Success($"Репутация {scope} '{id}' для {targetPlayer.PlayerName}: {current} → {value}");
        }

        /// <summary>
        /// /avq rep get [playerName] [npc|faction] [id]
        /// Gets current reputation value.
        /// </summary>
        public TextCommandResult HandleGet(TextCommandCallingArgs args)
        {
            string playerName = args.Parsers[0].GetValue() as string;
            string scopeRaw = args.Parsers[1].GetValue() as string;
            string id = args.Parsers[2].GetValue() as string;

            var targetPlayer = ResolvePlayer(playerName, args);
            if (targetPlayer == null)
                return TextCommandResult.Error($"Игрок '{playerName}' не найден.");

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null)
                return TextCommandResult.Error("ReputationSystem не загружена.");

            if (!repSystem.TryParseScope(scopeRaw?.ToLowerInvariant(), out var scope))
                return TextCommandResult.Error("Scope должен быть 'npc' или 'faction'.");

            if (string.IsNullOrWhiteSpace(id))
                return TextCommandResult.Error("ID репутации не указан.");

            int value = repSystem.GetReputationValue(targetPlayer as IPlayer, scope, id);

            return TextCommandResult.Success($"Репутация {scope} '{id}' для {targetPlayer.PlayerName}: {value}");
        }

        private IServerPlayer ResolvePlayer(string playerName, TextCommandCallingArgs args)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                return sapi.Server.Players.FirstOrDefault(p =>
                    p?.PlayerName != null &&
                    p.PlayerName.ToLowerInvariant() == playerName.ToLowerInvariant());
            }

            return args.Caller.Player as IServerPlayer;
        }
    }
}
