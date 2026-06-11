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
        private static readonly Dictionary<string, string> _bookIdsByName = BuildBookIdsByName();
        private static Dictionary<string, string>? _localizedBookNamesById;
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

            var bookId = GetBookId(englishBookName);
            if (string.IsNullOrWhiteSpace(bookId))
            {
                return false;
            }

            return _localizedBookNamesById!.TryGetValue(bookId, out localizedBookName);
        }

        internal static void WarmUp() => EnsureCache();

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _localizedBookNamesById = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }

        private static void EnsureCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_localizedBookNamesById != null && _cacheLang == currentLang)
            {
                return;
            }

            lock (_cacheLock)
            {
                if (_localizedBookNamesById != null && _cacheLang == currentLang)
                {
                    return;
                }

                _localizedBookNamesById = BuildLocalizedNamesById();
                _cacheLang = currentLang;
            }
        }

        private static Dictionary<string, string> BuildBookIdsByName()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in PowerBooks.BookNamesToIds)
            {
                AddBookId(cache, pair.Key, pair.Value);
            }

            return cache;
        }

        private static Dictionary<string, string> BuildLocalizedNamesById()
        {
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var objectId in PowerBooks.BookIdsToNames.Keys)
            {
                var itemData = ItemRegistry.GetDataOrErrorItem($"(O){objectId}");
                var localizedName = itemData?.DisplayName?.Trim();
                if (string.IsNullOrWhiteSpace(localizedName))
                {
                    continue;
                }

                cache[objectId] = localizedName;
            }

            return cache;
        }

        private static void AddBookId(Dictionary<string, string> cache, string englishName, string objectId)
        {
            if (string.IsNullOrWhiteSpace(englishName) || string.IsNullOrWhiteSpace(objectId))
            {
                return;
            }

            cache.TryAdd(englishName.Trim(), objectId);
            cache.TryAdd(NormalizeBookName(englishName), objectId);
            cache.TryAdd(objectId.Trim(), objectId);
            cache.TryAdd(NormalizeBookName(objectId), objectId);
        }

        private static string? GetBookId(string englishBookName)
        {
            var trimmed = englishBookName.Trim();
            if (_bookIdsByName.TryGetValue(trimmed, out var bookId))
            {
                return bookId;
            }

            var normalized = NormalizeBookName(englishBookName);
            return _bookIdsByName.TryGetValue(normalized, out bookId) ? bookId : null;
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
