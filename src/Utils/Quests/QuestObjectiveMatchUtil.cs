using System;
using System.Linq;

namespace VsQuest
{
    public static class QuestObjectiveMatchUtil
    {
        private static readonly SimpleLRUCache<string, ParsedPos> parsedPosCacheByString = 
            new SimpleLRUCache<string, ParsedPos>(5000, StringComparer.Ordinal);

        private class ParsedPos
        {
            public bool Ok;
            public int X;
            public int Y;
            public int Z;
        }

        public static bool TryParsePosCached(string coordString, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            try
            {
                if (parsedPosCacheByString.TryGetValue(coordString, out var cached) && cached != null)
                {
                    if (!cached.Ok) return false;
                    x = cached.X;
                    y = cached.Y;
                    z = cached.Z;
                    return true;
                }

                int comma1 = coordString.IndexOf(',');
                if (comma1 < 0) { parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false }); return false; }
                int comma2 = coordString.IndexOf(',', comma1 + 1);
                if (comma2 < 0) { parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false }); return false; }

                if (!int.TryParse(coordString.Substring(0, comma1), out x)
                    || !int.TryParse(coordString.Substring(comma1 + 1, comma2 - comma1 - 1), out y)
                    || !int.TryParse(coordString.Substring(comma2 + 1), out z))
                {
                    parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false });
                    return false;
                }

                parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = true, X = x, Y = y, Z = z });
                return true;
            }
            catch (Exception)
            {
                // Failed to parse position - return false to indicate failure
                return false;
            }
        }

        public static void ClearPosCache()
        {
            parsedPosCacheByString.Clear();
        }


        public static bool InteractObjectiveMatches(Objective objective, string code, int[] position)
        {
            if (objective == null) return false;

            if (objective.positions != null && objective.positions.Count > 0)
            {
                var posStr = string.Join(",", position);
                if (!objective.positions.Any(p => p == posStr))
                {
                    return false;
                }
            }

            if (objective.validCodes == null || objective.validCodes.Count == 0)
            {
                return true;
            }

            foreach (var codeCandidate in objective.validCodes)
            {
                if (LocalizationUtils.MobCodeMatches(codeCandidate, code))
                {
                    return true;
                }

                if (codeCandidate.EndsWith("*") && code.StartsWith(codeCandidate.Remove(codeCandidate.Length - 1)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
