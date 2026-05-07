using System.Collections.Generic;



namespace VsQuest

{

    public class AlegacyVsQuestConfig

    {

        public bool Debug { get; set; } = false;

        /// <summary>
        /// Default value for quest completion notifications.
        /// If true, quests will broadcast a chat message when completed unless the quest has notifyOnComplete set to false.
        /// If false, quests will not broadcast completion messages unless explicitly set on the quest.
        /// </summary>
        public bool DefaultNotifyOnComplete { get; set; } = true;

        /// <summary>
        /// If true, shows the quest description as hover text in completion chat messages
        /// when no custom -hover text is defined for the quest.
        /// If false, only the quest title is shown without hover.
        /// </summary>
        public bool ShowQuestDescriptionInHover { get; set; } = false;

        /// <summary>
        /// If true, hover text is only shown when a custom -hover localization key exists.
        /// The quest description (-desc) is never used as hover text, regardless of ShowQuestDescriptionInHover.
        /// If false, description may be used as hover when ShowQuestDescriptionInHover is true.
        /// </summary>
        public bool OnlyCustomHoverText { get; set; } = false;

        public BossHuntCoreConfig BossHunt { get; set; } = new BossHuntCoreConfig();



		public BossCombatCoreConfig BossCombat { get; set; } = new BossCombatCoreConfig();



		public QuestTickCoreConfig QuestTick { get; set; } = new QuestTickCoreConfig();



		public ActionItemsCoreConfig ActionItems { get; set; } = new ActionItemsCoreConfig();



		public ClientCoreConfig Client { get; set; } = new ClientCoreConfig();



		public HarmonyPatchesCoreConfig HarmonyPatches { get; set; } = new HarmonyPatchesCoreConfig();
		public List<string> NestedLocalizationDomains { get; set; } = new List<string>
		{
			"alegacyvsquest",
			"albase",
			"alstory"
		};

		public PerformanceCoreConfig Performance { get; set; } = new PerformanceCoreConfig();

    }

}
