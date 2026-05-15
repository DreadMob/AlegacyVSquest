using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class ModClassRegistry
    {
        public static void RegisterAll(ICoreAPI api)
        {
            RegisterEntityBehaviors(api);
            RegisterItems(api);
            RegisterBlocksAndBlockEntities(api);
            RegisterAiTasks(api);
        }

        private static void RegisterAiTasks(ICoreAPI api)
        {
            // AI tasks need to be registered on server side
            if (api.Side == EnumAppSide.Server)
            {
                var sapi = api as ICoreServerAPI;
                sapi?.RegisterAiTask<AiTaskSeekTargetPlayer>("seektargetplayer");
            }
        }

        private static void RegisterEntityBehaviors(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterEntityBehaviorClass("questtarget", typeof(EntityBehaviorQuestTarget));
            api.RegisterEntityBehaviorClass("bossnametag", typeof(EntityBehaviorBossNameTag));
            api.RegisterEntityBehaviorClass("alegacyvsquestbosshealthbar", typeof(EntityBehaviorBossHealthbarOverride));
            api.RegisterEntityBehaviorClass("bossrespawn", typeof(EntityBehaviorBossRespawn));
            api.RegisterEntityBehaviorClass("bossdespair", typeof(EntityBehaviorBossDespair));
            api.RegisterEntityBehaviorClass("bosscombatmarker", typeof(EntityBehaviorBossCombatMarker));
            api.RegisterEntityBehaviorClass("bosshuntcombatmarker", typeof(EntityBehaviorBossHuntCombatMarker));
            api.RegisterEntityBehaviorClass("bossmusicurl", typeof(EntityBehaviorBossMusicUrlController));
            api.RegisterEntityBehaviorClass("bosshastargetsync", typeof(EntityBehaviorBossHasTargetSync));
            api.RegisterEntityBehaviorClass("bosssummonritual", typeof(EntityBehaviorBossSummonRitual));
            api.RegisterEntityBehaviorClass("bossgrowthritual", typeof(EntityBehaviorBossGrowthRitual));
            api.RegisterEntityBehaviorClass("bossrebirth2", typeof(EntityBehaviorBossRebirth2));
            api.RegisterEntityBehaviorClass("bosscastphase", typeof(EntityBehaviorBossCastPhase));
            api.RegisterEntityBehaviorClass("bossdash", typeof(EntityBehaviorBossDash));
            api.RegisterEntityBehaviorClass("bossteleport", typeof(EntityBehaviorBossTeleport));
            api.RegisterEntityBehaviorClass("bossfaketeleport", typeof(EntityBehaviorBossFakeTeleport));
            api.RegisterEntityBehaviorClass("bossrepulsestun", typeof(EntityBehaviorBossRepulseStun));
            api.RegisterEntityBehaviorClass("bosshook", typeof(EntityBehaviorBossHook));
            api.RegisterEntityBehaviorClass("bossgrab", typeof(EntityBehaviorBossGrab));
            api.RegisterEntityBehaviorClass("bossanticheese", typeof(EntityBehaviorBossAntiCheese));
            api.RegisterEntityBehaviorClass("bossdamageshield", typeof(EntityBehaviorBossDamageShield));
            api.RegisterEntityBehaviorClass("bossdamagesourceimmunity", typeof(EntityBehaviorBossDamageSourceImmunity));
            api.RegisterEntityBehaviorClass("bossintoxaura", typeof(EntityBehaviorBossIntoxicationAura));
            api.RegisterEntityBehaviorClass("bossoxygendrainaura", typeof(EntityBehaviorBossOxygenDrainAura));
            api.RegisterEntityBehaviorClass("bosscloning", typeof(EntityBehaviorBossCloning));
            api.RegisterEntityBehaviorClass("bossrandomlightning", typeof(EntityBehaviorBossRandomLightning));
            api.RegisterEntityBehaviorClass("bossplayerclone", typeof(EntityBehaviorBossPlayerClone));
            api.RegisterEntityBehaviorClass("bosstrapclone", typeof(EntityBehaviorBossTrapClone));
            api.RegisterEntityBehaviorClass("bossformswap", typeof(EntityBehaviorBossFormSwap));
            api.RegisterEntityBehaviorClass("bossformswaplist", typeof(EntityBehaviorBossFormSwapList));
            api.RegisterEntityBehaviorClass("bossmodelswap", typeof(EntityBehaviorBossModelSwap));
            api.RegisterEntityBehaviorClass("bossintermissiondispel", typeof(EntityBehaviorBossIntermissionDispel));
            api.RegisterEntityBehaviorClass("bossparasiteleech", typeof(EntityBehaviorBossParasiteLeech));
            api.RegisterEntityBehaviorClass("explosivelocust", typeof(EntityBehaviorExplosiveLocust));
            api.RegisterEntityBehaviorClass("bossperiodicspawn", typeof(EntityBehaviorBossPeriodicSpawn));
            api.RegisterEntityBehaviorClass("bossashfloor", typeof(EntityBehaviorBossAshFloor));
            api.RegisterEntityBehaviorClass("shiverdebug", typeof(EntityBehaviorShiverDebug));
            // api.RegisterEntityBehaviorClass("bossdynamicscaling", typeof(EntityBehaviorBossDynamicScaling)); // DISABLED
            api.RegisterEntityBehaviorClass("bossdamageinvulnerability", typeof(EntityBehaviorBossDamageInvulnerability));
            api.RegisterEntityBehaviorClass("bosspushback", typeof(EntityBehaviorBossPushback));
            api.RegisterEntityBehaviorClass("bossstillnessmark", typeof(EntityBehaviorBossStillnessMark));
            api.RegisterEntityBehaviorClass("bosswoundedmark", typeof(EntityBehaviorBossWoundedMark));
            api.RegisterEntityBehaviorClass("bosssurroundedresponse", typeof(EntityBehaviorBossSurroundedResponse));
            api.RegisterEntityBehaviorClass("bosslifedrainnova", typeof(EntityBehaviorBossLifeDrainNova));
            api.RegisterEntityBehaviorClass("bosspassiveregen", typeof(EntityBehaviorBossPassiveRegen));
            api.RegisterEntityBehaviorClass("bosscorpseexplosion", typeof(EntityBehaviorBossCorpseExplosion));
            api.RegisterEntityBehaviorClass("bossrequiemchains", typeof(EntityBehaviorBossRequiemChains));
            api.RegisterEntityBehaviorClass("bossmindcontrol", typeof(EntityBehaviorBossMindControl));
            api.RegisterEntityBehaviorClass("explodeondeath", typeof(EntityBehaviorExplodeOnDeath));
            api.RegisterEntityBehaviorClass("alegacyvsquest:questdropondeath", typeof(EntityBehaviorQuestDropOnDeath));
            api.RegisterEntityBehaviorClass("alegacyvsquestguionclick", typeof(EntityBehaviorGuiOnClick));
            api.RegisterEntityBehaviorClass("damagedummy", typeof(EntityBehaviorDamageDummy));

            // Hollow Trials behaviors
            api.RegisterEntityBehaviorClass("bossenrage", typeof(EntityBehaviorBossEnrage));
            api.RegisterEntityBehaviorClass("bossvulnerabilitywindow", typeof(EntityBehaviorBossVulnerabilityWindow));
            api.RegisterEntityBehaviorClass("bosstelegraph", typeof(EntityBehaviorBossTelegraph));
            api.RegisterEntityBehaviorClass("bossgroundslam", typeof(EntityBehaviorBossGroundSlam));
            api.RegisterEntityBehaviorClass("bossvoidzone", typeof(EntityBehaviorBossVoidZone));
            api.RegisterEntityBehaviorClass("bosscursestack", typeof(EntityBehaviorBossCurseStack));
            api.RegisterEntityBehaviorClass("bossphaseshift", typeof(EntityBehaviorBossPhaseShift));
            api.RegisterEntityBehaviorClass("bosssoulchain", typeof(EntityBehaviorBossSoulChain));
            api.RegisterEntityBehaviorClass("bossbloodprice", typeof(EntityBehaviorBossBloodPrice));
            api.RegisterEntityBehaviorClass("bossmirrorimage", typeof(EntityBehaviorBossMirrorImage));
            api.RegisterEntityBehaviorClass("bosscursemark", typeof(EntityBehaviorBossCurseMark));
            api.RegisterEntityBehaviorClass("bossretaliation", typeof(EntityBehaviorBossRetaliation));
            api.RegisterEntityBehaviorClass("bossmovementstyle", typeof(EntityBehaviorBossMovementStyle));
            api.RegisterEntityBehaviorClass("bossghostflight", typeof(EntityBehaviorBossGhostFlight));
        }

        private static void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));
            api.RegisterItemClass("ItemEntitySpawner", typeof(ItemEntitySpawner));
        }

        private static void RegisterBlocksAndBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockCooldownPlaceholder", typeof(BlockCooldownPlaceholder));
            api.RegisterBlockEntityClass("CooldownPlaceholder", typeof(BlockEntityCooldownPlaceholder));

            api.RegisterBlockClass("BlockAshFloor", typeof(BlockAshFloor));
            api.RegisterBlockEntityClass("AshFloor", typeof(BlockEntityAshFloor));

            api.RegisterBlockClass("BlockQuestSpawner", typeof(BlockQuestSpawner));
            api.RegisterBlockEntityClass("QuestSpawner", typeof(BlockEntityQuestSpawner));

            api.RegisterBlockClass("BlockBossHuntAnchor", typeof(BlockBossHuntAnchor));
            api.RegisterBlockEntityClass("BossHuntAnchor", typeof(BlockEntityBossHuntAnchor));

            api.RegisterBlockClass("BlockVoidRiftAnchor", typeof(BlockVoidRiftAnchor));
            api.RegisterBlockEntityClass("VoidRiftAnchor", typeof(BlockEntityVoidRiftAnchor));

            api.RegisterBlockClass("BlockBossHuntArena", typeof(BlockBossHuntArena));
            api.RegisterBlockEntityClass("BossHuntArena", typeof(BlockEntityBossHuntArena));

            api.RegisterBlockClass("BlockTemporalRiftProjector", typeof(BlockTemporalRiftProjector));
            api.RegisterBlockEntityClass("TemporalRiftProjector", typeof(BlockEntityTemporalRiftProjector));
        }
    }
}
