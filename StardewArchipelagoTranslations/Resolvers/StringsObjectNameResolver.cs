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
        private static Dictionary<string, string>? _englishObjectKeysByName;
        private static Dictionary<string, string>? _localizedObjectNamesByKey;
        private static LocalizedContentManager.LanguageCode _localizedCacheLang =
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
                _englishObjectKeysByName = null;
                _localizedObjectNamesByKey = null;
                _localizedCacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }

        private static bool TryGetCachedName(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (_englishObjectKeysByName == null || _localizedObjectNamesByKey == null)
            {
                return false;
            }

            if (_englishObjectKeysByName.TryGetValue(englishName.Trim(), out var objectKey))
            {
                return _localizedObjectNamesByKey.TryGetValue(objectKey, out localizedName);
            }

            return _englishObjectKeysByName.TryGetValue(NormalizeName(englishName), out objectKey)
                && _localizedObjectNamesByKey.TryGetValue(objectKey, out localizedName);
        }

        private static void EnsureObjectNamesCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (
                _englishObjectKeysByName != null
                && _localizedObjectNamesByKey != null
                && _localizedCacheLang == currentLang
            )
            {
                return;
            }

            lock (_cacheLock)
            {
                if (
                    _englishObjectKeysByName != null
                    && _localizedObjectNamesByKey != null
                    && _localizedCacheLang == currentLang
                )
                {
                    return;
                }

                _englishObjectKeysByName ??= BuildEnglishObjectKeysByName();
                _localizedObjectNamesByKey = BuildLocalizedNamesByKey();
                _localizedCacheLang = currentLang;
            }
        }

        private static Dictionary<string, string> BuildEnglishObjectKeysByName()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var engManager = new ContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );

                var locStrings = Game1.content.Load<Dictionary<string, string>>("Strings\\Objects");
                var engStrings = engManager.Load<Dictionary<string, string>>("Strings\\Objects");
                foreach (var pair in engStrings)
                {
                    if (!pair.Key.EndsWith("_Name", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var englishName = pair.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(englishName))
                    {
                        continue;
                    }

                    cache.TryAdd(englishName, pair.Key);
                    cache.TryAdd(NormalizeName(englishName), pair.Key);

                    var keyName = pair.Key[..^"_Name".Length];
                    cache.TryAdd(keyName, pair.Key);
                    cache.TryAdd(NormalizeName(keyName), pair.Key);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[StringsObjectNameResolver] Error building localized cache: {ex}",
                    LogLevel.Warn
                );
            }

            return cache;
        }

        private static Dictionary<string, string> BuildLocalizedNamesByKey()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var locStrings = Game1.content.Load<Dictionary<string, string>>("Strings\\Objects");
                foreach (var pair in locStrings)
                {
                    if (!pair.Key.EndsWith("_Name", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var localizedName = pair.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(localizedName))
                    {
                        continue;
                    }

                    cache[pair.Key] = localizedName;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[StringsObjectNameResolver] Error building localized cache: {ex}",
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
