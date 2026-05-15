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
                    .WithDescription("Force-respawns a trial boss at a specific anchor by its friendly ID (e.g. anchor1).")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("anchorId"))
                    .HandleWith(args => HandleRespawn(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Reloads trial configs from disk (hot-reload without restart).")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(args => HandleReload(args, sapi))
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

            // Show active modifier
            var modType = (TrialModifierType)system.GetActiveModifier();
            if (modType != TrialModifierType.None)
            {
                string modName = LocalizationUtils.GetSafe(TrialWeeklyModifierUtils.GetNameKey(modType));
                sb.AppendLine($"Modifier: {modName}");
            }
            else
            {
                sb.AppendLine($"Modifier: None");
            }

            sb.AppendLine($"Active bosses:");

            foreach (var key in activeKeys)
            {
                string status = system.GetTrialStatus(key);
                var anchors = system.GetAnchorFriendlyIds(key);
                string anchorStr = anchors.Count > 0 ? $" [{string.Join(", ", anchors)}]" : "";
                sb.AppendLine($"  - {key}: {status}{anchorStr}");
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

            // Force spawn all new bosses at their anchors
            int spawned = system.ForceSpawnAllActive();

            return TextCommandResult.Success($"Rotation complete. New active: [{string.Join(", ", newKeys)}]. Spawned: {spawned}");
        }

        private TextCommandResult HandleRespawn(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            string anchorId = (string)args.Parsers[0].GetValue();
            if (string.IsNullOrWhiteSpace(anchorId))
                return TextCommandResult.Error("Usage: /avq trials respawn <anchorId>");

            if (!system.ForceRespawnByAnchor(anchorId, out string error, out string spawnedInfo))
                return TextCommandResult.Error(error);

            return TextCommandResult.Success($"Force-respawned: {spawnedInfo}");
        }

        private TextCommandResult HandleReload(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            int count = system.ReloadConfigs();
            return TextCommandResult.Success($"Reloaded {count} trial configs.");
        }
    }
}
