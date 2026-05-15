using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        private void TrySpawnIfPlayerNearby(HollowTrialConfig cfg, HollowTrialStateEntry entry, double nowHours)
        {
            if (cfg == null || entry == null) return;
            if (entry.anchorPoints == null || entry.anchorPoints.Count == 0) return;

            // Use first anchor point (trials don't relocate)
            var anchor = entry.anchorPoints[0];
            var point = new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
            int dim = anchor.dim;

            float range = cfg.GetActivationRange(coreConfig);

            // Find the nearest player and determine their quest tier
            var activatingPlayer = FindNearestPlayerWithTrialQuest(cfg, point, dim, range);
            if (activatingPlayer == null) return;

            int spawnTier = DeterminePlayerTrialTier(cfg, activatingPlayer);
            if (spawnTier < 1) spawnTier = 1;

            TrySpawnBoss(cfg, point, dim, anchor, spawnTier, activatingPlayer.PlayerUID);
        }

        /// <summary>
        /// Find the nearest online player within range who has a trial quest for this boss.
        /// Falls back to any player in range if none have a quest.
        /// </summary>
        private IServerPlayer FindNearestPlayerWithTrialQuest(HollowTrialConfig cfg, Vec3d point, int dim, float range)
        {
            if (range <= 0) range = 120f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return null;

            double rangeSq = range * (double)range;
            IServerPlayer nearest = null;
            IServerPlayer nearestWithQuest = null;
            double nearestDistSq = double.MaxValue;
            double nearestQuestDistSq = double.MaxValue;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe?.Pos == null) continue;
                if (pe.Pos.Dimension != dim) continue;

                double dx = pe.Pos.X - point.X;
                double dy = pe.Pos.Y - point.Y;
                double dz = pe.Pos.Z - point.Z;
                double distSq = dx * dx + dy * dy + dz * dz;

                if (distSq > rangeSq) continue;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = sp;
                }

                // Check if player has a trial quest for this boss
                if (questSystem != null && cfg.tiers != null)
                {
                    var playerQuests = questSystem.GetPlayerQuests(sp.PlayerUID);
                    if (playerQuests != null)
                    {
                        foreach (var aq in playerQuests)
                        {
                            foreach (var tierKvp in cfg.tiers)
                            {
                                if (string.Equals(aq.questId, tierKvp.Value?.questId, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (distSq < nearestQuestDistSq)
                                    {
                                        nearestQuestDistSq = distSq;
                                        nearestWithQuest = sp;
                                    }
                                    goto nextPlayer;
                                }
                            }
                        }
                    }
                }

                nextPlayer:;
            }

            return nearestWithQuest ?? nearest;
        }

        /// <summary>
        /// Determine which tier quest a player has for this boss. Returns 1 if no quest found.
        /// </summary>
        private int DeterminePlayerTrialTier(HollowTrialConfig cfg, IServerPlayer player)
        {
            if (cfg?.tiers == null || player == null) return 1;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return 1;

            var playerQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (playerQuests == null) return 1;

            // Find the highest tier quest the player has for this boss
            int highestTier = 0;
            foreach (var aq in playerQuests)
            {
                foreach (var tierKvp in cfg.tiers)
                {
                    if (string.Equals(aq.questId, tierKvp.Value?.questId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (tierKvp.Key > highestTier) highestTier = tierKvp.Key;
                    }
                }
            }

            return highestTier > 0 ? highestTier : 1;
        }

        private void TrySpawnBoss(HollowTrialConfig cfg, Vec3d point, int dim, HollowTrialAnchorPoint anchor, int tier, string activatingPlayerUid = null)
        {
            if (cfg == null || point == null) return;

            // Don't spawn if already alive
            var existing = entityTracker?.GetTrackedEntity(cfg.trialKey);
            if (existing != null && existing.Alive) return;

            try
            {
                var entityCode = cfg.GetEntityCode();
                var type = sapi.World.GetEntityType(new AssetLocation(entityCode));
                if (type == null)
                {
                    DebugLog($"Spawn failed: entity type not found for '{entityCode}'");
                    return;
                }

                Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null)
                {
                    DebugLog($"Spawn failed: entity create returned null for '{entityCode}'");
                    return;
                }

                // Set quest target ID so killactiontarget objective works
                if (entity.WatchedAttributes != null)
                {
                    entity.WatchedAttributes.SetString("alegacyvsquest:killaction:targetid", cfg.trialKey);
                    entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:killaction:targetid");
                }

                // Set spawner anchor for leash behavior
                EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, new BlockPos((int)point.X, (int)point.Y, (int)point.Z, dim));

                entity.Pos.SetPosWithDimension(new Vec3d(point.X, point.Y + dim * 32768.0, point.Z));
                entity.Pos.SetFrom(entity.Pos);

                sapi.World.SpawnEntity(entity);

                // Apply tier-based stats AFTER spawn (so they don't get overwritten by behavior Initialize)
                float killCountScale = 1.0f;
                if (!string.IsNullOrWhiteSpace(activatingPlayerUid))
                {
                    var repMgr = GetReputationManager();
                    int killCount = repMgr?.GetKillCount(activatingPlayerUid, cfg.trialKey) ?? 0;
                    killCountScale = TrialReputationManager.GetKillCountScaling(killCount);
                }
                ApplyTierStats(entity, cfg, tier, killCountScale);

                entityTracker?.ForceScan();

                DebugLog($"Spawned trial boss '{cfg.trialKey}' tier={tier} at {point.X:0},{point.Y:0},{point.Z:0} dim={dim}");
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Failed to spawn trial boss '{0}': {1}", cfg.trialKey, ex.Message);
            }
        }

        /// <summary>
        /// Apply tier-based stats to a freshly spawned trial boss entity.
        /// Sets HP, damage multiplier, speed multiplier, enrage timer, and ability scaling.
        /// </summary>
        private void ApplyTierStats(Entity entity, HollowTrialConfig cfg, int tier, float killCountScale = 1.0f)
        {
            var tierData = cfg.GetTierData(tier);
            if (tierData == null) return;

            // Get active weekly modifier
            var modifier = (TrialModifierType)(state?.activeModifier ?? 0);

            // Store the spawn tier on the entity for other systems to read
            entity.WatchedAttributes.SetInt("alegacyvsquest:trial:spawnTier", tier);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:spawnTier");

            // Store active modifier on entity for behaviors to read
            entity.WatchedAttributes.SetInt("alegacyvsquest:trial:activeModifier", (int)modifier);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:activeModifier");

            // Store kill count scale for reference
            if (killCountScale > 1.001f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:killCountScale", killCountScale);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:killCountScale");
            }

            // Apply max health (with modifier + kill count scaling)
            if (tierData.maxHealth > 0)
            {
                float finalHp = tierData.maxHealth * TrialWeeklyModifierUtils.GetHpMultiplier(modifier) * killCountScale;
                var healthTree = entity.WatchedAttributes.GetOrAddTreeAttribute("health");
                healthTree.SetFloat("maxhealth", finalHp);
                healthTree.SetFloat("basemaxhealth", finalHp);
                healthTree.SetFloat("currenthealth", finalHp);
                entity.WatchedAttributes.MarkPathDirty("health");
            }

            // Apply damage multiplier (with modifier + kill count scaling)
            float damageMult = tierData.damageMult * TrialWeeklyModifierUtils.GetDamageMultiplier(modifier) * killCountScale;
            if (damageMult != 1.0f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:damageMult", damageMult);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:damageMult");
            }

            // Apply speed multiplier (with modifier)
            float speedMult = tierData.speedMult * TrialWeeklyModifierUtils.GetSpeedMultiplier(modifier);
            if (speedMult != 1.0f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:speedMult", speedMult);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:speedMult");

                float speedBonus = speedMult - 1.0f;
                if (Math.Abs(speedBonus) > 0.001f)
                {
                    entity.Stats?.Set("walkspeed", "trialtier", speedBonus, false);
                }
            }

            // Apply enrage timer override (with modifier)
            if (tierData.enrageTimerSeconds > 0)
            {
                double enrageTimer = tierData.enrageTimerSeconds * TrialWeeklyModifierUtils.GetEnrageTimerMultiplier(modifier);
                entity.WatchedAttributes.SetDouble("alegacyvsquest:trial:enrageTimerSeconds", enrageTimer);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:enrageTimerSeconds");
            }

            // Apply ability scaling multiplier (with modifier)
            float abilityScale = tier switch
            {
                1 => 1.0f,
                2 => 1.3f,
                3 => 1.6f,
                _ => 1.0f
            };
            entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:abilityDamageMult", abilityScale);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:abilityDamageMult");

            float abilityCooldownMult = tier switch
            {
                1 => 1.0f,
                2 => 0.8f,
                3 => 0.65f,
                _ => 1.0f
            };
            // Stack with weekly modifier
            abilityCooldownMult *= TrialWeeklyModifierUtils.GetAbilityCooldownMultiplier(modifier);
            entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:abilityCooldownMult", abilityCooldownMult);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:abilityCooldownMult");

            // Store vulnerability window modifier for the behavior to read
            float vulnMult = TrialWeeklyModifierUtils.GetVulnerabilityDurationMultiplier(modifier);
            if (vulnMult != 1.0f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:vulnDurationMult", vulnMult);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:vulnDurationMult");
            }

            // Store vulnerability damage multiplier (GlassCannon modifier)
            float vulnDmgMult = TrialWeeklyModifierUtils.GetVulnerabilityDamageMultiplier(modifier);
            if (vulnDmgMult != 1.0f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:vulnDamageMult", vulnDmgMult);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:vulnDamageMult");
            }

            // Store healing reduction modifier for player-side systems
            float healMult = TrialWeeklyModifierUtils.GetPlayerHealingMultiplier(modifier);
            if (healMult != 1.0f)
            {
                entity.WatchedAttributes.SetFloat("alegacyvsquest:trial:playerHealingMult", healMult);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:playerHealingMult");
            }

            // Store boss regen flag
            if (TrialWeeklyModifierUtils.IsBossRegenActive(modifier))
            {
                entity.WatchedAttributes.SetBool("alegacyvsquest:trial:bossRegen", true);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:bossRegen");
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || entity == null) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            var cfg = FindConfig(qt.TargetId);
            if (cfg == null) return;

            // This is a trial boss death
            var entry = GetOrCreateEntry(cfg.trialKey);
            double nowHours = sapi.World.Calendar.TotalHours;

            entry.deadUntilTotalHours = nowHours + cfg.GetRespawnHours(coreConfig);
            stateDirty = true;

            DebugLog($"Trial boss '{cfg.trialKey}' died. Respawn at {entry.deadUntilTotalHours:0.0}h (in {cfg.GetRespawnHours(coreConfig):0.0}h)");

            // Trigger collapse animation on the anchor block(s)
            try
            {
                if (entry.anchorPoints != null)
                {
                    foreach (var anchor in entry.anchorPoints)
                    {
                        var pos = new BlockPos(anchor.x, anchor.y, anchor.z, anchor.dim);
                        var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                        be?.TriggerCollapseAnimation();
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Collapse animation failed for '{0}': {1}", cfg.trialKey, ex.Message);
            }

            // Process kill credit, challenges, rewards
            try
            {
                OnTrialBossKilled(entity, damageSource);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] OnTrialBossKilled failed for '{0}': {1}", cfg.trialKey, ex.Message);
            }
        }

        /// <summary>
        /// Force respawn a trial boss by anchor friendly ID. Used by admin command.
        /// Finds which boss is assigned to this anchor and spawns it at tier 1.
        /// </summary>
        public bool ForceRespawnByAnchor(string anchorFriendlyId, out string error, out string spawnedInfo)
        {
            error = null;
            spawnedInfo = null;

            if (string.IsNullOrWhiteSpace(anchorFriendlyId))
            {
                error = "Anchor ID is empty.";
                return false;
            }

            // Search all entries for this anchor
            if (state?.entries == null)
            {
                error = "No trial state entries.";
                return false;
            }

            foreach (var entry in state.entries)
            {
                if (entry.anchorPoints == null) continue;

                foreach (var anchor in entry.anchorPoints)
                {
                    if (string.Equals(anchor.friendlyId, anchorFriendlyId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found the anchor — get the config
                        var cfg = FindConfig(entry.trialKey);
                        if (cfg == null)
                        {
                            error = $"Config not found for trialKey '{entry.trialKey}' (anchor '{anchorFriendlyId}').";
                            return false;
                        }

                        // Check if boss is already alive
                        var existing = entityTracker?.GetTrackedEntity(entry.trialKey);
                        if (existing != null && existing.Alive)
                        {
                            error = $"Boss '{entry.trialKey}' is already alive.";
                            return false;
                        }

                        // Reset dead timer and spawn
                        entry.deadUntilTotalHours = 0;
                        stateDirty = true;

                        var point = new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
                        TrySpawnBoss(cfg, point, anchor.dim, anchor, 1);

                        spawnedInfo = $"'{entry.trialKey}' at anchor '{anchorFriendlyId}' ({anchor.x},{anchor.y},{anchor.z})";
                        return true;
                    }
                }
            }

            // Not found — list available anchors
            var available = new List<string>();
            foreach (var entry in state.entries)
            {
                if (entry.anchorPoints == null) continue;
                foreach (var a in entry.anchorPoints)
                {
                    if (!string.IsNullOrWhiteSpace(a.friendlyId))
                        available.Add($"{a.friendlyId} ({entry.trialKey})");
                }
            }

            error = $"Anchor '{anchorFriendlyId}' not found. Available: [{string.Join(", ", available)}]";
            return false;
        }

        /// <summary>
        /// Force respawn a specific trial boss at a specific anchor. Used internally.
        /// </summary>
        public bool ForceRespawn(string trialKey, out string error, string anchorFriendlyId = null)
        {
            error = null;

            if (!IsTrialActive(trialKey))
            {
                error = $"Trial '{trialKey}' is not in the current rotation. Active: [{string.Join(", ", GetActiveTrialKeys())}]";
                return false;
            }

            var cfg = FindConfig(trialKey);
            if (cfg == null)
            {
                error = $"No config found for trial '{trialKey}'.";
                return false;
            }

            var existing = entityTracker?.GetTrackedEntity(trialKey);
            if (existing != null && existing.Alive)
            {
                error = $"Trial boss '{trialKey}' is already alive.";
                return false;
            }

            var entry = GetOrCreateEntry(trialKey);
            entry.deadUntilTotalHours = 0;
            stateDirty = true;

            // Find the anchor to spawn at
            if (entry.anchorPoints == null || entry.anchorPoints.Count == 0)
            {
                error = $"No anchors registered for '{trialKey}'.";
                return false;
            }

            HollowTrialAnchorPoint anchor = null;
            if (!string.IsNullOrWhiteSpace(anchorFriendlyId))
            {
                anchor = entry.anchorPoints.Find(a =>
                    string.Equals(a.friendlyId, anchorFriendlyId, StringComparison.OrdinalIgnoreCase));
                if (anchor == null)
                {
                    var available = string.Join(", ", entry.anchorPoints
                        .Where(a => !string.IsNullOrWhiteSpace(a.friendlyId))
                        .Select(a => a.friendlyId));
                    error = $"Anchor '{anchorFriendlyId}' not found for '{trialKey}'. Available: [{available}]";
                    return false;
                }
            }
            else
            {
                anchor = entry.anchorPoints[0];
            }

            var point = new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
            int dim = anchor.dim;
            TrySpawnBoss(cfg, point, dim, anchor, 1);

            return true;
        }

        /// <summary>
        /// Get the status of a specific trial boss.
        /// </summary>
        public string GetTrialStatus(string trialKey)
        {
            var cfg = FindConfig(trialKey);
            if (cfg == null) return "unknown";

            var entry = GetOrCreateEntry(trialKey);
            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;

            var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
            if (bossEntity != null && bossEntity.Alive) return "alive";

            if (entry.deadUntilTotalHours > nowHours)
            {
                double hoursLeft = entry.deadUntilTotalHours - nowHours;
                return $"dead (respawn in {hoursLeft:0.0}h)";
            }

            return "waiting (no player nearby)";
        }

        // ---- Anchor management ----

        /// <summary>
        /// Register an anchor point for a trial boss.
        /// </summary>
        public void SetAnchorPoint(string trialKey, string anchorId, BlockPos pos, float yOffset)
        {
            if (string.IsNullOrWhiteSpace(trialKey) || pos == null) return;

            var cfg = FindConfig(trialKey);
            if (cfg == null)
            {
                sapi?.Logger?.Warning("[HollowTrialSystem] SetAnchorPoint: no config for trialKey '{0}'", trialKey);
                return;
            }

            var entry = GetOrCreateEntry(trialKey);
            entry.anchorPoints ??= new List<HollowTrialAnchorPoint>();

            // Check if anchor already exists
            for (int i = 0; i < entry.anchorPoints.Count; i++)
            {
                if (string.Equals(entry.anchorPoints[i].anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    // Update existing
                    entry.anchorPoints[i].x = pos.X;
                    entry.anchorPoints[i].y = pos.Y;
                    entry.anchorPoints[i].z = pos.Z;
                    entry.anchorPoints[i].dim = pos.dimension;
                    entry.anchorPoints[i].yOffset = yOffset;
                    stateDirty = true;
                    return;
                }
            }

            // Add new — generate friendly ID (anchor1, anchor2, ...)
            int nextFriendlyNum = entry.anchorPoints.Count + 1;
            string friendlyId = $"anchor{nextFriendlyNum}";

            entry.anchorPoints.Add(new HollowTrialAnchorPoint
            {
                anchorId = anchorId,
                friendlyId = friendlyId,
                x = pos.X,
                y = pos.Y,
                z = pos.Z,
                dim = pos.dimension,
                yOffset = yOffset
            });
            stateDirty = true;

            DebugLog($"Anchor registered: trialKey={trialKey} friendly={friendlyId} id={anchorId} pos={pos.X},{pos.Y},{pos.Z} dim={pos.dimension}");
        }

        /// <summary>
        /// Unregister an anchor point for a trial boss.
        /// </summary>
        public void UnsetAnchorPoint(string trialKey, string anchorId, BlockPos pos)
        {
            if (string.IsNullOrWhiteSpace(trialKey) || string.IsNullOrWhiteSpace(anchorId)) return;

            var entry = GetOrCreateEntry(trialKey);
            if (entry?.anchorPoints == null) return;

            for (int i = entry.anchorPoints.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entry.anchorPoints[i].anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    entry.anchorPoints.RemoveAt(i);
                    stateDirty = true;

                    // Despawn boss if anchor removed
                    var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
                    if (bossEntity != null && bossEntity.Alive)
                    {
                        sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    break;
                }
            }
        }

        // ---- Utility ----

        private bool AnyPlayerNear(double x, double y, double z, int dim, float range)
        {
            if (range <= 0) range = 120f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return false;

            double rangeSq = range * (double)range;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe?.Pos == null) continue;
                if (pe.Pos.Dimension != dim) continue;

                double dx = pe.Pos.X - x;
                double dy = pe.Pos.Y - y;
                double dz = pe.Pos.Z - z;

                if (dx * dx + dy * dy + dz * dz <= rangeSq) return true;
            }

            return false;
        }

        /// <summary>
        /// Get all known trial keys from loaded configs.
        /// </summary>
        public string[] GetKnownTrialKeys()
        {
            var keys = new List<string>(allConfigs.Count);
            foreach (var cfg in allConfigs)
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.trialKey))
                    keys.Add(cfg.trialKey);
            }
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys.ToArray();
        }

        /// <summary>
        /// Get the tracked entity for a trial boss (public for tracker action).
        /// </summary>
        public Vintagestory.API.Common.Entities.Entity GetTrackedEntity(string trialKey)
        {
            return entityTracker?.GetTrackedEntity(trialKey);
        }

        /// <summary>
        /// Get the anchor position for a trial boss.
        /// </summary>
        public Vec3d GetAnchorPosition(string trialKey)
        {
            var entry = GetOrCreateEntry(trialKey);
            if (entry?.anchorPoints == null || entry.anchorPoints.Count == 0) return null;

            var anchor = entry.anchorPoints[0];
            return new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
        }
    }
}
