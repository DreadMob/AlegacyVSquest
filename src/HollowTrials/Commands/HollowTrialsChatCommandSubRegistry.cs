using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class HollowTrialsChatCommandSubRegistry : IChatCommandSubRegistry
    {
        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            avq.BeginSubCommand("trials")
                .WithDescription("Hollow Trials admin commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("status")
                    .WithDescription("Shows current active trial bosses, time until rotation, and status of each boss.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(args => HandleStatus(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("skip")
                    .WithDescription("Force-rotates to the next set of trial bosses.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(args => HandleSkip(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("respawn")
                    .WithDescription("Force-respawns a specific trial boss by trialKey.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("trialKey"))
                    .HandleWith(args => HandleRespawn(args, sapi))
                .EndSubCommand()
            .EndSubCommand();
        }

        private TextCommandResult HandleStatus(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            var activeKeys = system.GetActiveTrialKeys();
            if (activeKeys == null || activeKeys.Count == 0)
                return TextCommandResult.Error("No active trials (no configs loaded or no anchors placed).");

            double hoursLeft = system.GetHoursUntilRotation();
            double daysLeft = hoursLeft / 24.0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Hollow Trials Status ===");
            sb.AppendLine($"Rotation in: {hoursLeft:0.0}h (~{daysLeft:0.0} days)");
            sb.AppendLine($"Active bosses:");

            foreach (var key in activeKeys)
            {
                string status = system.GetTrialStatus(key);
                sb.AppendLine($"  - {key}: {status}");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult HandleSkip(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            if (!system.ForceRotation(out var newKeys))
                return TextCommandResult.Error("Rotation failed (no configs?).");

            return TextCommandResult.Success($"Rotation complete. New active: [{string.Join(", ", newKeys)}]");
        }

        private TextCommandResult HandleRespawn(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            string trialKey = (string)args.Parsers[0].GetValue();
            if (string.IsNullOrWhiteSpace(trialKey))
                return TextCommandResult.Error("Usage: /avq trials respawn <trialKey>");

            if (!system.ForceRespawn(trialKey, out string error))
                return TextCommandResult.Error(error);

            return TextCommandResult.Success($"Force-respawned trial boss '{trialKey}'.");
        }
    }
}
