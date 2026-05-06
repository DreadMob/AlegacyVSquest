using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public static class LocalizationUtils
    {
        private static ICoreAPI lastApi;
        private static Dictionary<string, string> displayNameMap;
        private static readonly HashSet<string> nestedLocalizationDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Custom storage for nested language files: domain:lang -> key -> value
        private static Dictionary<string, Dictionary<string, string>> nestedLangCache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> loadedLangDomains = new HashSet<string>();

        public static void SetNestedLocalizationDomains(IEnumerable<string> domains)
        {
            nestedLocalizationDomains.Clear();
            if (domains == null) return;

            foreach (var domain in domains)
            {
                if (string.IsNullOrWhiteSpace(domain)) continue;
                nestedLocalizationDomains.Add(domain.Trim());
            }
        }

        /// <summary>
        /// Loads language files from nested folder structure: lang/{languageCode}/*.json
        /// This supports organizing translations into subdirectories.
        /// </summary>
        public static void LoadNestedLanguageFiles(ICoreAPI api)
        {
            if (api == null) return;
            lastApi = api;

            string currentLang = Lang.CurrentLocale ?? "en";
            string altLang = currentLang.Contains("-") ? currentLang.Split('-')[0] : currentLang;

            foreach (var mod in api.ModLoader.Mods)
            {
                string domain = mod?.Info?.ModID;
                if (string.IsNullOrWhiteSpace(domain)) continue;

                string cacheKey = $"{domain}:{currentLang}";
                if (loadedLangDomains.Contains(cacheKey)) continue;

                try
                {
                    var langAssets = new List<IAsset>();

                    // Primary probe
                    var direct = api.Assets.GetMany($"lang/{currentLang}/", domain, loadAsset: true);
                    if (direct != null) langAssets.AddRange(direct);

                    // Recursive probe (covers nested folders reliably)
                    var locations = api.Assets.GetLocations($"lang/{currentLang}/", domain);
                    if (locations != null)
                    {
                        foreach (var loc in locations)
                        {
                            var asset = api.Assets.TryGet(loc, loadAsset: true);
                            if (asset != null) langAssets.Add(asset);
                        }
                    }

                    // Fallback to short locale (ru from ru-RU)
                    if ((langAssets.Count == 0) && altLang != currentLang)
                    {
                        direct = api.Assets.GetMany($"lang/{altLang}/", domain, loadAsset: true);
                        if (direct != null) langAssets.AddRange(direct);

                        locations = api.Assets.GetLocations($"lang/{altLang}/", domain);
                        if (locations != null)
                        {
                            foreach (var loc in locations)
                            {
                                var asset = api.Assets.TryGet(loc, loadAsset: true);
                                if (asset != null) langAssets.Add(asset);
                            }
                        }
                    }

                    if (langAssets == null || !langAssets.Any()) continue;

                    // De-dup by asset location (GetMany + GetLocations may overlap)
                    var uniqueAssets = langAssets
                        .Where(a => a?.Location != null)
                        .GroupBy(a => a.Location.ToShortString(), StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var asset in uniqueAssets)
                    {
                        if (asset == null) continue;
                        if (!asset.Location.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            // Read as text and parse to handle BOM correctly
                            string jsonText = asset.ToText();
                            if (string.IsNullOrWhiteSpace(jsonText)) continue;

                            // Remove UTF-8 BOM if present (EF BB BF)
                            if (jsonText.Length > 0 && jsonText[0] == '\uFEFF')
                            {
                                jsonText = jsonText.Substring(1);
                            }

                            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
                            if (dict == null) continue;

                            foreach (var kvp in dict)
                            {
                                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null) continue;
                                // Skip comment entries
                                if (kvp.Key.StartsWith("_")) continue;
                                merged[kvp.Key] = kvp.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            api.Logger.Warning($"[vsquest] Failed to parse language file {asset.Location}: {ex.Message}");
                        }
                    }

                    if (merged.Count > 0)
                    {
                        // Store in custom cache with both prefixed and non-prefixed keys
                        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in merged)
                        {
                            // Store with domain prefix (albase:key)
                            cache[$"{domain}:{kvp.Key}"] = kvp.Value;
                            // Also store without prefix for compatibility
                            cache[kvp.Key] = kvp.Value;
                        }
                        nestedLangCache[cacheKey] = cache;
                        loadedLangDomains.Add(cacheKey);
                    }
                }
                catch (Exception e)
                {
                    api.Logger.Warning($"[vsquest] Could not load nested language files from '{domain}': {e.Message}");
                }
            }
        }

        /// <summary>
        /// Gets a translation from nested language files if available.
        /// </summary>
        public static string GetFromNested(string langKey)
        {
            if (string.IsNullOrEmpty(langKey)) return null;

            // Locale may not be finalized when early startup loaders run.
            // Retry loading with the current locale on demand.
            if (lastApi != null)
            {
                try
                {
                    LoadNestedLanguageFiles(lastApi);
                }
                catch
                {
                }
            }

            // Try all loaded language caches
            foreach (var cache in nestedLangCache.Values)
            {
                if (cache.TryGetValue(langKey, out var value))
                    return value;
            }

            return null;
        }

        public static string GetDialogueText(string langKey)
        {
            if (string.IsNullOrEmpty(langKey)) return "";
            var result = GetFromNested(langKey);
            return result ?? langKey;
        }

        public static void LoadFromAssets(ICoreAPI api)
        {
            if (api == null) return;

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
                catch (Exception e)
                {
                    api.Logger.Warning($"[vsquest] Could not load mobdisplaynames from domain '{domain}': {e.Message}");
                }
            }

            foreach (var mod in api.ModLoader.Mods)
            {
                TryMergeFromDomain(mod?.Info?.ModID);
            }

            displayNameMap = merged.Count > 0 ? merged : null;
        }

        public static string GetSafe(string langKey)
        {
            if (string.IsNullOrEmpty(langKey)) return "";

            // First check nested language files
            var nested = GetFromNested(langKey);
            if (nested != null) return nested;

            try
            {
                var result = Lang.Get(langKey);
                return result;
            }
            catch (FormatException)
            {
                // String contains format placeholders but no args provided
                // Return the raw translation without formatting
                return Lang.Get(langKey, false);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetSafe(string langKey, params object[] args)
        {
            if (string.IsNullOrEmpty(langKey)) return "";

            // First check nested language files
            var nested = GetFromNested(langKey);
            if (nested != null)
            {
                // Apply string formatting if args provided
                if (args != null && args.Length > 0)
                {
                    try
                    {
                        return string.Format(nested, args);
                    }
                    catch
                    {
                        return nested;
                    }
                }
                return nested;
            }

            try
            {
                return Lang.Get(langKey, args);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetFallback(string primaryLangKey, string fallbackLangKey)
        {
            if (!string.IsNullOrEmpty(primaryLangKey))
            {
                return GetSafe(primaryLangKey);
            }

            return GetSafe(fallbackLangKey);
        }

        public static string GetSafeParams(string langKey, params object[] args)
        {
            return GetSafe(langKey, args);
        }

        /// <summary>
        /// Strict split:
        /// - configured domains -> nested custom localization only
        /// - all other domains/keys -> native Lang.Get only
        /// </summary>
        public static string GetSafeStrictDomains(string langKey, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(langKey)) return langKey ?? "";

            string domain = null;
            int colon = langKey.IndexOf(':');
            if (colon > 0)
            {
                domain = langKey.Substring(0, colon);
            }

            bool isOurDomain = !string.IsNullOrWhiteSpace(domain) && nestedLocalizationDomains.Contains(domain);

            if (isOurDomain)
            {
                var nested = GetFromNested(langKey);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    if (args != null && args.Length > 0)
                    {
                        try
                        {
                            return string.Format(nested, args);
                        }
                        catch
                        {
                            return nested;
                        }
                    }

                    return nested;
                }

                return langKey;
            }

            try
            {
                return Lang.Get(langKey, args);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetSafeMatching(string langKey, string fallback)
        {
            if (string.IsNullOrWhiteSpace(langKey)) return fallback ?? "";
            string value = GetSafe(langKey);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return fallback ?? "";
            }
            return value;
        }

        public static string GetSafeMatchingParams(string langKey, params object[] args)
        {
            string value = GetSafe(langKey, args);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return langKey ?? "";
            }
            return value;
        }

        /// <summary>
        /// Strict split:
        /// - our domains -> nested custom localization only
        /// - all other domains/keys -> native Lang.GetMatching only
        /// </summary>
        public static string GetSafeMatchingStrictDomains(string langKey, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(langKey)) return langKey ?? "";

            string domain = null;
            int colon = langKey.IndexOf(':');
            if (colon > 0)
            {
                domain = langKey.Substring(0, colon);
            }

            bool isOurDomain = !string.IsNullOrWhiteSpace(domain) && nestedLocalizationDomains.Contains(domain);

            if (isOurDomain)
            {
                var nested = GetFromNested(langKey);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    if (args != null && args.Length > 0)
                    {
                        try
                        {
                            return string.Format(nested, args);
                        }
                        catch
                        {
                            return nested;
                        }
                    }

                    return nested;
                }

                return langKey;
            }

            try
            {
                return Lang.GetMatching(langKey, args);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetMobDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

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
                t = TryLangGet($"item-creature-{candidate}");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet($"game:item-creature-{candidate}");
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
                t = TryLangGet($"item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet($"game:item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            string fallback = MapCode(normalized);
            return fallback;
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

        private static string TryLangGet(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                string t = Lang.Get(key);
                if (!string.IsNullOrWhiteSpace(t) && !string.Equals(t, key, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            catch
            {
            }

            return null;
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
