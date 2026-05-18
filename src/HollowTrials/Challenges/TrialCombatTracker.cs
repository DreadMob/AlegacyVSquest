using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Tracks combat metrics for a single trial boss fight.
    /// Attached per-boss, records data for the player with most impact.
    /// </summary>
    public class TrialCombatTracker
    {
        /// <summary>
        /// Total hours when first damage was dealt (fight start).
        /// </summary>
        public double FightStartTotalHours { get; private set; }

        /// <summary>
        /// Total hours when boss died (fight end).
        /// </summary>
        public double FightEndTotalHours { get; private set; }

        /// <summary>
        /// Per-player damage dealt to this boss.
        /// </summary>
        public Dictionary<string, float> DamageByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player death count during this fight.
        /// </summary>
        public Dictionary<string, int> DeathsByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player: whether saturation increased during the fight (for "nofood" challenge).
        /// </summary>
        public Dictionary<string, bool> SaturationGainedByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player: whether a potion/healing item was used during the fight (for "nopotions" challenge).
        /// </summary>
        public Dictionary<string, bool> PotionUsedByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player: max HP recorded during the fight (for "lowdiet" challenge).
        /// </summary>
        public Dictionary<string, float> MaxHpByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player: saturation value recorded at fight start (first damage by that player).
        /// </summary>
        public Dictionary<string, float> InitialSaturationByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player max armor tier worn during fight.
        /// </summary>
        public Dictionary<string, int> MaxArmorTierByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-player set of ability codes that dealt damage to them.
        /// </summary>
        public Dictionary<string, HashSet<string>> AbilityDamageByPlayer { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// UID of the first player to deal damage.
        /// </summary>
        public string FirstAttackerUid { get; private set; }

        /// <summary>
        /// The tier at which this boss was spawned (1, 2, or 3).
        /// </summary>
        public int SpawnTier { get; private set; }

        private bool started;
        private bool finished;

        public bool IsStarted => started;
        public bool IsFinished => finished;

        /// <summary>
        /// Set the spawn tier for this fight.
        /// </summary>
        public void SetSpawnTier(int tier)
        {
            SpawnTier = tier;
        }

        /// <summary>
        /// Record damage dealt by a player.
        /// </summary>
        public void RecordDamage(string playerUid, float damage, double totalHours)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || damage <= 0) return;

            if (!started)
            {
                started = true;
                FightStartTotalHours = totalHours;
                FirstAttackerUid = playerUid;
            }

            if (!DamageByPlayer.ContainsKey(playerUid))
                DamageByPlayer[playerUid] = 0;

            DamageByPlayer[playerUid] += damage;
        }

        /// <summary>
        /// Record a player death during the fight.
        /// </summary>
        public void RecordPlayerDeath(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            if (!started) return;

            if (!DeathsByPlayer.ContainsKey(playerUid))
                DeathsByPlayer[playerUid] = 0;

            DeathsByPlayer[playerUid]++;
        }

        /// <summary>
        /// Record the initial saturation for a player when they first engage the boss.
        /// </summary>
        public void RecordInitialSaturation(string playerUid, float saturation)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            if (!started) return;

            if (!InitialSaturationByPlayer.ContainsKey(playerUid))
            {
                InitialSaturationByPlayer[playerUid] = saturation;
            }
        }

        /// <summary>
        /// Record that a player's saturation increased during the fight (ate food).
        /// </summary>
        public void RecordSaturationGain(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            if (!started) return;

            SaturationGainedByPlayer[playerUid] = true;
        }

        /// <summary>
        /// Record the max armor tier a player has worn during the fight.
        /// </summary>
        public void RecordArmorTier(string playerUid, int tier)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            if (!started) return;

            if (!MaxArmorTierByPlayer.ContainsKey(playerUid))
                MaxArmorTierByPlayer[playerUid] = 0;

            if (tier > MaxArmorTierByPlayer[playerUid])
                MaxArmorTierByPlayer[playerUid] = tier;
        }

        /// <summary>
        /// Record that a specific boss ability dealt damage to a player.
        /// </summary>
        public void RecordAbilityDamage(string playerUid, string abilityCode)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(abilityCode)) return;
            if (!started) return;

            if (!AbilityDamageByPlayer.ContainsKey(playerUid))
                AbilityDamageByPlayer[playerUid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AbilityDamageByPlayer[playerUid].Add(abilityCode);
        }

        /// <summary>
        /// Mark the fight as finished (boss died).
        /// </summary>
        public void MarkFinished(double totalHours)
        {
            finished = true;
            FightEndTotalHours = totalHours;
        }

        /// <summary>
        /// Get the player UID with the most total damage (kill credit).
        /// If tied, returns the first attacker.
        /// If top player is offline, returns next online player.
        /// </summary>
        public string GetKillCreditPlayer(ICoreServerAPI sapi)
        {
            if (DamageByPlayer.Count == 0) return null;

            string topUid = null;
            float topDamage = 0;

            foreach (var kvp in DamageByPlayer)
            {
                if (kvp.Value > topDamage)
                {
                    topDamage = kvp.Value;
                    topUid = kvp.Key;
                }
                else if (kvp.Value == topDamage && string.Equals(kvp.Key, FirstAttackerUid, StringComparison.OrdinalIgnoreCase))
                {
                    topUid = kvp.Key;
                }
            }

            // Check if top player is online
            if (topUid != null && sapi != null)
            {
                var player = sapi.World.PlayerByUid(topUid);
                if (player is IServerPlayer sp && sp.ConnectionState == EnumClientState.Playing)
                    return topUid;

                // Top player offline — find next best online player
                var sorted = new List<KeyValuePair<string, float>>(DamageByPlayer);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

                foreach (var kvp in sorted)
                {
                    var p = sapi.World.PlayerByUid(kvp.Key);
                    if (p is IServerPlayer onlineSp && onlineSp.ConnectionState == EnumClientState.Playing)
                        return kvp.Key;
                }
            }

            return topUid;
        }

        /// <summary>
        /// Get fight duration in minutes.
        /// </summary>
        public double GetFightDurationMinutes()
        {
            if (!started || !finished) return 0;
            return (FightEndTotalHours - FightStartTotalHours) * 60.0;
        }

        /// <summary>
        /// Reset all tracking data (for soft reset).
        /// </summary>
        public void Reset()
        {
            started = false;
            finished = false;
            SpawnTier = 0;
            FightStartTotalHours = 0;
            FightEndTotalHours = 0;
            FirstAttackerUid = null;
            DamageByPlayer.Clear();
            DeathsByPlayer.Clear();
            SaturationGainedByPlayer.Clear();
            PotionUsedByPlayer.Clear();
            MaxHpByPlayer.Clear();
            InitialSaturationByPlayer.Clear();
            MaxArmorTierByPlayer.Clear();
            AbilityDamageByPlayer.Clear();
        }
    }
}
