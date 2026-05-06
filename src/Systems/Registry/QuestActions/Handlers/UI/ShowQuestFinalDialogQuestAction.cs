using Vintagestory.API.Server;

namespace VsQuest
{
    public class ShowQuestFinalDialogQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2) throw new QuestException("The 'showquestfinaldialog' action requires at least 2 arguments: titleLangKey, textLangKey.");

            string titleLangKey = args[0];
            string textLangKey = args[1];
            string option1LangKey = args.Length >= 3 ? args[2] : null;
            string option2LangKey = args.Length >= 4 ? args[3] : null;

            if (titleLangKey == "albase:dialogue-priest-final-title" && textLangKey == "albase:dialogue-priest-final-text")
            {
                option2LangKey = null;
            }

            api.Network.GetChannel("alegacyvsquest").SendPacket(new ShowQuestDialogMessage()
            {
                TitleLangKey = titleLangKey,
                TextLangKey = textLangKey,
                Option1LangKey = option1LangKey,
                Option2LangKey = option2LangKey
            }, byPlayer);
        }
    }
}
