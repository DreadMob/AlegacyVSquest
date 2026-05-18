using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class HollowTrialsChatCommandSubRegistry : IChatCommandSubRegistry
    {
        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            avq.BeginSubCommand("trials")
                .WithDescription("Hollow Trials admin commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("status")
                    .WithDescription("Shows all anchors in the world, their bosses, tiers, cooldowns, and modifier.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(args => HandleStatus(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("skip")
                    .WithDescription("Kills all alive trial bosses, force-rotates to next set, reassigns all anchors.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(args => HandleSkip(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("respawn")
                    .WithDescription("Force-respawns a trial boss at a specific anchor by its friendly ID (e.g. anchor1).")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("anchorId"))
                    .HandleWith(args => HandleRespawn(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Reloads trial configs from disk (hot-reload without restart).")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(args => HandleReload(args, sapi))
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("Clears all trial anchor registrations and despawns all trial bosses. Anchors stay as blocks but lose their state.")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(args => HandleClear(args, sapi))
                .EndSubCommand()
            .EndSubCommand();
        }

        private TextCommandResult HandleStatus(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Испытания Пустоты — Статус ===");

            // Rotation info
            double hoursLeft = system.GetHoursUntilRotation();
            double daysLeft = hoursLeft / 24.0;
            sb.AppendLine($"Следующая ротация: {hoursLeft:0.0}ч (~{daysLeft:0.1} дн.)");

            // Active modifier
            var modType = (TrialModifierType)system.GetActiveModifier();
            if (modType != TrialModifierType.None)
            {
                string modName = LocalizationUtils.GetSafe(TrialWeeklyModifierUtils.GetNameKey(modType));
                sb.AppendLine($"Модификатор: {modName}");
            }
            else
            {
                sb.AppendLine("Модификатор: Нет");
            }

            sb.AppendLine();

            // All anchors in the world — scan all state entries for anchor points
            // Also scan all loaded BlockEntityVoidRiftAnchor instances
            var allAnchors = CollectAllAnchors(sapi, system);

            if (allAnchors.Count == 0)
            {
                sb.AppendLine("Якоря: не найдены");
            }
            else
            {
                sb.AppendLine($"Якоря ({allAnchors.Count}):");
                double nowHours = sapi.World.Calendar.TotalHours;

                foreach (var info in allAnchors)
                {
                    string bossName = !string.IsNullOrWhiteSpace(info.TrialKey) ? info.TrialKey : "не назначен";
                    string tierStr = info.Tier > 0 ? $"T{info.Tier}" : "T?";
                    string posStr = $"{info.X},{info.Y},{info.Z}";

                    // Boss status
                    string statusStr;
                    if (string.IsNullOrWhiteSpace(info.TrialKey))
                    {
                        statusStr = "ожидает назначения";
                    }
                    else if (info.SpawnedEntityId > 0)
                    {
                        // Check if entity is alive
                        var bossEntity = sapi.World.GetEntityById(info.SpawnedEntityId);
                        if (bossEntity != null && bossEntity.Alive)
                        {
                            var healthTree = bossEntity.WatchedAttributes.GetTreeAttribute("health");
                            float curHp = healthTree?.GetFloat("currenthealth", 0) ?? 0;
                            float maxHp = healthTree?.GetFloat("maxhealth", 0) ?? 0;
                            statusStr = $"жив ({curHp:0}/{maxHp:0} HP)";
                        }
                        else
                        {
                            statusStr = "готов к призыву";
                        }
                    }
                    else if (info.DeadUntilHours > nowHours)
                    {
                        double cdLeft = info.DeadUntilHours - nowHours;
                        statusStr = $"кд ({cdLeft:0.1}ч)";
                    }
                    else
                    {
                        statusStr = "готов к призыву";
                    }

                    sb.AppendLine($"  [{tierStr}] {bossName} @ {posStr} dim={info.Dim} — {statusStr}");
                }
            }

            // Known configs
            var knownKeys = system.GetKnownTrialKeys();
            sb.AppendLine();
            sb.AppendLine($"Конфиги боссов ({knownKeys.Length}): {string.Join(", ", knownKeys)}");

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult HandleSkip(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            // Kill/despawn ALL alive trial bosses before rotation
            int killed = DespawnAllTrialBosses(sapi, system);

            // Force rotation
            if (!system.ForceRotation(out var newKeys))
                return TextCommandResult.Error("Rotation failed (no configs?).");

            // Reassign all anchors (clears stored boss + cooldown on each BE)
            system.ReassignAllAnchors();

            var modType = (TrialModifierType)system.GetActiveModifier();
            string modName = modType != TrialModifierType.None
                ? LocalizationUtils.GetSafe(TrialWeeklyModifierUtils.GetNameKey(modType))
                : "Нет";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Ротация выполнена. Убито боссов: {killed}.");
            sb.AppendLine($"Модификатор: {modName}");
            sb.AppendLine("Все якоря сброшены — новый босс назначится при следующем ПКМ.");

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult HandleRespawn(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            string anchorId = (string)args.Parsers[0].GetValue();
            if (string.IsNullOrWhiteSpace(anchorId))
                return TextCommandResult.Error("Usage: /avq trials respawn <anchorId>");

            if (!system.ForceRespawnByAnchor(anchorId, out string error, out string spawnedInfo))
                return TextCommandResult.Error(error);

            return TextCommandResult.Success($"Force-respawned: {spawnedInfo}");
        }

        private TextCommandResult HandleReload(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            int count = system.ReloadConfigs();
            return TextCommandResult.Success($"Reloaded {count} trial configs. Anchor points cleared (will re-register).");
        }

        private TextCommandResult HandleClear(TextCommandCallingArgs args, ICoreServerAPI sapi)
        {
            var system = sapi?.ModLoader?.GetModSystem<HollowTrialSystem>();
            if (system == null)
                return TextCommandResult.Error("HollowTrialSystem not available.");

            // 1. Despawn all alive trial bosses
            int despawned = DespawnAllTrialBosses(sapi, system);

            // 2. Clear all anchor block entities (reset their stored state)
            int anchorsCleared = ClearAllAnchorBlockEntities(sapi, system);

            // 3. Clear all state entries (anchor registrations, cooldowns, etc.)
            system.ClearAllEntries();

            return TextCommandResult.Success(
                $"Очищено. Деспавнено боссов: {despawned}. Якорей сброшено: {anchorsCleared}. " +
                $"Все регистрации якорей удалены. Якоря остались как блоки — при ПКМ назначат нового босса.");
        }

        /// <summary>
        /// Clear stored state on all placed VoidRiftAnchor block entities in the world.
        /// Resets trial key, cooldown, and spawned entity reference.
        /// </summary>
        private static int ClearAllAnchorBlockEntities(ICoreServerAPI sapi, HollowTrialSystem system)
        {
            int count = 0;
            var allEntries = system.GetAllEntries();
            if (allEntries == null) return 0;

            // Collect all anchor positions first to avoid collection modification during iteration
            var positions = new List<(int x, int y, int z, int dim)>();
            foreach (var entry in allEntries)
            {
                if (entry?.anchorPoints == null) continue;
                foreach (var ap in entry.anchorPoints)
                {
                    positions.Add((ap.x, ap.y, ap.z, ap.dim));
                }
            }

            // Now iterate the snapshot — safe from collection modification
            foreach (var (x, y, z, dim) in positions)
            {
                try
                {
                    var pos = new BlockPos(x, y, z, dim);
                    var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                    if (be != null)
                    {
                        be.OnBossKilled(0); // Reset cooldown
                        count++;
                    }
                }
                catch { }
            }

            return count;
        }

        /// <summary>
        /// Despawn all alive trial bosses in the world. Returns count of despawned.
        /// Checks both entityTracker and all loaded entities with quest target behavior.
        /// </summary>
        private static int DespawnAllTrialBosses(ICoreServerAPI sapi, HollowTrialSystem system)
        {
            int count = 0;
            var despawnedIds = new HashSet<long>();
            var knownKeys = system.GetKnownTrialKeys();

            // First pass: despawn via entityTracker (fast path)
            foreach (var key in knownKeys)
            {
                var entity = system.GetTrackedEntity(key);
                if (entity != null && entity.Alive && !despawnedIds.Contains(entity.EntityId))
                {
                    try
                    {
                        Vec3d deathPos = entity.Pos.XYZ.Clone();
                        ParticleUtils.SpawnShadowExplosion(sapi, deathPos, 3f, 2);
                        sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        despawnedIds.Add(entity.EntityId);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[HollowTrials] Skip: failed to despawn '{0}': {1}", key, ex.Message);
                    }
                }

                // Reset cooldown so new bosses can be summoned immediately
                var entry = system.GetOrCreateEntry(key);
                if (entry != null)
                {
                    entry.deadUntilTotalHours = 0;
                }
            }

            // Second pass: scan all loaded entities for any trial bosses missed by tracker
            // (handles case where multiple anchors spawned same boss type)
            try
            {
                var knownKeySet = new HashSet<string>(knownKeys, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in sapi.World.LoadedEntities)
                {
                    var entity = kvp.Value;
                    if (entity == null || !entity.Alive) continue;
                    if (despawnedIds.Contains(entity.EntityId)) continue;

                    var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) continue;
                    if (!knownKeySet.Contains(qt.TargetId)) continue;

                    try
                    {
                        Vec3d deathPos = entity.Pos.XYZ.Clone();
                        ParticleUtils.SpawnShadowExplosion(sapi, deathPos, 3f, 2);
                        sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        despawnedIds.Add(entity.EntityId);
                        count++;
                    }
                    catch { }
                }
            }
            catch { }

            return count;
        }

        /// <summary>
        /// Reset per-anchor cooldowns on all placed VoidRiftAnchor block entities.
        /// </summary>
        private static void ResetAllAnchorCooldowns(ICoreServerAPI sapi, HollowTrialSystem system)
        {
            var knownKeys = system.GetKnownTrialKeys();
            foreach (var key in knownKeys)
            {
                var entry = system.GetOrCreateEntry(key);
                if (entry?.anchorPoints == null) continue;

                foreach (var ap in entry.anchorPoints)
                {
                    try
                    {
                        var pos = new BlockPos(ap.x, ap.y, ap.z, ap.dim);
                        var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                        if (be != null)
                        {
                            be.OnBossKilled(0); // Reset cooldown to 0 (immediately available)
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Collect info about all anchors in the world from both state entries and loaded block entities.
        /// </summary>
        private static List<AnchorInfo> CollectAllAnchors(ICoreServerAPI sapi, HollowTrialSystem system)
        {
            var result = new List<AnchorInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From ALL state entries (registered anchors — includes __unassigned__ placeholder)
            var allEntries = system.GetAllEntries();
            if (allEntries != null)
            {
                foreach (var entry in allEntries)
                {
                    if (entry?.anchorPoints == null) continue;

                    foreach (var ap in entry.anchorPoints)
                    {
                        string uid = $"{ap.x}:{ap.y}:{ap.z}:{ap.dim}";
                        if (seen.Contains(uid)) continue;
                        seen.Add(uid);

                        string displayKey = entry.trialKey;
                        if (string.Equals(displayKey, "__unassigned__", StringComparison.OrdinalIgnoreCase))
                            displayKey = null;

                        result.Add(new AnchorInfo
                        {
                            X = ap.x,
                            Y = ap.y,
                            Z = ap.z,
                            Dim = ap.dim,
                            Tier = 0,
                            TrialKey = displayKey,
                            FriendlyId = ap.friendlyId
                        });
                    }
                }
            }

            // Enrich with data from block entities at known positions
            foreach (var info in result)
            {
                try
                {
                    var pos = new BlockPos(info.X, info.Y, info.Z, info.Dim);
                    var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                    if (be != null)
                    {
                        info.Tier = be.Tier;
                        info.SpawnedEntityId = be.GetSpawnedEntityId();
                        info.DeadUntilHours = be.DeadUntilTotalHours;
                        // Always prefer the BE's current trial key
                        string beKey = be.GetActiveTrialKey();
                        if (!string.IsNullOrWhiteSpace(beKey))
                        {
                            info.TrialKey = beKey;
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private class AnchorInfo
        {
            public int X, Y, Z, Dim;
            public int Tier;
            public string TrialKey;
            public string FriendlyId;
            public long SpawnedEntityId;
            public double DeadUntilHours;
        }
    }
}
