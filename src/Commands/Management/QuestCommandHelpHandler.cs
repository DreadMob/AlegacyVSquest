using Vintagestory.API.Common;

namespace VsQuest
{
    public class QuestCommandHelpHandler
    {
        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                "Quest commands:\n" +
                "/avq quest list - list all quest ids\n" +
                "/avq quest check <playerName> - show active/completed quests\n" +
                "/avq quest start <questId> <playerName> - start quest for player\n" +
                "/avq quest complete <questId> <playerName> - force-complete quest\n" +
                "/avq quest completeactive [playerName] - force-complete active quest\n" +
                "/avq quest forgive <all|notes|active|questId> [playerName] - reset quest(s)\n" +
                "/avq setquality [playerName] <qualityId> - set quality on held item\n" +
                "/avq rerollquality [playerName] - roll random quality on held item\n" +
                "\nExamples:\n" +
                "/avq quest start albase:bosshunt-ossuarywarden PlayerName\n" +
                "/avq quest forgive notes PlayerName\n" +
                "/avq setquality PlayerName prestigious"
            );
        }
    }
}
