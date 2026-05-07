using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class InLandObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            bool result = TryGetClaimName(byPlayer, out string name) && NameMatches(args, name);
            
            // Debug logging
            if (byPlayer?.Entity?.Api is Vintagestory.API.Server.ICoreServerAPI sapi)
            {
                if (args != null && args.Length > 0)
                {
                    string expected = args[0];
                    sapi.Logger.Debug($"[InLandObjective] Player {byPlayer.PlayerName} checking region. Expected: '{expected}', Found: '{name}', Result: {result}");
                }
            }
            
            return result;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool ok = IsCompletable(byPlayer, args);
            return ok
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        private static bool NameMatches(string[] args, string claimName)
        {
            if (args == null || args.Length < 1) return false;
            if (string.IsNullOrWhiteSpace(claimName)) return false;

            string expected = args[0];
            if (string.IsNullOrWhiteSpace(expected)) return false;

            string exp = expected.Trim();
            string actual = claimName.Trim();

            if (args.Length >= 2 && string.Equals(args[1]?.Trim(), "contains", StringComparison.OrdinalIgnoreCase))
            {
                return actual.IndexOf(exp, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return string.Equals(exp, actual, StringComparison.OrdinalIgnoreCase);
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
