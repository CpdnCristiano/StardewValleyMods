using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ReadBookResolver : IItemResolver
    {
        private static Dictionary<string, string>? _readBooksCache;
        private static LocalizedContentManager.LanguageCode _cacheLang =
            (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _cacheLock = new();

        public bool TryResolve(string englishItemName, out string localizedItemName)
        {
            localizedItemName = englishItemName;

            const string readPrefix = "Read ";

            if (!englishItemName.StartsWith(readPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string englishBookName = englishItemName.Substring(readPrefix.Length).Trim();

            if (string.IsNullOrWhiteSpace(englishBookName))
            {
                return false;
            }

            EnsureBookCache();

            string? localizedBookTitle = null;

            if (!_readBooksCache!.TryGetValue(englishBookName, out localizedBookTitle))
            {
                string normalizedSearch = NormalizeBookName(englishBookName);

                foreach (var pair in _readBooksCache)
                {
                    if (NormalizeBookName(pair.Key) == normalizedSearch)
                    {
                        localizedBookTitle = pair.Value;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(localizedBookTitle))
                {
                    ModEntry.Instance.Monitor.Log(
                        $"[ReadBookResolver] Book not found: '{englishBookName}'",
                        LogLevel.Trace
                    );

                    return false;
                }
            }

            if (!ModEntry.Translation.Get("book.read").HasValue())
            {
                return false;
            }

            localizedItemName = ModEntry
                .Translation.Get("book.read", new { book = localizedBookTitle })
                .ToString();

            return true;
        }

        internal static void WarmUp() => EnsureBookCache();

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _readBooksCache = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }

        private static void EnsureBookCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_readBooksCache != null && _cacheLang == currentLang)
            {
                return;
            }

            lock (_cacheLock)
            {
                if (_readBooksCache != null && _cacheLang == currentLang)
                {
                    return;
                }

                _readBooksCache = BuildCache();
                _cacheLang = currentLang;
            }
        }

        private static string NormalizeBookName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text.Normalize(NormalizationForm.FormKC);

            var sb = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
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

                LoadBookStrings(cache, engManager);
                LoadBookObjects(cache, engManager);

                ModEntry.Instance.Monitor.Log(
                    $"[ReadBookResolver] Loaded {cache.Count} book entries.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[ReadBookResolver] Error building cache: {ex}",
                    LogLevel.Warn
                );
            }

            return cache;
        }

        private static void LoadBookStrings(
            Dictionary<string, string> cache,
            ContentManager engManager
        )
        {
            var engStrings = engManager.Load<Dictionary<string, string>>("Strings\\Objects");

            var locStrings = Game1.content.Load<Dictionary<string, string>>("Strings\\Objects");

            foreach (var kvp in engStrings)
            {
                if (!kvp.Key.StartsWith("Book_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!kvp.Key.EndsWith("_Name", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? englishTitle = kvp.Value?.Trim();

                if (string.IsNullOrWhiteSpace(englishTitle))
                {
                    continue;
                }

                if (!locStrings.TryGetValue(kvp.Key, out string? localizedTitle))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(localizedTitle))
                {
                    continue;
                }

                cache.TryAdd(englishTitle, localizedTitle.Trim());
            }
        }

        private static void LoadBookObjects(
            Dictionary<string, string> cache,
            ContentManager engManager
        )
        {
            var engObjects = engManager.Load<Dictionary<string, ObjectData>>("Data\\Objects");

            foreach (var kvp in engObjects)
            {
                string? englishName = kvp.Value.Name?.Trim();

                if (string.IsNullOrWhiteSpace(englishName))
                {
                    continue;
                }

                var itemData = ItemRegistry.GetDataOrErrorItem($"(O){kvp.Key}");

                if (itemData == null)
                {
                    continue;
                }

                string? localizedName = itemData.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(localizedName))
                {
                    continue;
                }

                cache.TryAdd(englishName, localizedName);
            }
        }
    }
}
