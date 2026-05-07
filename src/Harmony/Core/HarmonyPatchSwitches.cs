namespace VsQuest
{
    public static class HarmonyPatchSwitches
    {
        public static bool ItemAttributePatchesEnabled { get; private set; } = true;
        public static bool PlayerAttributePatchesEnabled { get; private set; } = true;

        public static bool Item_CollectibleObject_GetHeldItemName { get; private set; } = true;
        public static bool Item_ModSystemWearableStats_onFootStep { get; private set; } = true;
        public static bool Item_EntityBehaviorHealth_OnFallToGround { get; private set; } = true;
        public static bool Item_EntityBehaviorTemporalStabilityAffected_OnGameTick { get; private set; } = true;
        public static bool Item_CollectibleObject_GetAttackPower { get; private set; } = true;
        public static bool Item_CollectibleBehaviorWearable_GetWarmth { get; private set; } = true;
        public static bool Item_EntityBehaviorBodyTemperature_WarmthBonus { get; private set; } = true;
        public static bool Item_CollectibleObject_GetMiningSpeed_MiningSpeedMult { get; private set; } = true;
        public static bool Item_ModSystemWearableStats_handleDamaged { get; private set; } = true;
        public static bool Item_ModSystemWearableStats_updateWearableStats { get; private set; } = true;
        public static bool Item_CollectibleObject_TryMergeStacks_SecondChanceCharge { get; private set; } = true;
        // ItemWearable merge patches removed - obsolete in 1.22

        public static bool Player_ModSystemWearableStats_handleDamaged_PlayerAttributes { get; private set; } = true;
        public static bool Player_EntityAgent_OnGameTick_Unified { get; private set; } = true;
        public static bool Player_EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance { get; private set; } = true;
        public static bool Player_EntityBehaviorHealth_OnEntityDeath_SecondChanceReset { get; private set; } = true;
        public static bool Player_EntityAgent_ReceiveDamage_PlayerAttackPower { get; private set; } = true;
        public static bool Player_EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth { get; private set; } = true;

        public static bool ActionItemModePatchesEnabled { get; private set; } = true;
        public static bool ActionItemMode_CollectibleObject_GetToolModes_ActionItemModes { get; private set; } = true;
        public static bool ActionItemMode_CollectibleObject_GetToolMode_ActionItemModes { get; private set; } = true;
        public static bool ActionItemMode_CollectibleObject_SetToolMode_ActionItemModes { get; private set; } = true;

        public static bool ItemMoveActingPlayerContextPatchesEnabled { get; private set; } = true;
        public static bool ItemMoveActingPlayerContext_InventoryBase_ActivateSlot { get; private set; } = true;
        public static bool ItemMoveActingPlayerContext_ItemSlot_ActivateSlot { get; private set; } = true;

        public static bool ItemTooltipPatchesEnabled { get; private set; } = true;
        public static bool ItemTooltip_CollectibleObject_GetHeldItemInfo { get; private set; } = true;
        // ItemWearable tooltip patch removed - obsolete in 1.22, covered by CollectibleObject patch

        public static bool QuestItemHotbarOnlyPatchesEnabled { get; private set; } = true;
        public static bool QuestItemHotbarOnly_ItemSlot_CanTakeFrom { get; private set; } = true;
        public static bool QuestItemHotbarOnly_ItemSlot_CanHold { get; private set; } = true;

        public static bool QuestItemNoDropOnDeathPatchesEnabled { get; private set; } = true;
        public static bool QuestItemNoDropOnDeath_PlayerInventoryManager_OnDeath { get; private set; } = true;

        public static bool EntityInfoTextPatchesEnabled { get; private set; } = true;
        public static bool EntityInfoText_Entity_GetInfoText { get; private set; } = true;
        public static bool EntityInfoText_EntityAgent_GetInfoText { get; private set; } = true;

        public static bool EntityInteractPatchesEnabled { get; private set; } = true;
        public static bool EntityInteract_EntityBehavior_OnInteract { get; private set; } = true;

        public static bool EntityPlayerBotInteractPatchesEnabled { get; private set; } = true;
        public static bool EntityPlayerBotInteract_EntityPlayerBot_OnInteract { get; private set; } = true;

        public static bool EntityPrefixAndCreatureNamePatchesEnabled { get; private set; } = true;
        public static bool EntityPrefixAndCreatureName_Entity_GetPrefixAndCreatureName { get; private set; } = true;

        public static bool EntityShiverStrokePatchesEnabled { get; private set; } = true;
        public static bool EntityShiverStroke_EntityShiver_OnGameTick_StrokeFreq { get; private set; } = true;

        public static bool EntitySoundPitchPatchesEnabled { get; private set; } = true;
        public static bool EntitySoundPitch_Entity_PlayEntitySound { get; private set; } = true;

        public static bool BlockInteractPatchesEnabled { get; private set; } = true;
        public static bool BlockInteract_Block_OnBlockInteractStart { get; private set; } = true;

        public static bool ServerBlockInteractPatchesEnabled { get; private set; } = true;
        public static bool ServerBlockInteract_Block_OnBlockInteractStart { get; private set; } = true;

        public static bool QuestItemDropBlockPatchesEnabled { get; private set; } = true;
        public static bool QuestItemDropBlock_InventoryManager_DropItem { get; private set; } = true;

        public static bool QuestItemEquipBlockPatchesEnabled { get; private set; } = true;
        public static bool QuestItemEquipBlock_ItemSlotCharacter_CanHold { get; private set; } = true;
        public static bool QuestItemEquipBlock_ItemSlotCharacter_CanTakeFrom { get; private set; } = true;

        public static bool QuestItemGroundStorageBlockPatchesEnabled { get; private set; } = true;
        public static bool QuestItemGroundStorageBlock_CollectibleBehaviorGroundStorable_Interact { get; private set; } = true;

        public static bool Item_InventoryChangeTracking { get; private set; } = true;

        public static bool ConversablePatchesEnabled { get; private set; } = true;
        public static bool Conversable_EntityBehaviorConversable_Controller_DialogTriggers { get; private set; } = true;

        public static bool DebugDamagePatchesEnabled { get; private set; } = true;
        public static bool DebugDamage_EntityAgent_ReceiveDamage { get; private set; } = true;

        public static void ApplyFromConfig(AlegacyVsQuestConfig config)
        {
            var harmony = config?.HarmonyPatches;

            ItemAttributePatchesEnabled = harmony?.ItemAttributePatchesEnabled ?? true;
            PlayerAttributePatchesEnabled = harmony?.PlayerAttributePatchesEnabled ?? true;

            var item = harmony?.ItemAttribute;
            Item_CollectibleObject_GetHeldItemName = item?.CollectibleObject_GetHeldItemName ?? true;
            Item_ModSystemWearableStats_onFootStep = item?.ModSystemWearableStats_onFootStep ?? true;
            Item_EntityBehaviorHealth_OnFallToGround = item?.EntityBehaviorHealth_OnFallToGround ?? true;
            Item_EntityBehaviorTemporalStabilityAffected_OnGameTick = item?.EntityBehaviorTemporalStabilityAffected_OnGameTick ?? true;
            Item_CollectibleObject_GetAttackPower = item?.CollectibleObject_GetAttackPower ?? true;
            Item_CollectibleBehaviorWearable_GetWarmth = item?.CollectibleBehaviorWearable_GetWarmth ?? true;
            Item_EntityBehaviorBodyTemperature_WarmthBonus = item?.EntityBehaviorBodyTemperature_WarmthBonus ?? true;
            Item_CollectibleObject_GetMiningSpeed_MiningSpeedMult = item?.CollectibleObject_GetMiningSpeed_MiningSpeedMult ?? true;
            Item_ModSystemWearableStats_handleDamaged = item?.ModSystemWearableStats_handleDamaged ?? true;
            Item_ModSystemWearableStats_updateWearableStats = item?.ModSystemWearableStats_updateWearableStats ?? true;
            Item_CollectibleObject_TryMergeStacks_SecondChanceCharge = item?.CollectibleObject_TryMergeStacks_SecondChanceCharge ?? true;

            var player = harmony?.PlayerAttribute;
            Player_ModSystemWearableStats_handleDamaged_PlayerAttributes = player?.ModSystemWearableStats_handleDamaged_PlayerAttributes ?? true;
            Player_EntityAgent_OnGameTick_Unified = player?.EntityAgent_OnGameTick_Unified ?? true;
            Player_EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance = player?.EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance ?? true;
            Player_EntityBehaviorHealth_OnEntityDeath_SecondChanceReset = player?.EntityBehaviorHealth_OnEntityDeath_SecondChanceReset ?? true;
            Player_EntityAgent_ReceiveDamage_PlayerAttackPower = player?.EntityAgent_ReceiveDamage_PlayerAttackPower ?? true;
            Player_EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth = player?.EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth ?? true;

            ActionItemModePatchesEnabled = harmony?.ActionItemModePatchesEnabled ?? true;
            var modes = harmony?.ActionItemMode;
            ActionItemMode_CollectibleObject_GetToolModes_ActionItemModes = modes?.CollectibleObject_GetToolModes_ActionItemModes ?? true;
            ActionItemMode_CollectibleObject_GetToolMode_ActionItemModes = modes?.CollectibleObject_GetToolMode_ActionItemModes ?? true;
            ActionItemMode_CollectibleObject_SetToolMode_ActionItemModes = modes?.CollectibleObject_SetToolMode_ActionItemModes ?? true;

            ItemMoveActingPlayerContextPatchesEnabled = harmony?.ItemMoveActingPlayerContextPatchesEnabled ?? true;
            var moveCtx = harmony?.ItemMoveActingPlayerContext;
            ItemMoveActingPlayerContext_InventoryBase_ActivateSlot = moveCtx?.InventoryBase_ActivateSlot ?? true;
            ItemMoveActingPlayerContext_ItemSlot_ActivateSlot = moveCtx?.ItemSlot_ActivateSlot ?? true;

            ItemTooltipPatchesEnabled = harmony?.ItemTooltipPatchesEnabled ?? true;
            var tooltip = harmony?.ItemTooltip;
            ItemTooltip_CollectibleObject_GetHeldItemInfo = tooltip?.CollectibleObject_GetHeldItemInfo ?? true;

            QuestItemHotbarOnlyPatchesEnabled = harmony?.QuestItemHotbarOnlyPatchesEnabled ?? true;
            var hotbar = harmony?.QuestItemHotbarOnly;
            QuestItemHotbarOnly_ItemSlot_CanTakeFrom = hotbar?.ItemSlot_CanTakeFrom ?? true;
            QuestItemHotbarOnly_ItemSlot_CanHold = hotbar?.ItemSlot_CanHold ?? true;

            QuestItemNoDropOnDeathPatchesEnabled = harmony?.QuestItemNoDropOnDeathPatchesEnabled ?? true;
            var nodrop = harmony?.QuestItemNoDropOnDeath;
            QuestItemNoDropOnDeath_PlayerInventoryManager_OnDeath = nodrop?.PlayerInventoryManager_OnDeath ?? true;

            EntityInfoTextPatchesEnabled = harmony?.EntityInfoTextPatchesEnabled ?? true;
            var infotext = harmony?.EntityInfoText;
            EntityInfoText_Entity_GetInfoText = infotext?.Entity_GetInfoText ?? true;
            EntityInfoText_EntityAgent_GetInfoText = infotext?.EntityAgent_GetInfoText ?? true;

            EntityInteractPatchesEnabled = harmony?.EntityInteractPatchesEnabled ?? true;
            var interact = harmony?.EntityInteract;
            EntityInteract_EntityBehavior_OnInteract = interact?.EntityBehavior_OnInteract ?? true;

            EntityPlayerBotInteractPatchesEnabled = harmony?.EntityPlayerBotInteractPatchesEnabled ?? true;
            var botInteract = harmony?.EntityPlayerBotInteract;
            EntityPlayerBotInteract_EntityPlayerBot_OnInteract = botInteract?.EntityPlayerBot_OnInteract ?? true;

            EntityPrefixAndCreatureNamePatchesEnabled = harmony?.EntityPrefixAndCreatureNamePatchesEnabled ?? true;
            var prefix = harmony?.EntityPrefixAndCreatureName;
            EntityPrefixAndCreatureName_Entity_GetPrefixAndCreatureName = prefix?.Entity_GetPrefixAndCreatureName ?? true;

            EntityShiverStrokePatchesEnabled = harmony?.EntityShiverStrokePatchesEnabled ?? true;
            var shiver = harmony?.EntityShiverStroke;
            EntityShiverStroke_EntityShiver_OnGameTick_StrokeFreq = shiver?.EntityShiver_OnGameTick_StrokeFreq ?? true;

            EntitySoundPitchPatchesEnabled = harmony?.EntitySoundPitchPatchesEnabled ?? true;
            var sound = harmony?.EntitySoundPitch;
            EntitySoundPitch_Entity_PlayEntitySound = sound?.Entity_PlayEntitySound ?? true;

            BlockInteractPatchesEnabled = harmony?.BlockInteractPatchesEnabled ?? true;
            var blockInteract = harmony?.BlockInteract;
            BlockInteract_Block_OnBlockInteractStart = blockInteract?.Block_OnBlockInteractStart ?? true;

            ServerBlockInteractPatchesEnabled = harmony?.ServerBlockInteractPatchesEnabled ?? true;
            var serverBlockInteract = harmony?.ServerBlockInteract;
            ServerBlockInteract_Block_OnBlockInteractStart = serverBlockInteract?.Block_OnBlockInteractStart ?? true;

            QuestItemDropBlockPatchesEnabled = harmony?.QuestItemDropBlockPatchesEnabled ?? true;
            var drop = harmony?.QuestItemDropBlock;
            QuestItemDropBlock_InventoryManager_DropItem = drop?.InventoryManager_DropItem ?? true;

            QuestItemEquipBlockPatchesEnabled = harmony?.QuestItemEquipBlockPatchesEnabled ?? true;
            var equip = harmony?.QuestItemEquipBlock;
            QuestItemEquipBlock_ItemSlotCharacter_CanHold = equip?.ItemSlotCharacter_CanHold ?? true;
            QuestItemEquipBlock_ItemSlotCharacter_CanTakeFrom = equip?.ItemSlotCharacter_CanTakeFrom ?? true;

            QuestItemGroundStorageBlockPatchesEnabled = harmony?.QuestItemGroundStorageBlockPatchesEnabled ?? true;
            var ground = harmony?.QuestItemGroundStorageBlock;
            QuestItemGroundStorageBlock_CollectibleBehaviorGroundStorable_Interact = ground?.CollectibleBehaviorGroundStorable_Interact ?? true;

            Item_InventoryChangeTracking = harmony?.Item_InventoryChangeTracking ?? true;

            ConversablePatchesEnabled = harmony?.ConversablePatchesEnabled ?? true;
            var conversable = harmony?.Conversable;
            Conversable_EntityBehaviorConversable_Controller_DialogTriggers = conversable?.EntityBehaviorConversable_Controller_DialogTriggers ?? true;

            DebugDamagePatchesEnabled = harmony?.DebugDamagePatchesEnabled ?? true;
            var debugDamage = harmony?.DebugDamage;
            DebugDamage_EntityAgent_ReceiveDamage = debugDamage?.EntityAgent_ReceiveDamage ?? true;
        }

        public static bool ItemEnabled(bool perPatch) => ItemAttributePatchesEnabled && perPatch;
        public static bool PlayerEnabled(bool perPatch) => PlayerAttributePatchesEnabled && perPatch;
        public static bool ActionItemModeEnabled(bool perPatch) => ActionItemModePatchesEnabled && perPatch;
        public static bool ItemMoveActingPlayerContextEnabled(bool perPatch) => ItemMoveActingPlayerContextPatchesEnabled && perPatch;
        public static bool ItemTooltipEnabled(bool perPatch) => ItemTooltipPatchesEnabled && perPatch;
        public static bool QuestItemHotbarOnlyEnabled(bool perPatch) => QuestItemHotbarOnlyPatchesEnabled && perPatch;
        public static bool QuestItemNoDropOnDeathEnabled(bool perPatch) => QuestItemNoDropOnDeathPatchesEnabled && perPatch;
        public static bool EntityInfoTextEnabled(bool perPatch) => EntityInfoTextPatchesEnabled && perPatch;
        public static bool EntityInteractEnabled(bool perPatch) => EntityInteractPatchesEnabled && perPatch;
        public static bool EntityPlayerBotInteractEnabled(bool perPatch) => EntityPlayerBotInteractPatchesEnabled && perPatch;
        public static bool EntityPrefixAndCreatureNameEnabled(bool perPatch) => EntityPrefixAndCreatureNamePatchesEnabled && perPatch;
        public static bool EntityShiverStrokeEnabled(bool perPatch) => EntityShiverStrokePatchesEnabled && perPatch;
        public static bool EntitySoundPitchEnabled(bool perPatch) => EntitySoundPitchPatchesEnabled && perPatch;
        public static bool BlockInteractEnabled(bool perPatch) => BlockInteractPatchesEnabled && perPatch;
        public static bool ServerBlockInteractEnabled(bool perPatch) => ServerBlockInteractPatchesEnabled && perPatch;
        public static bool QuestItemDropBlockEnabled(bool perPatch) => QuestItemDropBlockPatchesEnabled && perPatch;
        public static bool QuestItemEquipBlockEnabled(bool perPatch) => QuestItemEquipBlockPatchesEnabled && perPatch;
        public static bool QuestItemGroundStorageBlockEnabled(bool perPatch) => QuestItemGroundStorageBlockPatchesEnabled && perPatch;
        public static bool ConversableEnabled(bool perPatch) => ConversablePatchesEnabled && perPatch;
        public static bool DebugDamageEnabled(bool perPatch) => DebugDamagePatchesEnabled && perPatch;
    }
}
