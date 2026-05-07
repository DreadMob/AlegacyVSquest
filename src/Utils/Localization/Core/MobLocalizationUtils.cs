using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public static class MobLocalizationUtils
    {
        private static Dictionary<string, string> displayNameMap;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> localizationCache = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void LoadFromAssets(ICoreAPI api)
        {
            if (api == null) return;

            localizationCache.Clear();
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void TryMergeFromDomain(string domain)
            {
                if (string.IsNullOrWhiteSpace(domain)) return;
                try
                {
                    var dicts = api.Assets.GetMany<Dictionary<string, string>>(api.Logger, "config/mobdisplaynames", domain);
                    foreach (var pair in dicts)
                    {
                        if (pair.Value == null) continue;
                        foreach (var kvp in pair.Value)
                        {
                            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Warning("[MobLocalizationUtils] Failed to load mob display names from domain {0}: {1}", domain, ex.Message);
                }
            }

            // Support legacy/alternate asset domains used in this workspace.
            TryMergeFromDomain("vsquest");
            TryMergeFromDomain("alegacyvsquest");

            foreach (var mod in api.ModLoader.Mods)
            {
                TryMergeFromDomain(mod?.Info?.ModID);
            }

            displayNameMap = merged.Count > 0 ? merged : null;
        }

        public static string GetMobDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            if (localizationCache.TryGetValue(code, out var cachedName))
            {
                return cachedName;
            }

            string result = GetMobDisplayNameInternal(code);
            localizationCache.TryAdd(code, result);
            return result;
        }

        private static string GetMobDisplayNameInternal(string code)
        {
            string domain = null;
            int colonIndex = code.IndexOf(':');
            if (colonIndex > 0 && colonIndex < code.Length - 1)
            {
                domain = code.Substring(0, colonIndex);
            }

            string normalized = NormalizeMobCode(code);


            // 1) mobdisplaynames.json overrides (exact only)
            if (displayNameMap != null && displayNameMap.TryGetValue(normalized, out var exactOverride) && !string.IsNullOrWhiteSpace(exactOverride))
            {
                return exactOverride;
            }

            // 2) Game localization (exact, then progressively shorter prefixes)
            foreach (string candidate in GetFallbackCandidates(normalized))
            {
                string t;
                t = TryLangGet(domain, $"item-creature-{candidate}");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet(null, $"game:item-creature-{candidate}");
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // 3) mobdisplaynames.json overrides (prefixes/base), only if game localization has no match
            if (displayNameMap != null)
            {
                foreach (string candidate in GetFallbackCandidates(normalized))
                {
                    if (displayNameMap.TryGetValue(candidate, out var mappedName) && !string.IsNullOrWhiteSpace(mappedName))
                    {
                        return mappedName;
                    }
                }
            }

            // 4) Wildcard translations as a last resort
            foreach (string candidate in GetFallbackCandidates(normalized))
            {
                string t;
                t = TryLangGet(domain, $"item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet(null, $"game:item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            string fallback = MapCode(normalized);
            return fallback;
        }


        public static string GetMobDisplayName(Entity entity)
        {
            if (entity == null) return null;

            try
            {
                string domain = entity.Code?.Domain;
                string codePath = entity.Code?.Path;
                if (string.IsNullOrWhiteSpace(codePath)) return entity.Code?.ToShortString();

                string variantSuffix = "";
                try
                {
                    if (entity.Properties?.Variant != null && entity.Properties.Variant.Count > 0)
                    {
                        foreach (var val in entity.Properties.Variant.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(val)) variantSuffix += "-" + val;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Variant access can fail on malformed entity definitions - log once per entity type
                    System.Diagnostics.Debug.WriteLine($"[MobLocalizationUtils] Failed to read entity variant: {ex.Message}");
                }

                if (!string.IsNullOrWhiteSpace(variantSuffix))
                {
                    string withVariant = codePath + variantSuffix;

                    if (displayNameMap != null && displayNameMap.TryGetValue(withVariant, out var exactOverride) && !string.IsNullOrWhiteSpace(exactOverride))
                    {
                        return exactOverride;
                    }

                    foreach (string candidate in GetFallbackCandidates(withVariant))
                    {
                        string t;
                        t = TryLangGet(domain, $"item-creature-{candidate}");
                        if (!string.IsNullOrWhiteSpace(t)) return t;
                        t = TryLangGet(null, $"game:item-creature-{candidate}");
                        if (!string.IsNullOrWhiteSpace(t)) return t;
                    }
                }

                return GetMobDisplayName(entity.Code?.ToShortString());
            }
            catch
            {
                return entity.Code?.ToShortString();
            }
        }

        private static IEnumerable<string> GetFallbackCandidates(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) yield break;

            string cur = code;
            while (!string.IsNullOrWhiteSpace(cur))
            {
                yield return cur;
                int idx = cur.LastIndexOf('-');
                if (idx <= 0) break;
                cur = cur.Substring(0, idx);
            }
        }

        private static string TryLangGet(string domain, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            if (!string.IsNullOrWhiteSpace(domain) && key.IndexOf(':') < 0)
            {
                string fullKey = domain + ":" + key;
                string t = LocalizationUtils.GetSafe(fullKey);
                if (!string.IsNullOrWhiteSpace(t) && !string.Equals(t, fullKey, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            string t2 = LocalizationUtils.GetSafe(key);
            if (!string.IsNullOrWhiteSpace(t2) && !string.Equals(t2, key, StringComparison.OrdinalIgnoreCase))
            {
                return t2;
            }

            return null;
        }

        public static string NormalizeMobCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            int colonIndex = code.IndexOf(':');
            if (colonIndex > 0 && colonIndex < code.Length - 1)
            {
                code = code.Substring(colonIndex + 1);
            }

            return code;
        }

        public static bool MobCodeMatches(string targetCode, string killedCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode) || string.IsNullOrWhiteSpace(killedCode)) return false;

            targetCode = NormalizeMobCode(targetCode);
            killedCode = NormalizeMobCode(killedCode);

            if (string.Equals(targetCode, killedCode, StringComparison.OrdinalIgnoreCase)) return true;
            if (killedCode.StartsWith(targetCode + "-", StringComparison.OrdinalIgnoreCase)) return true;

            // Handle eurasian-adult-male / eurasian-adult-female for wolves
            if (targetCode.Contains("wolf") && killedCode.Contains("wolf"))
            {
                if (killedCode.Contains("eurasian") && targetCode.Contains("eurasian")) return true;
                if (!killedCode.Contains("eurasian") && !targetCode.Contains("eurasian")) return true;
            }

            return false;
        }

        private static string MapCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            code = NormalizeMobCode(code);

            int dashIndex = code.IndexOf('-');
            if (dashIndex > 0)
            {
                return code.Substring(0, dashIndex);
            }

            return code;
        }
    }
}
