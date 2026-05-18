using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestActionRegistry : IRegistry
    {
        private readonly Dictionary<string, IQuestAction> actionRegistry;
        private readonly ICoreAPI api;
        private readonly ICoreServerAPI sapi;
        private readonly Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback;

        public QuestActionRegistry(Dictionary<string, IQuestAction> actionRegistry, ICoreAPI api, ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            this.actionRegistry = QuestRegistryService.ActionRegistry;
            this.api = api;
            this.sapi = sapi;
            this.onQuestAcceptedCallback = onQuestAcceptedCallback;
        }

        public void Register()
        {
            RegisterWorldAndUi();
            RegisterSpawnAndEntity();
            RegisterPlayerAndAttributes();
            RegisterQuestLifecycle();
            RegisterReputationAndJournal();
            RegisterItemsAndCommands();
            RegisterObjectivesAndTracking();
        }

        private void RegisterWorldAndUi()
        {
            actionRegistry.Add("openquests", new OpenQuestsAction());
            actionRegistry.Add("openserverinfo", new OpenServerInfoAction());
            actionRegistry.Add("playsound", new PlaySoundQuestAction());
            actionRegistry.Add("preloadbossmusic", new PreloadBossMusicQuestAction());
            actionRegistry.Add("notify", new NotifyQuestAction());
            actionRegistry.Add("discover", new DiscoverQuestAction());
            actionRegistry.Add("showquestfinaldialog", new ShowQuestFinalDialogQuestAction());
            actionRegistry.Add("showquiz", new ShowQuizAction());
            actionRegistry.Add("closedialogue", new CloseDialogueAction());
            actionRegistry.Add("revealname", new RevealNameAction());
            actionRegistry.Add("openrerolldialog", new OpenRerollDialogAction());
        }

        private void RegisterSpawnAndEntity()
        {
            actionRegistry.Add("despawnquestgiver", new DespawnQuestGiverAction());
            actionRegistry.Add("spawnentities", new SpawnEntitiesAction());
            actionRegistry.Add("spawnany", new SpawnAnyOfEntitiesAction());
            actionRegistry.Add("spawnentitiesatplayer", new SpawnEntitiesAtPlayerAction());
            actionRegistry.Add("spawnsmoke", new SpawnSmokeAction());
            actionRegistry.Add("recruitentity", new RecruitEntityAction());
            actionRegistry.Add("cycleentityanimation", new CycleEntityAnimationAction());
            actionRegistry.Add("damageentity", new DamageSelectedEntityAction());
            actionRegistry.Add("damagequestgiver", new DamageQuestGiverAction());
            actionRegistry.Add("cooldownblock", new CooldownBlockAction());
            actionRegistry.Add("setquestgiverattribute", new SetQuestGiverAttributeQuestAction());
        }

        private void RegisterPlayerAndAttributes()
        {
            actionRegistry.Add("healplayer", new HealPlayerAction());
            actionRegistry.Add("addplayerattribute", new AddPlayerAttributeAction());
            actionRegistry.Add("addplayerint", new AddPlayerIntAction());
            actionRegistry.Add("landclaimallowance", new LandClaimAllowanceAction());
            actionRegistry.Add("landclaimmaxareas", new LandClaimMaxAreasAction());
            actionRegistry.Add("removeplayerattribute", new RemovePlayerAttributeAction());
            actionRegistry.Add("allowcharselonce", new AllowCharSelOnceAction());
        }

        private void RegisterQuestLifecycle()
        {
            actionRegistry.Add("completequest", new CompleteQuestAction());
            actionRegistry.Add("acceptquest", new AcceptQuestAction(sapi, onQuestAcceptedCallback));
        }

        private void RegisterReputationAndJournal()
        {
            actionRegistry.Add("addreputation", new AddReputationAction());
            actionRegistry.Add("addjournalentry", new AddJournalEntryQuestAction());
            actionRegistry.Add("addvanillajournalentry", new AddVanillaJournalEntryQuestAction());
        }

        private void RegisterItemsAndCommands()
        {
            actionRegistry.Add("giveitem", new GiveItemAction());
            actionRegistry.Add("takeitem", new TakeItemAction());
            actionRegistry.Add("addtraits", new AddTraitsAction());
            actionRegistry.Add("removetraits", new RemoveTraitsAction());
            actionRegistry.Add("servercommand", new ServerCommandAction());
            actionRegistry.Add("playercommand", new PlayerCommandAction());
            actionRegistry.Add("questitem", new GiveActionItemAction());
            actionRegistry.Add("giveactionitem", new GiveActionItemAction());
            actionRegistry.Add("randomquestitem", new RandomQuestItemAction());
            actionRegistry.Add("consumeactionitem", new ConsumeActionItemAction());
        }

        private void RegisterObjectivesAndTracking()
        {
            actionRegistry.Add("randomkill", new RollKillObjectivesAction());
            actionRegistry.Add("resetwalkdistance", new ResetWalkDistanceQuestAction());
            actionRegistry.Add("checkobjective", new CheckObjectiveAction());
            actionRegistry.Add("markinteraction", new MarkInteractionAction());
            actionRegistry.Add("markentityinteraction", new MarkEntityInteractionAction());
            actionRegistry.Add("markduelparticipation", new MarkDuelParticipationAction());
            actionRegistry.Add("trackboss", new TrackBossAction());
            actionRegistry.Add("repairheldany", new RepairHeldItemAction());

            // Hollow Trials actions
            actionRegistry.Add("trialchallengebonuses", new TrialChallengeBonusesAction());
            actionRegistry.Add("tracktrialboss", new TrackTrialBossAction());
            actionRegistry.Add("resettrialtracker", new ResetTrialTrackersAction());
            actionRegistry.Add("openshop", new OpenTrialShopAction());

            // [EXPERIMENTAL] Timer and reset actions
            actionRegistry.Add("resettimer", new ResetTimerAction());
            actionRegistry.Add("resetexperimentalobjective", new ResetExperimentalObjectiveAction());
        }
    }
}
