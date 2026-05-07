using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BossHuntWorldStateDto
    {
        public List<BossHuntStateEntryDto> entries = new();
        public string activeBossKey;
        public double nextBossRotationTotalHours;

        public static BossHuntWorldStateDto FromDomain(BossHuntSystem.BossHuntWorldState state)
        {
            if (state == null) return null;

            var dto = new BossHuntWorldStateDto
            {
                activeBossKey = state.activeBossKey,
                nextBossRotationTotalHours = state.nextBossRotationTotalHours,
                entries = new List<BossHuntStateEntryDto>(state.entries?.Count ?? 0)
            };

            if (state.entries != null)
            {
                for (int i = 0; i < state.entries.Count; i++)
                {
                    dto.entries.Add(BossHuntStateEntryDto.FromDomain(state.entries[i]));
                }
            }

            return dto;
        }

        public BossHuntSystem.BossHuntWorldState ToDomain()
        {
            var state = new BossHuntSystem.BossHuntWorldState
            {
                activeBossKey = activeBossKey,
                nextBossRotationTotalHours = nextBossRotationTotalHours,
                entries = new List<BossHuntSystem.BossHuntStateEntry>(entries?.Count ?? 0)
            };

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    state.entries.Add(entries[i]?.ToDomain());
                }
            }

            return state;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BossHuntStateEntryDto
    {
        public string bossKey;
        public int currentPointIndex;
        public double nextRelocateAtTotalHours;
        public double deadUntilTotalHours;
        public double lastSoftResetAtTotalHours;
        public List<BossHuntAnchorPointDto> anchorPoints;

        public static BossHuntStateEntryDto FromDomain(BossHuntSystem.BossHuntStateEntry state)
        {
            if (state == null) return null;

            var dto = new BossHuntStateEntryDto
            {
                bossKey = state.bossKey,
                currentPointIndex = state.currentPointIndex,
                nextRelocateAtTotalHours = state.nextRelocateAtTotalHours,
                deadUntilTotalHours = state.deadUntilTotalHours,
                lastSoftResetAtTotalHours = state.lastSoftResetAtTotalHours,
                anchorPoints = new List<BossHuntAnchorPointDto>(state.anchorPoints?.Count ?? 0)
            };

            if (state.anchorPoints != null)
            {
                for (int i = 0; i < state.anchorPoints.Count; i++)
                {
                    dto.anchorPoints.Add(BossHuntAnchorPointDto.FromDomain(state.anchorPoints[i]));
                }
            }

            return dto;
        }

        public BossHuntSystem.BossHuntStateEntry ToDomain()
        {
            var state = new BossHuntSystem.BossHuntStateEntry
            {
                bossKey = bossKey,
                currentPointIndex = currentPointIndex,
                nextRelocateAtTotalHours = nextRelocateAtTotalHours,
                deadUntilTotalHours = deadUntilTotalHours,
                lastSoftResetAtTotalHours = lastSoftResetAtTotalHours,
                anchorPoints = new List<BossHuntSystem.BossHuntAnchorPoint>(anchorPoints?.Count ?? 0)
            };

            if (anchorPoints != null)
            {
                for (int i = 0; i < anchorPoints.Count; i++)
                {
                    state.anchorPoints.Add(anchorPoints[i]?.ToDomain());
                }
            }

            return state;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BossHuntAnchorPointDto
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

        public static BossHuntAnchorPointDto FromDomain(BossHuntSystem.BossHuntAnchorPoint p)
        {
            if (p == null) return null;

            return new BossHuntAnchorPointDto
            {
                anchorId = p.anchorId,
                order = p.order,
                x = p.x,
                y = p.y,
                z = p.z,
                dim = p.dim,
                leashRange = p.leashRange,
                outOfCombatLeashRange = p.outOfCombatLeashRange,
                yOffset = p.yOffset
            };
        }

        public BossHuntSystem.BossHuntAnchorPoint ToDomain()
        {
            return new BossHuntSystem.BossHuntAnchorPoint
            {
                anchorId = anchorId,
                order = order,
                x = x,
                y = y,
                z = z,
                dim = dim,
                leashRange = leashRange,
                outOfCombatLeashRange = outOfCombatLeashRange,
                yOffset = yOffset
            };
        }
    }
}
