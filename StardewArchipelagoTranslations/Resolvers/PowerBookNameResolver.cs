using System;
using System.Collections.Generic;
using System.Text;
using StardewArchipelago.Constants.Vanilla;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class PowerBookNameResolver
    {
        private static readonly object _cacheLock = new();
        private static Dictionary<string, string>? _bookNamesCache;
        private static LocalizedContentManager.LanguageCode _cacheLang =
            (LocalizedContentManager.LanguageCode)(-1);

        internal static bool TryResolve(string englishBookName, out string? localizedBookName)
        {
            localizedBookName = null;

            if (string.IsNullOrWhiteSpace(englishBookName))
            {
                return false;
            }

            EnsureCache();

            return _bookNamesCache!.TryGetValue(englishBookName.Trim(), out localizedBookName)
                || _bookNamesCache.TryGetValue(NormalizeBookName(englishBookName), out localizedBookName);
        }

        internal static void WarmUp() => EnsureCache();

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _bookNamesCache = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }

        private static void EnsureCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_bookNamesCache != null && _cacheLang == currentLang)
            {
                return;
            }

            lock (_cacheLock)
            {
                if (_bookNamesCache != null && _cacheLang == currentLang)
                {
                    return;
                }

                _bookNamesCache = BuildCache();
                _cacheLang = currentLang;
            }
        }

        private static Dictionary<string, string> BuildCache()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in PowerBooks.BookNamesToIds)
            {
                AddBook(cache, pair.Key, pair.Value);
            }

            foreach (var pair in PowerBooks.BookIdsToNames)
            {
                AddBook(cache, pair.Value, pair.Key);
            }

            ModEntry.Instance.Monitor.Log(
                $"[PowerBookNameResolver] Loaded {cache.Count} power book entries.",
                LogLevel.Trace
            );

            return cache;
        }

        private static void AddBook(Dictionary<string, string> cache, string englishName, string objectId)
        {
            if (string.IsNullOrWhiteSpace(englishName) || string.IsNullOrWhiteSpace(objectId))
            {
                return;
            }

            var itemData = ItemRegistry.GetDataOrErrorItem($"(O){objectId}");
            var localizedName = itemData?.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(localizedName))
            {
                return;
            }

            cache.TryAdd(englishName.Trim(), localizedName);
            cache.TryAdd(NormalizeBookName(englishName), localizedName);
            cache.TryAdd(objectId.Trim(), localizedName);
            cache.TryAdd(NormalizeBookName(objectId), localizedName);
        }

        private static string NormalizeBookName(string text)
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
    }
}
