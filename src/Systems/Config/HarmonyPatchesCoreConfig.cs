namespace VsQuest
{
    public class HarmonyPatchesCoreConfig
    {
        public bool ItemAttributePatchesEnabled { get; set; } = true;
        public bool PlayerAttributePatchesEnabled { get; set; } = true;
        public ItemAttributePatchesCoreConfig ItemAttribute { get; set; } = new ItemAttributePatchesCoreConfig();
        public PlayerAttributePatchesCoreConfig PlayerAttribute { get; set; } = new PlayerAttributePatchesCoreConfig();
        public bool ActionItemModePatchesEnabled { get; set; } = true;
        public ActionItemModePatchesCoreConfig ActionItemMode { get; set; } = new ActionItemModePatchesCoreConfig();
        public bool ItemMoveActingPlayerContextPatchesEnabled { get; set; } = true;
        public ItemMoveActingPlayerContextPatchesCoreConfig ItemMoveActingPlayerContext { get; set; } = new ItemMoveActingPlayerContextPatchesCoreConfig();
        public bool ItemTooltipPatchesEnabled { get; set; } = true;
        public ItemTooltipPatchesCoreConfig ItemTooltip { get; set; } = new ItemTooltipPatchesCoreConfig();
        public bool QuestItemHotbarOnlyPatchesEnabled { get; set; } = true;
        public QuestItemHotbarOnlyPatchesCoreConfig QuestItemHotbarOnly { get; set; } = new QuestItemHotbarOnlyPatchesCoreConfig();
        public bool QuestItemNoDropOnDeathPatchesEnabled { get; set; } = true;
        public QuestItemNoDropOnDeathPatchesCoreConfig QuestItemNoDropOnDeath { get; set; } = new QuestItemNoDropOnDeathPatchesCoreConfig();
        public bool EntityInfoTextPatchesEnabled { get; set; } = true;
        public EntityInfoTextPatchesCoreConfig EntityInfoText { get; set; } = new EntityInfoTextPatchesCoreConfig();
        public bool EntityInteractPatchesEnabled { get; set; } = true;
        public EntityInteractPatchesCoreConfig EntityInteract { get; set; } = new EntityInteractPatchesCoreConfig();
        public bool EntityPlayerBotInteractPatchesEnabled { get; set; } = true;
        public EntityPlayerBotInteractPatchesCoreConfig EntityPlayerBotInteract { get; set; } = new EntityPlayerBotInteractPatchesCoreConfig();
        public bool EntityPrefixAndCreatureNamePatchesEnabled { get; set; } = true;
        public EntityPrefixAndCreatureNamePatchesCoreConfig EntityPrefixAndCreatureName { get; set; } = new EntityPrefixAndCreatureNamePatchesCoreConfig();
        public bool EntityShiverStrokePatchesEnabled { get; set; } = true;
        public EntityShiverStrokePatchesCoreConfig EntityShiverStroke { get; set; } = new EntityShiverStrokePatchesCoreConfig();
        public bool EntitySoundPitchPatchesEnabled { get; set; } = true;
        public EntitySoundPitchPatchesCoreConfig EntitySoundPitch { get; set; } = new EntitySoundPitchPatchesCoreConfig();
        public bool BlockInteractPatchesEnabled { get; set; } = true;
        public BlockInteractPatchesCoreConfig BlockInteract { get; set; } = new BlockInteractPatchesCoreConfig();
        public bool ServerBlockInteractPatchesEnabled { get; set; } = true;
        public ServerBlockInteractPatchesCoreConfig ServerBlockInteract { get; set; } = new ServerBlockInteractPatchesCoreConfig();
        public bool QuestItemDropBlockPatchesEnabled { get; set; } = true;
        public QuestItemDropBlockPatchesCoreConfig QuestItemDropBlock { get; set; } = new QuestItemDropBlockPatchesCoreConfig();
        public bool QuestItemEquipBlockPatchesEnabled { get; set; } = true;
        public QuestItemEquipBlockPatchesCoreConfig QuestItemEquipBlock { get; set; } = new QuestItemEquipBlockPatchesCoreConfig();
        public bool QuestItemGroundStorageBlockPatchesEnabled { get; set; } = true;
        public QuestItemGroundStorageBlockPatchesCoreConfig QuestItemGroundStorageBlock { get; set; } = new QuestItemGroundStorageBlockPatchesCoreConfig();
        public bool ConversablePatchesEnabled { get; set; } = true;
        public ConversablePatchesCoreConfig Conversable { get; set; } = new ConversablePatchesCoreConfig();
        public bool DebugDamagePatchesEnabled { get; set; } = true;
        public DebugDamagePatchesCoreConfig DebugDamage { get; set; } = new DebugDamagePatchesCoreConfig();
        public bool Item_InventoryChangeTracking { get; set; } = true;
    }

    public class ItemAttributePatchesCoreConfig
    {
        public bool CollectibleObject_GetHeldItemName { get; set; } = true;
        public bool ModSystemWearableStats_onFootStep { get; set; } = true;
        public bool EntityBehaviorHealth_OnFallToGround { get; set; } = true;
        public bool EntityBehaviorTemporalStabilityAffected_OnGameTick { get; set; } = true;
        public bool CollectibleObject_GetAttackPower { get; set; } = true;
        public bool CollectibleObject_OnHeldAttackStart_AttackSpeed { get; set; } = true;
        public bool CollectibleBehaviorWearable_GetWarmth { get; set; } = true;
        public bool EntityBehaviorBodyTemperature_WarmthBonus { get; set; } = true;
        public bool CollectibleObject_GetMiningSpeed_MiningSpeedMult { get; set; } = true;
        public bool ModSystemWearableStats_handleDamaged { get; set; } = true;
        public bool ModSystemWearableStats_updateWearableStats { get; set; } = true;
        public bool CollectibleObject_TryMergeStacks_SecondChanceCharge { get; set; } = true;
    }

    public class PlayerAttributePatchesCoreConfig
    {
        public bool ModSystemWearableStats_handleDamaged_PlayerAttributes { get; set; } = true;
        public bool EntityAgent_OnGameTick_Unified { get; set; } = true;
        public bool EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance { get; set; } = true;
        public bool EntityBehaviorHealth_OnEntityDeath_SecondChanceReset { get; set; } = true;
        public bool EntityAgent_ReceiveDamage_PlayerAttackPower { get; set; } = true;
        public bool EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth { get; set; } = true;
    }

    public class ActionItemModePatchesCoreConfig
    {
        public bool CollectibleObject_GetToolModes_ActionItemModes { get; set; } = true;
        public bool CollectibleObject_GetToolMode_ActionItemModes { get; set; } = true;
        public bool CollectibleObject_SetToolMode_ActionItemModes { get; set; } = true;
    }

    public class ItemMoveActingPlayerContextPatchesCoreConfig
    {
        public bool InventoryBase_ActivateSlot { get; set; } = true;
        public bool ItemSlot_ActivateSlot { get; set; } = true;
    }

    public class ItemTooltipPatchesCoreConfig
    {
        public bool CollectibleObject_GetHeldItemInfo { get; set; } = true;
    }

    public class QuestItemHotbarOnlyPatchesCoreConfig
    {
        public bool ItemSlot_CanTakeFrom { get; set; } = true;
        public bool ItemSlot_CanHold { get; set; } = true;
    }

    public class QuestItemNoDropOnDeathPatchesCoreConfig
    {
        public bool PlayerInventoryManager_OnDeath { get; set; } = true;
    }

    public class EntityInfoTextPatchesCoreConfig
    {
        public bool Entity_GetInfoText { get; set; } = true;
        public bool EntityAgent_GetInfoText { get; set; } = true;
    }

    public class EntityInteractPatchesCoreConfig
    {
        public bool EntityBehavior_OnInteract { get; set; } = true;
    }

    public class EntityPlayerBotInteractPatchesCoreConfig
    {
        public bool EntityPlayerBot_OnInteract { get; set; } = true;
    }

    public class EntityPrefixAndCreatureNamePatchesCoreConfig
    {
        public bool Entity_GetPrefixAndCreatureName { get; set; } = true;
    }

    public class EntityShiverStrokePatchesCoreConfig
    {
        public bool EntityShiver_OnGameTick_StrokeFreq { get; set; } = true;
    }

    public class EntitySoundPitchPatchesCoreConfig
    {
        public bool Entity_PlayEntitySound { get; set; } = true;
    }

    public class BlockInteractPatchesCoreConfig
    {
        public bool Block_OnBlockInteractStart { get; set; } = true;
    }

    public class ServerBlockInteractPatchesCoreConfig
    {
        public bool Block_OnBlockInteractStart { get; set; } = true;
    }

    public class QuestItemDropBlockPatchesCoreConfig
    {
        public bool InventoryManager_DropItem { get; set; } = true;
    }

    public class QuestItemEquipBlockPatchesCoreConfig
    {
        public bool ItemSlotCharacter_CanHold { get; set; } = true;
        public bool ItemSlotCharacter_CanTakeFrom { get; set; } = true;
    }

    public class QuestItemGroundStorageBlockPatchesCoreConfig
    {
        public bool CollectibleBehaviorGroundStorable_Interact { get; set; } = true;
    }

    public class ConversablePatchesCoreConfig
    {
        public bool EntityBehaviorConversable_Controller_DialogTriggers { get; set; } = true;
    }

    public class DebugDamagePatchesCoreConfig
    {
        public bool EntityAgent_ReceiveDamage { get; set; } = true;
    }
}
