using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Commands
{
    /// <summary>
    /// Admin command to check VsQuest database connection status.
    /// Usage: .avq bd check
    /// </summary>
    public class VsQuestDbCheckCommand : ModSystem
    {
        private ICoreServerAPI sapi;
        private VsQuest.Systems.Database.VsQuestDbClient dbClient;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Register as subcommand - the main "avq" command is already registered by QuestSystem
            // We register a separate command to avoid conflicts
            try
            {
                api.ChatCommands
                    .GetOrCreate("avq")
                    .BeginSubCommand("db")
                        .WithDescription("VsQuest database management")
                        .RequiresPrivilege(Privilege.controlserver)
                        .BeginSubCommand("check")
                            .WithDescription("Check database connection")
                            .RequiresPrivilege(Privilege.controlserver)
                            .HandleWith(_ => CheckDatabase(_.Caller.Player as IServerPlayer))
                        .EndSubCommand()
                    .EndSubCommand();
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[VsQuestDbCheck] Could not register db command: {0}", ex.Message);
            }
        }

        private TextCommandResult OnAvqCommand(TextCommandCallingArgs args)
        {
            var caller = args.Caller as IServerPlayer;
            var subCommand = args.RawArgs.Length > 0 ? args.RawArgs[0].ToLower() : "";

            if (subCommand == "bd" || subCommand == "db")
            {
                var action = args.RawArgs.Length > 1 ? args.RawArgs[1].ToLower() : "";
                
                if (action == "check")
                {
                    return CheckDatabase(caller);
                }
                else
                {
                    return TextCommandResult.Error("Использование: .avq bd check");
                }
            }
            else
            {
                return TextCommandResult.Error("Использование: .avq bd check");
            }
        }

        private TextCommandResult CheckDatabase(IServerPlayer caller)
        {
            try
            {
                // Get the dbClient from QuestSystem
                var questSystem = sapi.ModLoader.GetModSystem<VsQuest.QuestSystem>();
                if (questSystem == null)
                {
                    return TextCommandResult.Error("❌ Система квестов не найдена");
                }

                dbClient = questSystem.GetDbClient();
                if (dbClient == null)
                {
                    return TextCommandResult.Error("❌ База данных не инициализирована");
                }

                // Check if enabled
                if (!dbClient.IsEnabled)
                {
                    return TextCommandResult.Success("⚠️ База данных отключена в конфиге (enableSync: false)");
                }

                // Test connection
                var status = dbClient.IsHealthyAsync().GetAwaiter().GetResult();
                
                if (status)
                {
                    return TextCommandResult.Success("✅ Соединение с базой данных успешно установлено");
                }
                else
                {
                    return TextCommandResult.Error("❌ Не удалось подключиться к базе данных");
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[VsQuestDbCheck] Error checking database: {0}", ex);
                return TextCommandResult.Error($"❌ Ошибка при проверке: {ex.Message}");
            }
        }
    }
}
