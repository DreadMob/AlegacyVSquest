using System;
using System.Collections.Generic;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class TemporalStormObjective : ActionObjectiveBase
    {
        public static string HaveKey(string questId, string objectiveId) => $"vsquest:tempstorm:{questId}:{objectiveId}:have";
        private static string LastActiveKey(string questId, string objectiveId) => $"vsquest:tempstorm:{questId}:{objectiveId}:lastactive";

        // Args format:
        // [0] questId
        // [1] objectiveId (must match actionObjective.objectiveId)
        // [2] needStorms
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out int need)) return false;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out int need)) return new List<int>(new int[] { 0, 0 });

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int>(new int[] { 0, need });

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            if (have > need) have = need;
            if (need < 0) need = 0;

            return new List<int>(new int[] { have, need });
        }

        public void OnTick(IServerPlayer player, ActiveQuest activeQuest, ActionWithArgs objectiveDef, ICoreServerAPI sapi)
        {
            if (player?.Entity?.WatchedAttributes == null) return;
            if (objectiveDef?.args == null) return;

            if (!TryParseArgs(objectiveDef.args, out string questId, out string objectiveId, out int need)) return;
            if (!string.Equals(questId, activeQuest?.questId, StringComparison.OrdinalIgnoreCase)) return;
            if (!string.Equals(objectiveId, objectiveDef.objectiveId, StringComparison.OrdinalIgnoreCase)) return;

            var wa = player.Entity.WatchedAttributes;

            bool activeNow = IsTemporalStormActive(sapi);
            bool lastActive = wa.GetBool(LastActiveKey(questId, objectiveId), false);

            // Count storm survived when it transitions active -> inactive
            if (lastActive && !activeNow)
            {
                string haveKey = HaveKey(questId, objectiveId);
                int have = wa.GetInt(haveKey, 0);
                if (have < need)
                {
                    have++;
                    wa.SetInt(haveKey, have);
                    wa.MarkPathDirty(haveKey);

                    if (have >= need)
                    {
                        QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, objectiveDef, objectiveDef.objectiveId, true);
                    }
                }
            }

            if (lastActive != activeNow)
            {
                string key = LastActiveKey(questId, objectiveId);
                wa.SetBool(key, activeNow);
                wa.MarkPathDirty(key);
            }
        }

        private static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out int need)
        {
            questId = null;
            objectiveId = null;
            need = 0;

            if (args == null || args.Length < 3) return false;

            questId = args[0];
            objectiveId = args[1];
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[2], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }

        private static bool IsTemporalStormActive(ICoreServerAPI sapi)
        {
            if (sapi == null) return false;

            // Direct lookup (available in Vintage Story source):
            // Vintagestory.GameContent.SystemTemporalStability keeps storm state in StormData.nowStormActive.
            var sys = sapi.ModLoader.GetModSystem<SystemTemporalStability>();
            if (sys == null) return false;

            // Prefer the boolean runtime flag; StormStrength is derived from it anyway.
            return sys.StormData?.nowStormActive ?? (sys.StormStrength > 0f);
        }
    }
}
