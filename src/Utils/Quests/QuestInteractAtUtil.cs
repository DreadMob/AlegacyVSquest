using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class QuestInteractAtUtil
    {
        private static readonly Dictionary<string, long> lastProcessTime = new Dictionary<string, long>();
        private static long lastCleanupMs = 0;
        private const long CleanupIntervalMs = 60000; // Cleanup every minute
        private const long MaxCacheAgeMs = 300000; // Remove entries older than 5 minutes
        
        public static string InteractionKey(int x, int y, int z) => $"interactat_{x}_{y}_{z}";

        /// <summary>
        /// Checks if player is holding the required item in active hotbar slot
        /// </summary>
        private static bool IsHoldingItem(IServerPlayer player, string requiredCodeOrActionItemId)
        {
            if (player?.InventoryManager == null) return false;
            if (string.IsNullOrWhiteSpace(requiredCodeOrActionItemId)) return true;

            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return false;

            // 1) Vanilla item code check (e.g. game:amethyst)
            if (slot.Itemstack.Item?.Code != null
                && slot.Itemstack.Item.Code.ToString().Equals(requiredCodeOrActionItemId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2) Action item id check (e.g. alstory-keepers-key)
            string actionItemId = slot.Itemstack.Attributes?.GetString(ItemAttributeUtils.ActionItemIdKey);
            
            if (!string.IsNullOrWhiteSpace(actionItemId)
                && actionItemId.Equals(requiredCodeOrActionItemId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool TryParsePos(string coordString, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            var coords = coordString.Split(',');
            if (coords.Length != 3) return false;

            return int.TryParse(coords[0], out x)
                && int.TryParse(coords[1], out y)
                && int.TryParse(coords[2], out z);
        }

        public static string[] GetCompletedInteractions(IPlayer player)
        {
            var wa = player?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool HasInteraction(IPlayer player, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(player);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static int CountCompleted(IPlayer player, string[] coordArgs)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(player);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static string[] GetCompletedInteractions(IServerPlayer serverPlayer)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static int CountCompleted(IServerPlayer serverPlayer, string[] coordArgs)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(serverPlayer);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static bool HasInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(serverPlayer);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static bool TryMarkInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string interactionKey = InteractionKey(x, y, z);
            string completedInteractions = wa.GetString("completedInteractions", "");
            var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (completed.Contains(interactionKey)) return false;

            completed.Add(interactionKey);
            wa.SetString("completedInteractions", string.Join(",", completed));
            wa.MarkPathDirty("completedInteractions");
            return true;
        }

        public static void ResetCompletedInteractAtObjectives(Quest quest, IServerPlayer serverPlayer)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = serverPlayer.Entity.WatchedAttributes;

            try
            {
                // Simply clear all completedInteractions for this quest
                // This is more reliable than trying to parse and match coordinates
                wa.SetString("completedInteractions", "");
                wa.MarkPathDirty("completedInteractions");
            }
            catch (Exception)
            {
                // Failed to reset interactions - non-critical
            }
        }

        public static void TryHandleInteractAtObjectives(Quest quest, ActiveQuest activeQuest, IServerPlayer serverPlayer, int[] position, ICoreServerAPI sapi)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;
            if (position == null || position.Length != 3) return;

            long nowMs = sapi.World.ElapsedMilliseconds;
            
            // Periodic cleanup of rate limit cache
            if (nowMs - lastCleanupMs > CleanupIntervalMs)
            {
                lastCleanupMs = nowMs;
                var oldKeys = lastProcessTime.Where(kvp => (nowMs - kvp.Value) > MaxCacheAgeMs).Select(kvp => kvp.Key).ToList();
                foreach (var key in oldKeys) lastProcessTime.Remove(key);
            }

            // Get action objectives from current stage using centralized method
            var actionObjectives = quest?.GetActionObjectives(activeQuest.currentStageIndex);
            
            if (actionObjectives == null || actionObjectives.Count == 0) return;

            var wa = serverPlayer.Entity.WatchedAttributes;
            bool anyChanged = false;

            for (int i = 0; i < actionObjectives.Count; i++)
            {
                var ao = actionObjectives[i];
                if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;

                var coordString = ao.args[0];
                if (string.IsNullOrWhiteSpace(coordString)) continue;

                // Support multiple coordinates separated by | for multiblock structures
                var coords = coordString.Split('|');
                bool matchedAnyCoord = false;
                int matchedX = 0, matchedY = 0, matchedZ = 0;

                foreach (var coord in coords)
                {
                    if (string.IsNullOrWhiteSpace(coord)) continue;
                    if (!TryParsePos(coord.Trim(), out int targetX, out int targetY, out int targetZ)) continue;

                    if (position[0] == targetX && position[1] == targetY && position[2] == targetZ)
                    {
                        matchedAnyCoord = true;
                        matchedX = targetX;
                        matchedY = targetY;
                        matchedZ = targetZ;
                        break;
                    }
                }

                if (!matchedAnyCoord) continue;

                // Check required item if specified (args[1] = item code)
                string requiredItem = ao.args.Length >= 2 ? ao.args[1] : null;

                if (!string.IsNullOrWhiteSpace(requiredItem))
                {
                    if (!IsHoldingItem(serverPlayer, requiredItem))
                    {
                        // Rate limit "not holding item" messages
                        string msgKey = $"msg_{matchedX}_{matchedY}_{matchedZ}";
                        if (lastProcessTime.TryGetValue(msgKey, out long lastMsg) && (nowMs - lastMsg) < 1000)
                        {
                            continue;
                        }
                        lastProcessTime[msgKey] = nowMs;
                        
                        sapi.SendMessage(serverPlayer, GlobalConstants.InfoLogChatGroup, 
                            Lang.Get("alegacyvsquest:interactat-hold-item", requiredItem), 
                            EnumChatType.Notification);
                        continue;
                    }
                }

                // Check if this interaction requires holding (args[2] = hold duration in seconds)
                double requiredHoldSeconds = 0;
                if (ao.args.Length >= 3 && double.TryParse(ao.args[2], out double holdDuration))
                {
                    requiredHoldSeconds = holdDuration;
                }

                // Get max click gap (args[3] = max seconds between clicks, default 1.0)
                double maxClickGapSeconds = 1.0;
                if (ao.args.Length >= 4 && double.TryParse(ao.args[3], out double clickGap))
                {
                    maxClickGapSeconds = clickGap;
                }

                if (requiredHoldSeconds > 0)
                {
                    // This interaction requires holding - rate limit updates to reduce server load
                    // Use objectiveId as the hold key so all blocks in the same objective share one timer
                    string holdKey = !string.IsNullOrWhiteSpace(ao.objectiveId) 
                        ? ao.objectiveId 
                        : $"{matchedX}_{matchedY}_{matchedZ}";
                    
                    string holdStartKey = $"alegacyvsquest:hold_start:{activeQuest.questId}:{holdKey}";
                    string lastClickKey = $"alegacyvsquest:hold_lastclick:{activeQuest.questId}:{holdKey}";
                    string lastPosKey = $"alegacyvsquest:hold_lastpos:{activeQuest.questId}:{holdKey}";
                    string lastProgressKey = $"alegacyvsquest:hold_lastprogress:{activeQuest.questId}:{holdKey}";
                    
                    // Use ElapsedMilliseconds for better server sync
                    // Migrate old Double values to Long if needed
                    long holdStartMs = -1;
                    long lastClickMs = -1;
                    
                    try
                    {
                        holdStartMs = wa.GetLong(holdStartKey, -1);
                    }
                    catch
                    {
                        // Old data was stored as Double (hours), migrate to Long (ms)
                        double oldHours = wa.GetDouble(holdStartKey, -1);
                        if (oldHours >= 0)
                        {
                            // Convert hours to ms and store
                            holdStartMs = nowMs - (long)(oldHours * 3600 * 1000);
                            wa.SetLong(holdStartKey, holdStartMs);
                            wa.MarkPathDirty(holdStartKey);
                        }
                    }
                    
                    try
                    {
                        lastClickMs = wa.GetLong(lastClickKey, -1);
                    }
                    catch
                    {
                        // Old data was stored as Double (hours), migrate to Long (ms)
                        double oldHours = wa.GetDouble(lastClickKey, -1);
                        if (oldHours >= 0)
                        {
                            lastClickMs = nowMs - (long)(oldHours * 3600 * 1000);
                            wa.SetLong(lastClickKey, lastClickMs);
                            wa.MarkPathDirty(lastClickKey);
                        }
                    }
                    
                    string lastPos = wa.GetString(lastPosKey, "");
                    string currentPosKey = $"{matchedX}_{matchedY}_{matchedZ}";
                    
                    // Rate limit hold updates - only process every 200ms AFTER hold started
                    if (holdStartMs > 0)
                    {
                        string holdRateKey = $"holdrate_{serverPlayer.PlayerUID}_{holdKey}";
                        if (lastProcessTime.TryGetValue(holdRateKey, out long lastHoldUpdate) && (nowMs - lastHoldUpdate) < 200)
                        {
                            return; // Skip - too soon since last update
                        }
                        lastProcessTime[holdRateKey] = nowMs;
                    }
                    
                    // Check if too much time passed since last click
                    long maxClickGapMs = (long)(maxClickGapSeconds * 1000);
                    if (lastClickMs > 0 && (nowMs - lastClickMs) > maxClickGapMs)
                    {
                        wa.RemoveAttribute(holdStartKey);
                        wa.RemoveAttribute(lastProgressKey);
                        holdStartMs = -1;
                    }
                    
                    // Check if player clicked on a different block - reset progress
                    if (!string.IsNullOrEmpty(lastPos) && lastPos != currentPosKey)
                    {
                        wa.RemoveAttribute(holdStartKey);
                        wa.RemoveAttribute(lastProgressKey);
                        holdStartMs = -1;
                    }
                    
                    // Update last click time
                    wa.SetLong(lastClickKey, nowMs);
                    
                    long requiredHoldMs = (long)(requiredHoldSeconds * 1000);

                    if (holdStartMs < 0)
                    {
                        // First click - start holding timer
                        wa.SetLong(holdStartKey, nowMs);
                        wa.SetString(lastPosKey, currentPosKey);
                        wa.SetInt(lastProgressKey, 0);
                        wa.MarkPathDirty(holdStartKey);
                        wa.MarkPathDirty(lastPosKey);
                        wa.MarkPathDirty(lastProgressKey);
                        
                        // Play sound to indicate holding started
                        float pitch = (float)sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                        sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"), matchedX, matchedY, matchedZ, null, pitch, 0.5f);
                        
                        // Show lore message
                        string loreKey = $"{activeQuest.questId}-obj-{ao.objectiveId}-hold";
                        string loreMessage = Lang.GetIfExists(loreKey);
                        if (string.IsNullOrWhiteSpace(loreMessage))
                        {
                            loreMessage = "Удержание...";
                        }
                        
                        // Show colored message in chat
                        string coloredMessage = $"<font color=\"#9370DB\">{loreMessage}</font>";
                        sapi.SendMessage(serverPlayer, GlobalConstants.CurrentChatGroup, coloredMessage, EnumChatType.Notification);
                        
                        return;
                    }

                    long heldMs = nowMs - holdStartMs;
                    
                    if (heldMs < requiredHoldMs)
                    {
                        // Still holding, not enough time yet
                        float progress = (float)heldMs / requiredHoldMs;
                        int progressPercent = (int)(progress * 100);
                        int lastProgress = wa.GetInt(lastProgressKey, 0);
                        
                        // Only show progress every 15% to reduce spam
                        if (progressPercent >= lastProgress + 15)
                        {
                            wa.SetInt(lastProgressKey, progressPercent);
                            wa.MarkPathDirty(lastProgressKey);
                            
                            // Play sound during holding
                            float pitch = (float)sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"), matchedX, matchedY, matchedZ, null, pitch, 0.3f);
                            
                            // Show lore message with progress
                            string loreKey = $"{activeQuest.questId}-obj-{ao.objectiveId}-hold";
                            string loreMessage = Lang.GetIfExists(loreKey);
                            if (string.IsNullOrWhiteSpace(loreMessage))
                            {
                                loreMessage = "Удержание";
                            }
                            
                            // Create progress bar using unicode blocks
                            int barLength = 20;
                            int filledLength = (int)(progress * barLength);
                            string progressBar = new string('█', filledLength) + new string('░', barLength - filledLength);
                            
                            // Color: purple for message, cyan for progress bar, yellow for percentage
                            string displayMessage = $"<font color=\"#9370DB\">{loreMessage}</font> <font color=\"#00CED1\">[{progressBar}]</font> <font color=\"#FFD700\">{progressPercent}%</font>";
                            
                            sapi.SendMessage(serverPlayer, GlobalConstants.CurrentChatGroup, displayMessage, EnumChatType.Notification);
                        }
                        
                        return;
                    }

                    // Held long enough - clear timer and continue to process interaction
                    wa.RemoveAttribute(holdStartKey);
                    wa.RemoveAttribute(lastPosKey);
                    wa.RemoveAttribute(lastClickKey);
                    wa.RemoveAttribute(lastProgressKey);
                    
                    // Clean rate limit keys
                    lastProcessTime.Remove($"holdrate_{serverPlayer.PlayerUID}_{holdKey}");
                    lastProcessTime.Remove($"sound_{holdKey}");
                    
                    // Execute cooldownblock actions for all coordinates in this objective
                    var cooldownCoords = coordString.Split('|');
                    foreach (var cooldownCoord in cooldownCoords)
                    {
                        if (string.IsNullOrWhiteSpace(cooldownCoord)) continue;
                        if (!TryParsePos(cooldownCoord.Trim(), out int cx, out int cy, out int cz)) continue;
                        
                        // Execute cooldownblock action programmatically
                        try
                        {
                            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                            if (questSystem?.ActionRegistry != null && questSystem.ActionRegistry.TryGetValue("cooldownblock", out var cooldownAction))
                            {
                                string[] cooldownArgs = new string[] { "2", $"{cx},{cy},{cz}" };
                                cooldownAction.Execute(sapi, null, serverPlayer, cooldownArgs);
                            }
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Warning("[alegacyvsquest] Failed to execute cooldownblock action: {0}", ex.Message);
                        }
                    }
                }
                else
                {
                    // Non-hold interaction - apply rate limiting
                    string posKey = $"{serverPlayer.PlayerUID}_{matchedX}_{matchedY}_{matchedZ}";
                    if (lastProcessTime.TryGetValue(posKey, out long lastTime) && (nowMs - lastTime) < 200)
                    {
                        continue;
                    }
                    lastProcessTime[posKey] = nowMs;
                }

                bool changed = TryMarkInteraction(serverPlayer, matchedX, matchedY, matchedZ);
                
                if (!changed) continue;

                anyChanged = true;
                bool completableNow = true;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactat", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                }
                catch
                {
                    completableNow = true;
                }

                string completionKey = !string.IsNullOrWhiteSpace(ao.objectiveId)
                    ? ao.objectiveId
                    : InteractionKey(matchedX, matchedY, matchedZ);

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, completionKey, completableNow);
            }

            if (!anyChanged) return;

            for (int i = 0; i < actionObjectives.Count; i++)
            {
                var ao = actionObjectives[i];
                if (ao?.id != "interactcount") continue;

                bool completableNow;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactcount", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                    else
                    {
                        completableNow = false;
                    }
                }
                catch
                {
                    completableNow = false;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, ao.objectiveId, completableNow);
            }
        }
    }
}
