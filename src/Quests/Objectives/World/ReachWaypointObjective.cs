using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class ReachWaypointObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            return IsInRange(byPlayer, args);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            return IsInRange(byPlayer, args)
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        private static bool IsInRange(IPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity == null) return false;
            if (args == null || args.Length < 1) return false;

            if (!TryParsePos(args[0], out int x, out int y, out int z)) return false;

            double radius = 2;
            if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                double.TryParse(args[1], out radius);
            }
            if (radius < 0) radius = 0;

            Vec3d p = byPlayer.Entity.Pos.XYZ;
            double dx = p.X - x;
            double dy = p.Y - y;
            double dz = p.Z - z;

            return (dx * dx + dy * dy + dz * dz) <= radius * radius;
        }

        private static bool TryParsePos(string pos, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(pos)) return false;

            var parts = pos.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length != 3) return false;

            return int.TryParse(parts[0], out x)
                && int.TryParse(parts[1], out y)
                && int.TryParse(parts[2], out z);
        }
    }
}
