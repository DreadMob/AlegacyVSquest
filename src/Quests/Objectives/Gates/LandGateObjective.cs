using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class LandGateObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string claimName, out _, out _, out _)) return true;

            return TryGetClaimName(byPlayer, out string currentName)
                && string.Equals(claimName.Trim(), currentName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool ok = IsCompletable(byPlayer, args);
            return ok
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        public static bool TryParseArgs(string[] args, out string claimName, out string objectiveId, out string prefix, out bool hidePrefix)
        {
            claimName = null;
            objectiveId = null;
            prefix = null;
            hidePrefix = false;

            if (args == null || args.Length < 1) return false;

            claimName = args[0];
            if (string.IsNullOrWhiteSpace(claimName)) return false;

            if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                objectiveId = args[1];
            }

            if (args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]))
            {
                prefix = args[2];
            }

            if (args.Length >= 4 && !string.IsNullOrWhiteSpace(args[3]))
            {
                var v = args[3].Trim();
                hidePrefix = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
            }

            return true;
        }

        private static bool TryGetClaimName(IPlayer byPlayer, out string claimName)
        {
            claimName = null;
            if (byPlayer.Entity?.Pos == null) return false;

            BlockPos pos = byPlayer.Entity.Pos.AsBlockPos;
            var claimsApi = byPlayer?.Entity?.World?.Claims;
            if (claimsApi == null) return false;

            var claims = claimsApi.Get(pos);
            if (claims == null || claims.Length == 0) return false;

            for (int i = 0; i < claims.Length; i++)
            {
                var desc = claims[i]?.Description;
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    claimName = desc;
                    return true;
                }

                var ownerName = claims[i]?.LastKnownOwnerName;
                if (!string.IsNullOrWhiteSpace(ownerName))
                {
                    claimName = ownerName;
                    return true;
                }
            }

            return false;
        }
    }
}
