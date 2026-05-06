using ProtoBuf;
using System.Collections.Generic;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntConfig
        {
            public string bossKey;
            public string questId;
            public double rotationDays;

            public double relocateIntervalHours;
            public double respawnInGameHours;
            public double noRelocateAfterDamageMinutes;

            public float activationRange;
            public float playerLockRange;

            public string GetBossEntityCode()
            {
                return DeriveEntityCodeFromBossKey(bossKey);
            }

            private static string DeriveEntityCodeFromBossKey(string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return null;

                var parts = key.Split(':');
                if (parts.Length == 2) return key;
                if (parts.Length < 2) return null;

                var domain = parts[0];
                var bossName = parts[parts.Length - 1];
                return domain + ":" + bossName;
            }

            public bool IsValid()
            {
                if (string.IsNullOrWhiteSpace(bossKey)) return false;
                if (string.IsNullOrWhiteSpace(GetBossEntityCode())) return false;
                return true;
            }

            public double GetRelocateIntervalHours() => relocateIntervalHours > 0 ? relocateIntervalHours : 72;
            public double GetRespawnHours() => respawnInGameHours > 0 ? respawnInGameHours : 24;
            public double GetNoRelocateAfterDamageHours() => noRelocateAfterDamageMinutes > 0 ? (noRelocateAfterDamageMinutes / 60.0) : (10.0 / 60.0);
            public float GetActivationRange(AlegacyVsQuestConfig.BossHuntCoreConfig coreConfig = null) => 
                activationRange > 0 ? activationRange : (coreConfig?.DefaultActivationRange ?? 160f);
            public float GetPlayerLockRange() => playerLockRange > 0 ? playerLockRange : 40f;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntWorldState
        {
            public List<BossHuntStateEntry> entries = new();
            public string activeBossKey;
            public double nextBossRotationTotalHours;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntStateEntry
        {
            public string bossKey;
            public int currentPointIndex;
            public double nextRelocateAtTotalHours;
            public double deadUntilTotalHours;

            public double lastSoftResetAtTotalHours;

            public List<BossHuntAnchorPoint> anchorPoints;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntAnchorPoint
        {
            public string anchorId;
            public int order;
            public int x;
            public int y;
            public int z;
            public int dim;
            public float leashRange;
            public float outOfCombatLeashRange;
            public float yOffset;
        }
    }
}
