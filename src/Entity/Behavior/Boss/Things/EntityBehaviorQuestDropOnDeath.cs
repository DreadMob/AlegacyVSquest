using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Drops items on entity death with quest-related conditions:
    /// - onlyIfQuestActive: only drops if player has this quest active
    /// - onlyOncePerQuest: sets a variable so item only drops once per quest instance
    /// </summary>
    public class EntityBehaviorQuestDropOnDeath : EntityBehavior
    {
        private ICoreServerAPI sapi;
        private DropConfig[] drops;

        public EntityBehaviorQuestDropOnDeath(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            var dropsArray = attributes["drops"].AsArray();
            if (dropsArray != null)
            {
                var list = new List<DropConfig>();
                foreach (var dropAttr in dropsArray)
                {
                    var config = new DropConfig
                    {
                        type = dropAttr["type"].AsString("item"),
                        code = dropAttr["code"].AsString(),
                        quantity = dropAttr["quantity"].AsObject<QuantityConfig>() ?? new QuantityConfig { avg = 1 },
                        onlyIfQuestActive = dropAttr["onlyIfQuestActive"].AsString(),
                        onlyOncePerQuest = dropAttr["onlyOncePerQuest"].AsBool(true),
                        variableKey = dropAttr["variableKey"].AsString()
                    };
                    if (!string.IsNullOrWhiteSpace(config.code))
                    {
                        list.Add(config);
                    }
                }
                drops = list.ToArray();
            }
        }

        public override void OnEntityDeath(DamageSource damageSource)
        {
            if (sapi == null || drops == null || drops.Length == 0) return;

            // Get credited players using the same system as boss kill announcements
            var creditedPlayers = GetCreditedPlayers();
            
            if (creditedPlayers.Count == 0)
            {
                // Fallback: try direct killer
                IServerPlayer killerPlayer = null;
                if (damageSource?.SourceEntity is EntityPlayer sourcePlayer)
                {
                    killerPlayer = sourcePlayer.Player as IServerPlayer;
                }
                else if (damageSource?.CauseEntity is EntityPlayer causePlayer)
                {
                    killerPlayer = causePlayer.Player as IServerPlayer;
                }
                
                if (killerPlayer != null)
                {
                    creditedPlayers.Add(killerPlayer);
                }
            }

            if (creditedPlayers.Count == 0) return;

            // Give drops to all credited players
            foreach (var player in creditedPlayers)
            {
                foreach (var drop in drops)
                {
                    TryDrop(drop, player);
                }
            }
        }

        private List<IServerPlayer> GetCreditedPlayers()
        {
            var result = new List<IServerPlayer>();
            if (entity?.WatchedAttributes == null) return result;

            try
            {
                // Same logic as QuestEventHandler
                var wa = entity.WatchedAttributes;
                if (wa == null) return result;

                var dmgTree = wa.GetTreeAttribute("damageByPlayer");
                if (dmgTree == null) return result;

                string[] attackers = wa.GetStringArray("bossCombatAttackers", new string[0]) ?? new string[0];
                if (attackers.Length == 0) return result;

                // Calculate max HP
                float maxHp = 1f;
                var healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
                if (healthTree != null)
                {
                    maxHp = healthTree.GetFloat("maxhealth", 1f);
                }

                if (maxHp <= 0) maxHp = 1f;

                // Count attackers with damage
                int attackersWithDamage = 0;
                foreach (var uid in attackers)
                {
                    if (string.IsNullOrWhiteSpace(uid)) continue;
                    double dmg = dmgTree.GetDouble(uid, 0);
                    if (dmg > 0) attackersWithDamage++;
                }

                // Get min share based on attacker count
                double minShare = GetMinShare(attackersWithDamage);

                // Find credited players
                foreach (var uid in attackers)
                {
                    if (string.IsNullOrWhiteSpace(uid)) continue;
                    
                    double dmg = dmgTree.GetDouble(uid, 0);
                    if (dmg > 0 && dmg / maxHp >= minShare)
                    {
                        var player = sapi.World.PlayerByUid(uid) as IServerPlayer;
                        if (player != null && !result.Contains(player))
                        {
                            result.Add(player);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[EntityBehaviorQuestDropOnDeath] Failed to get credited players: {0}", ex.Message);
            }

            return result;
        }

        private double GetMinShare(int attackersWithDamage)
        {
            // Same logic as QuestEventHandler.GetBossKillCreditMinShare
            if (attackersWithDamage <= 1) return 0.05;
            if (attackersWithDamage == 2) return 0.10;
            if (attackersWithDamage == 3) return 0.15;
            if (attackersWithDamage == 4) return 0.20;
            return 0.25;
        }

        private void TryDrop(DropConfig drop, IServerPlayer player)
        {
            try
            {
                sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: Trying to drop {drop.code} for player {player.PlayerName}");

                // Check if quest is active
                if (!string.IsNullOrWhiteSpace(drop.onlyIfQuestActive))
                {
                    bool isActive = IsQuestActive(player, drop.onlyIfQuestActive);
                    sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: Quest {drop.onlyIfQuestActive} active: {isActive}");
                    if (!isActive)
                    {
                        return;
                    }
                }

                // Check if already dropped for this quest instance
                if (drop.onlyOncePerQuest && !string.IsNullOrWhiteSpace(drop.onlyIfQuestActive))
                {
                    // Variable key format: alstory:questvars:{questId}:{variableKey}
                    // This allows automatic cleanup when quest is reset
                    string varKey = drop.variableKey ?? $"drop_{drop.code.Replace(":", "_")}";
                    string fullVarKey = $"alstory:questvars:{drop.onlyIfQuestActive}:{varKey}";
                    bool alreadyDropped = HasVariable(player, fullVarKey);
                    sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: Variable {fullVarKey} already set: {alreadyDropped}");
                    if (alreadyDropped)
                    {
                        return; // Already dropped
                    }
                    // Mark as dropped
                    SetVariable(player, fullVarKey, "1");
                }

                // Calculate quantity
                int quantity = CalculateQuantity(drop.quantity);
                if (quantity <= 0) return;

                // Use appropriate action to add to player inventory
                var actionRegistry = sapi.ModLoader.GetModSystem<QuestSystem>()?.ActionRegistry;
                sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: ActionRegistry is null: {actionRegistry == null}, type: {drop.type}");
                if (actionRegistry != null)
                {
                    var message = new QuestCompletedMessage { questId = drop.onlyIfQuestActive ?? "unknown" };
                    string[] args = new[] { drop.code, quantity.ToString() };

                    if (drop.type == "actionitem" && actionRegistry.ContainsKey("giveactionitem"))
                    {
                        sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: Executing giveactionitem for {drop.code}");
                        actionRegistry["giveactionitem"].Execute(sapi, message, player, args);
                    }
                    else if (actionRegistry.ContainsKey("giveitem"))
                    {
                        sapi.Logger.Debug($"[vsquest] QuestDropOnDeath: Executing giveitem for {drop.code}");
                        actionRegistry["giveitem"].Execute(sapi, message, player, args);
                    }
                    else
                    {
                        // Fallback: create itemstack and try to give directly
                        ItemStack stack = null;
                        if (drop.type == "item" || drop.type == "actionitem")
                        {
                            var item = sapi.World.GetItem(new AssetLocation(drop.code));
                            if (item != null) stack = new ItemStack(item, quantity);
                        }
                        else if (drop.type == "block")
                        {
                            var block = sapi.World.GetBlock(new AssetLocation(drop.code));
                            if (block != null) stack = new ItemStack(block, quantity);
                        }

                        if (stack != null)
                        {
                            if (!player.InventoryManager.TryGiveItemstack(stack))
                            {
                                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Error($"[vsquest] QuestDropOnDeath error: {e}");
            }
        }

        private bool IsQuestActive(IServerPlayer player, string questId)
        {
            try
            {
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                if (questSystem == null) return false;

                var activeQuests = questSystem.GetPlayerQuests(player.PlayerUID);
                if (activeQuests == null) return false;

                foreach (var quest in activeQuests)
                {
                    if (quest?.questId?.Equals(questId, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool HasVariable(IServerPlayer player, string fullKey)
        {
            try
            {
                var wa = player.Entity?.WatchedAttributes;
                if (wa == null) return false;

                return wa.HasAttribute(fullKey);
            }
            catch
            {
                return false;
            }
        }

        private void SetVariable(IServerPlayer player, string fullKey, string value)
        {
            try
            {
                var wa = player.Entity?.WatchedAttributes;
                if (wa == null) return;

                wa.SetString(fullKey, value);
                wa.MarkPathDirty(fullKey);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[EntityBehaviorQuestDropOnDeath] Failed to set player attribute {0}: {1}", fullKey, ex.Message);
            }
        }

        private int CalculateQuantity(QuantityConfig qty)
        {
            if (qty == null) return 1;

            float avg = qty.avg;
            float variance = qty.var;
            string dist = qty.dist?.ToLowerInvariant() ?? "uniform";

            if (variance <= 0) return (int)Math.Round(avg);

            var rand = sapi.World.Rand;
            float result;

            switch (dist)
            {
                case "invexp":
                    // Inverse exponential distribution
                    result = avg + (float)(Math.Log(1 - rand.NextDouble()) * -variance);
                    break;
                case "gaussian":
                case "normal":
                    // Box-Muller transform for normal distribution
                    double u1 = 1.0 - rand.NextDouble();
                    double u2 = 1.0 - rand.NextDouble();
                    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                    result = avg + (float)(randStdNormal * variance);
                    break;
                default:
                    // Uniform distribution
                    result = avg + (float)((rand.NextDouble() - 0.5) * 2 * variance);
                    break;
            }

            return Math.Max(0, (int)Math.Round(result));
        }

        public override string PropertyName() => "alegacyvsquest:questdropondeath";

        private class DropConfig
        {
            public string type;
            public string code;
            public QuantityConfig quantity;
            public string onlyIfQuestActive;
            public bool onlyOncePerQuest;
            public string variableKey;
        }

        private class QuantityConfig
        {
            public float avg = 1;
            public float var = 0;
            public string dist = "uniform";
        }
    }
}
