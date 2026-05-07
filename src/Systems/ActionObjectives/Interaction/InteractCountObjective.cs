using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractCountObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args == null || args.Length == 0) return false;

            var coordArgs = GetCoordArgs(args);
            if (coordArgs.Length == 0)
            {
                try
                {
                    byPlayer?.Entity?.Api?.Logger?.Warning($"[alegacyvsquest] interactcount objective has 0 coordinates (args='{string.Join("|", args ?? Array.Empty<string>())}'). Treating as completable to avoid stuck quest.");
                }
                catch
                {
                    // Swallow - this is a fail-safe logging attempt
                }

                // Fail-open: an invalid objective should not permanently block quest turn-in.
                return true;
            }

            try
            {
                return QuestInteractAtUtil.CountCompleted(byPlayer, coordArgs) >= coordArgs.Length;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            var coordArgs = GetCoordArgs(args);
            int need = coordArgs.Length;
            if (need == 0) return new List<int>(new int[] { 0, 1 });

            int have = QuestInteractAtUtil.CountCompleted(byPlayer, coordArgs);
            if (have > need) have = need;

            return new List<int>(new int[] { have, need });
        }

        private static string[] GetCoordArgs(string[] args)
        {
            if (args == null || args.Length == 0) return Array.Empty<string>();

            // First arg is the count (optional), remaining args are coordinate strings
            // If first arg is a number, skip it and parse the rest
            // Otherwise, treat all args as coordinate strings
            
            int startIndex = 0;
            if (args.Length > 0 && int.TryParse(args[0], out _))
            {
                // First arg is a count, skip it
                startIndex = 1;
            }

            var coordList = new List<string>();
            for (int i = startIndex; i < args.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(args[i])) continue;
                
                // Split by | to support multiple coordinates in one arg
                var coords = args[i].Split('|');
                foreach (var coord in coords)
                {
                    if (!string.IsNullOrWhiteSpace(coord))
                    {
                        coordList.Add(coord.Trim());
                    }
                }
            }

            return coordList.ToArray();
        }

        
    }
}
