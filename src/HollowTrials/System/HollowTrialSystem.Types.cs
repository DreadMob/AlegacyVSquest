using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class HollowTrialWorldState
        {
            /// <summary>
            /// Currently active trial keys (one per tier).
            /// </summary>
            public List<string> activeTrialKeys = new();

            /// <summary>
            /// Total hours when next rotation should occur.
            /// </summary>
            public double nextRotationTotalHours;

            /// <summary>
            /// Rotation index per tier (tracks which boss is next in the sorted list).
            /// Key = tier (1, 2, 3), Value = current index in sorted list.
            /// </summary>
            public Dictionary<int, int> rotationIndexPerTier = new();

            /// <summary>
            /// Per-boss state entries (respawn timers, anchor data).
            /// </summary>
            public List<HollowTrialStateEntry> entries = new();

            /// <summary>
            /// Currently active weekly modifier (persisted across restarts).
            /// </summary>
            public int activeModifier;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class HollowTrialStateEntry
        {
            /// <summary>
            /// The trial key this entry belongs to.
            /// </summary>
            public string trialKey;

            /// <summary>
            /// Total hours until boss can respawn (0 = alive or ready to spawn).
            /// </summary>
            public double deadUntilTotalHours;

            /// <summary>
            /// Last time a soft reset occurred (anti-spam).
            /// </summary>
            public double lastSoftResetAtTotalHours;

            /// <summary>
            /// Registered anchor points for this trial boss.
            /// </summary>
            public List<HollowTrialAnchorPoint> anchorPoints = new();
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class HollowTrialAnchorPoint
        {
            public string anchorId;
            public string friendlyId;
            public int x;
            public int y;
            public int z;
            public int dim;
            public float yOffset;
        }
    }
}
