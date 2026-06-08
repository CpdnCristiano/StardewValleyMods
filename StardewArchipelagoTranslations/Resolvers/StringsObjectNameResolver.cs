using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class StringsObjectNameResolver : IItemResolver
    {
        private static readonly Regex _quantityPattern = new(@"^([\d,]+)\s+(.+)$");
        private static readonly object _cacheLock = new();
        private static Dictionary<string, string>? _objectNamesCache;
        private static LocalizedContentManager.LanguageCode _cacheLang =
            (LocalizedContentManager.LanguageCode)(-1);

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (string.IsNullOrWhiteSpace(englishName))
            {
                return false;
            }

            var quantityMatch = _quantityPattern.Match(englishName.Trim());
            if (quantityMatch.Success)
            {
                var amount = quantityMatch.Groups[1].Value;
                var itemName = quantityMatch.Groups[2].Value.Trim();
                var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);

                if (!localizedItem.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    localizedName = $"{amount} {localizedItem}";
                    return true;
                }
            }

            EnsureObjectNamesCache();

            if (TryGetCachedName(englishName, out localizedName))
            {
                return true;
            }

            foreach (var singularCandidate in GetEnglishSingularCandidates(englishName.Trim()))
            {
                if (TryGetCachedName(singularCandidate, out localizedName))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void WarmUp() => EnsureObjectNamesCache();

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _objectNamesCache = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }

        private static bool TryGetCachedName(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (_objectNamesCache == null)
            {
                return false;
            }

            if (_objectNamesCache.TryGetValue(englishName.Trim(), out localizedName))
            {
                return true;
            }

            return _objectNamesCache.TryGetValue(NormalizeName(englishName), out localizedName);
        }

        private static void EnsureObjectNamesCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_objectNamesCache != null && _cacheLang == currentLang)
            {
                return;
            }

            lock (_cacheLock)
            {
                if (_objectNamesCache != null && _cacheLang == currentLang)
                {
                    return;
                }

                _objectNamesCache = BuildCache();
                _cacheLang = currentLang;
            }
        }

        private static Dictionary<string, string> BuildCache()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var engManager = new ContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );

                var engStrings = engManager.Load<Dictionary<string, string>>("Strings\\Objects");
                var locStrings = Game1.content.Load<Dictionary<string, string>>("Strings\\Objects");

                foreach (var pair in engStrings)
                {
                    if (!pair.Key.EndsWith("_Name", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!locStrings.TryGetValue(pair.Key, out var localizedValue))
                    {
                        continue;
                    }

                    var englishName = pair.Value?.Trim();
                    var localizedName = localizedValue?.Trim();

                    if (
                        string.IsNullOrWhiteSpace(englishName)
                        || string.IsNullOrWhiteSpace(localizedName)
                    )
                    {
                        continue;
                    }

                    cache.TryAdd(englishName, localizedName);
                    cache.TryAdd(NormalizeName(englishName), localizedName);

                    var keyName = pair.Key[..^"_Name".Length];
                    cache.TryAdd(keyName, localizedName);
                    cache.TryAdd(NormalizeName(keyName), localizedName);
                }

                ModEntry.Instance.Monitor.Log(
                    $"[StringsObjectNameResolver] Loaded {cache.Count} object name entries.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[StringsObjectNameResolver] Error building cache: {ex}",
                    LogLevel.Warn
                );
            }

            return cache;
        }

        private static string NormalizeName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);

            foreach (var character in text.Normalize(NormalizationForm.FormKC))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<string> GetEnglishSingularCandidates(string englishName)
        {
            if (englishName.Length < 3 || !englishName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var lower = englishName.ToLowerInvariant();

            if (lower.EndsWith("ies", StringComparison.Ordinal) && englishName.Length > 3)
            {
                yield return englishName[..^3] + "y";
            }

            if (lower.EndsWith("ves", StringComparison.Ordinal) && englishName.Length > 3)
            {
                yield return englishName[..^3] + "f";
                yield return englishName[..^3] + "fe";
            }

            if (
                lower.EndsWith("ches", StringComparison.Ordinal)
                || lower.EndsWith("shes", StringComparison.Ordinal)
                || lower.EndsWith("xes", StringComparison.Ordinal)
                || lower.EndsWith("zes", StringComparison.Ordinal)
                || lower.EndsWith("ses", StringComparison.Ordinal)
                || lower.EndsWith("oes", StringComparison.Ordinal)
            )
            {
                yield return englishName[..^2];
            }

            if (!lower.EndsWith("ss", StringComparison.Ordinal))
            {
                yield return englishName[..^1];
            }
        }

    }
}
