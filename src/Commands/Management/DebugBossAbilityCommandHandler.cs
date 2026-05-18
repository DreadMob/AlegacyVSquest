using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Debug command for testing boss abilities one by one.
    /// Enables a debug mode where only the selected ability fires on the boss.
    /// Use /avq debugba to toggle, /avq debugba next/prev to cycle abilities.
    /// </summary>
    public class DebugBossAbilityCommandHandler
    {
        private const string AttrDebugActive = "alegacyvsquest:debugba:active";
        private const string AttrDebugAbility = "alegacyvsquest:debugba:ability";
        private const float BossSearchRange = 60f;

        private readonly ICoreServerAPI sapi;

        public DebugBossAbilityCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        /// <summary>
        /// Toggle debug mode on/off for the nearest boss.
        /// </summary>
        public TextCommandResult Toggle(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            bool currentlyActive = boss.WatchedAttributes.GetBool(AttrDebugActive, false);

            if (currentlyActive)
            {
                // Disable debug mode
                boss.WatchedAttributes.SetBool(AttrDebugActive, false);
                boss.WatchedAttributes.RemoveAttribute(AttrDebugAbility);
                boss.WatchedAttributes.MarkPathDirty(AttrDebugActive);
                return TextCommandResult.Success($"[DebugBA] Disabled on '{boss.Code.Path}'. All abilities active.");
            }
            else
            {
                // Enable debug mode — set to first ability
                var abilities = GetBossAbilities(boss);
                if (abilities.Count == 0) return TextCommandResult.Error($"Boss '{boss.Code.Path}' has no BossAbilityBase behaviors.");

                string firstAbility = abilities[0].PropertyName();
                boss.WatchedAttributes.SetBool(AttrDebugActive, true);
                boss.WatchedAttributes.SetString(AttrDebugAbility, firstAbility);
                boss.WatchedAttributes.MarkPathDirty(AttrDebugActive);
                boss.WatchedAttributes.MarkPathDirty(AttrDebugAbility);

                return TextCommandResult.Success($"[DebugBA] Enabled on '{boss.Code.Path}'. Active: {firstAbility} (1/{abilities.Count})");
            }
        }

        /// <summary>
        /// Switch to the next ability in the list.
        /// </summary>
        public TextCommandResult Next(TextCommandCallingArgs args)
        {
            return CycleAbility(args, +1);
        }

        /// <summary>
        /// Switch to the previous ability in the list.
        /// </summary>
        public TextCommandResult Prev(TextCommandCallingArgs args)
        {
            return CycleAbility(args, -1);
        }

        /// <summary>
        /// Set a specific ability by name.
        /// </summary>
        public TextCommandResult Set(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            string abilityName = args.Parsers?[0]?.GetValue()?.ToString();
            if (string.IsNullOrWhiteSpace(abilityName)) return TextCommandResult.Error("Usage: /avq debugba set <abilityName>");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            if (!boss.WatchedAttributes.GetBool(AttrDebugActive, false))
                return TextCommandResult.Error("[DebugBA] Debug mode is not active. Use '/avq debugba' to enable.");

            var abilities = GetBossAbilities(boss);
            var match = abilities.FirstOrDefault(a =>
                string.Equals(a.PropertyName(), abilityName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                var available = string.Join(", ", abilities.Select(a => a.PropertyName()));
                return TextCommandResult.Error($"Ability '{abilityName}' not found. Available: {available}");
            }

            boss.WatchedAttributes.SetString(AttrDebugAbility, match.PropertyName());
            boss.WatchedAttributes.MarkPathDirty(AttrDebugAbility);

            int idx = abilities.IndexOf(match);
            string info = GetAbilityInfo(match);
            return TextCommandResult.Success($"[DebugBA] Active: {match.PropertyName()} ({idx + 1}/{abilities.Count}) {info}");
        }

        /// <summary>
        /// Force-fire the currently selected ability immediately.
        /// </summary>
        public TextCommandResult Fire(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            if (!boss.WatchedAttributes.GetBool(AttrDebugActive, false))
                return TextCommandResult.Error("[DebugBA] Debug mode is not active. Use '/avq debugba' to enable.");

            string selectedAbility = boss.WatchedAttributes.GetString(AttrDebugAbility, null);
            if (string.IsNullOrWhiteSpace(selectedAbility))
                return TextCommandResult.Error("[DebugBA] No ability selected.");

            var abilities = GetBossAbilities(boss);
            var ability = abilities.FirstOrDefault(a =>
                string.Equals(a.PropertyName(), selectedAbility, StringComparison.OrdinalIgnoreCase));

            if (ability == null)
                return TextCommandResult.Error($"[DebugBA] Ability '{selectedAbility}' not found on boss.");

            // Force activate with the calling player as target
            bool success = ability.ForceActivate(player.Entity);
            if (success)
            {
                return TextCommandResult.Success($"[DebugBA] Fired '{selectedAbility}' → target: {player.PlayerName}");
            }
            else
            {
                return TextCommandResult.Error($"[DebugBA] Failed to fire '{selectedAbility}'. No stages or target unavailable.");
            }
        }

        /// <summary>
        /// List all abilities on the nearest boss.
        /// </summary>
        public TextCommandResult List(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            var abilities = GetBossAbilities(boss);
            if (abilities.Count == 0) return TextCommandResult.Success($"Boss '{boss.Code.Path}' has no BossAbilityBase behaviors.");

            bool debugActive = boss.WatchedAttributes.GetBool(AttrDebugActive, false);
            string selected = boss.WatchedAttributes.GetString(AttrDebugAbility, null);

            var lines = new List<string>();
            lines.Add($"[DebugBA] Boss: {boss.Code.Path} | Debug: {(debugActive ? "ON" : "OFF")}");
            lines.Add("---");

            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                string marker = (debugActive && string.Equals(a.PropertyName(), selected, StringComparison.OrdinalIgnoreCase))
                    ? " ◄"
                    : "";
                string info = GetAbilityInfo(a);
                lines.Add($"  {i + 1}. {a.PropertyName()}{marker} {info}");
            }

            // Also list non-BossAbilityBase behaviors (enrage, etc.)
            var enrage = boss.GetBehavior<EntityBehaviorBossEnrage>();
            if (enrage != null)
            {
                lines.Add($"  [special] bossenrage (IsEnraged: {enrage.IsEnraged})");
            }

            return TextCommandResult.Success(string.Join("\n", lines));
        }

        /// <summary>
        /// Reset all cooldowns on the nearest boss.
        /// </summary>
        public TextCommandResult ResetCooldowns(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            // Clear all cooldown keys from WatchedAttributes
            var keysToRemove = new List<string>();
            foreach (var key in boss.WatchedAttributes.Keys)
            {
                if (key.Contains("alegacyvsquest") && (key.Contains("lastStartMs") || key.Contains("lastMs")))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                boss.WatchedAttributes.RemoveAttribute(key);
            }

            if (keysToRemove.Count > 0)
            {
                boss.WatchedAttributes.MarkPathDirty("alegacyvsquest");
            }

            return TextCommandResult.Success($"[DebugBA] Reset {keysToRemove.Count} cooldowns on '{boss.Code.Path}'.");
        }

        /// <summary>
        /// Force-trigger enrage on the nearest boss.
        /// </summary>
        public TextCommandResult Enrage(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            var enrage = boss.GetBehavior<EntityBehaviorBossEnrage>();
            if (enrage == null) return TextCommandResult.Error($"Boss '{boss.Code.Path}' has no bossenrage behavior.");

            if (enrage.IsEnraged)
            {
                enrage.Reset();
                return TextCommandResult.Success($"[DebugBA] Enrage RESET on '{boss.Code.Path}'.");
            }
            else
            {
                // Force enrage by setting first damage time far in the past
                boss.WatchedAttributes.SetDouble("alegacyvsquest:enrage:firstDamageHours",
                    sapi.World.Calendar.TotalHours - 999);
                boss.WatchedAttributes.MarkPathDirty("alegacyvsquest:enrage:firstDamageHours");
                return TextCommandResult.Success($"[DebugBA] Enrage FORCED on '{boss.Code.Path}'. Will activate next tick.");
            }
        }

        // ========================================
        // HELPERS
        // ========================================

        private TextCommandResult CycleAbility(TextCommandCallingArgs args, int direction)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            var boss = FindNearestBoss(player);
            if (boss == null) return TextCommandResult.Error("No living boss found within range.");

            if (!boss.WatchedAttributes.GetBool(AttrDebugActive, false))
                return TextCommandResult.Error("[DebugBA] Debug mode is not active. Use '/avq debugba' to enable.");

            var abilities = GetBossAbilities(boss);
            if (abilities.Count == 0) return TextCommandResult.Error("No abilities found.");

            string current = boss.WatchedAttributes.GetString(AttrDebugAbility, null);
            int currentIdx = abilities.FindIndex(a =>
                string.Equals(a.PropertyName(), current, StringComparison.OrdinalIgnoreCase));

            if (currentIdx < 0) currentIdx = 0;

            int newIdx = (currentIdx + direction + abilities.Count) % abilities.Count;
            var newAbility = abilities[newIdx];

            boss.WatchedAttributes.SetString(AttrDebugAbility, newAbility.PropertyName());
            boss.WatchedAttributes.MarkPathDirty(AttrDebugAbility);

            string info = GetAbilityInfo(newAbility);
            return TextCommandResult.Success($"[DebugBA] Active: {newAbility.PropertyName()} ({newIdx + 1}/{abilities.Count}) {info}");
        }

        private Entity FindNearestBoss(IServerPlayer player)
        {
            if (player?.Entity == null) return null;

            var playerPos = player.Entity.Pos.XYZ;
            var entities = sapi.World.GetEntitiesAround(playerPos, BossSearchRange, BossSearchRange,
                e => e.Alive && e.Code != null && HasAnyBossAbility(e));

            if (entities == null || entities.Length == 0) return null;

            Entity nearest = null;
            double nearestDist = double.MaxValue;

            foreach (var e in entities)
            {
                double dist = e.Pos.SquareDistanceTo(playerPos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = e;
                }
            }

            return nearest;
        }

        private bool HasAnyBossAbility(Entity entity)
        {
            if (entity?.Properties?.Server?.Behaviors == null) return false;

            // Check if entity has any BossAbilityBase behavior
            foreach (var beh in entity.SidedProperties?.Behaviors ?? Enumerable.Empty<EntityBehavior>())
            {
                if (beh is BossAbilityBase) return true;
                if (beh is EntityBehaviorBossEnrage) return true;
            }

            return false;
        }

        private List<BossAbilityBase> GetBossAbilities(Entity boss)
        {
            var result = new List<BossAbilityBase>();
            if (boss?.SidedProperties?.Behaviors == null) return result;

            foreach (var beh in boss.SidedProperties.Behaviors)
            {
                if (beh is BossAbilityBase ability)
                {
                    result.Add(ability);
                }
            }

            return result;
        }

        private string GetAbilityInfo(BossAbilityBase ability)
        {
            // Can't access protected members directly, but we can use ForceActivate success as indicator
            // Just return empty for now — the stage count info is shown via list
            return "";
        }
    }
}
