using Vintagestory.API.Common;

namespace VsQuest
{
    public static class ModClassRegistry
    {
        public static void RegisterAll(ICoreAPI api)
        {
            RegisterEntityBehaviors(api);
            RegisterItems(api);
            RegisterBlocksAndBlockEntities(api);
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
            api.RegisterEntityBehaviorClass("explodeondeath", typeof(EntityBehaviorExplodeOnDeath));
            api.RegisterEntityBehaviorClass("alegacyvsquest:questdropondeath", typeof(EntityBehaviorQuestDropOnDeath));
            api.RegisterEntityBehaviorClass("alegacyvsquestguionclick", typeof(EntityBehaviorGuiOnClick));
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

            api.RegisterBlockClass("BlockBossHuntArena", typeof(BlockBossHuntArena));
            api.RegisterBlockEntityClass("BossHuntArena", typeof(BlockEntityBossHuntArena));

            api.RegisterBlockClass("BlockTemporalRiftProjector", typeof(BlockTemporalRiftProjector));
            api.RegisterBlockEntityClass("TemporalRiftProjector", typeof(BlockEntityTemporalRiftProjector));
        }
    }
}
