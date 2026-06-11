using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class QuestLocationResolver : ILocationResolver
    {
        private static Dictionary<string, string>? _questIdsByEnglishTitle;
        private static Dictionary<string, string>? _localizedQuestTitlesById;
        private static LocalizedContentManager.LanguageCode _cacheLang =
            (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _cacheLock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            const string questPrefix = "Quest: ";
            if (!englishName.StartsWith(questPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var englishQuestTitle = NormalizeQuestTitle(
                englishName.Substring(questPrefix.Length).Trim()
            );
            if (string.IsNullOrWhiteSpace(englishQuestTitle))
            {
                return false;
            }

            try
            {
                EnsureQuestCache();

                if (
                    _questIdsByEnglishTitle!.TryGetValue(
                        englishQuestTitle,
                        out var questId
                    )
                    && _localizedQuestTitlesById!.TryGetValue(questId, out var localizedTitle)
                )
                {
                    localizedName = $"{questPrefix}{localizedTitle}";
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[QuestLocationResolver] Failed to resolve '{englishQuestTitle}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            return false;
        }

        internal static void WarmUp() => EnsureQuestCache();

        private static void EnsureQuestCache()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (
                _questIdsByEnglishTitle != null
                && _localizedQuestTitlesById != null
                && _cacheLang == currentLang
            )
            {
                return;
            }

            lock (_cacheLock)
            {
                if (
                    _questIdsByEnglishTitle != null
                    && _localizedQuestTitlesById != null
                    && _cacheLang == currentLang
                )
                {
                    return;
                }

                var questIdsByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var localizedTitlesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using var engManager = new ContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );

                var engQuests = engManager.Load<Dictionary<string, string>>("Data\\Quests");
                var locQuests = Game1.content.Load<Dictionary<string, string>>("Data\\Quests");

                foreach (var kvp in engQuests)
                {
                    var engParts = kvp.Value.Split('/');
                    if (engParts.Length < 2 || string.IsNullOrWhiteSpace(engParts[1]))
                    {
                        continue;
                    }

                    var engTitle = NormalizeQuestTitle(engParts[1].Trim());

                    if (!locQuests.TryGetValue(kvp.Key, out var locQuestData))
                    {
                        continue;
                    }

                    var locParts = locQuestData.Split('/');
                    if (locParts.Length < 2 || string.IsNullOrWhiteSpace(locParts[1]))
                    {
                        continue;
                    }

                    questIdsByTitle.TryAdd(engTitle, kvp.Key);
                    localizedTitlesById[kvp.Key] = NormalizeQuestTitle(locParts[1].Trim());
                }

                _questIdsByEnglishTitle = questIdsByTitle;
                _localizedQuestTitlesById = localizedTitlesById;
                _cacheLang = currentLang;
            }
        }

        private static string NormalizeQuestTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            title = title.Trim();
            if (title.Length >= 2 && title[0] == '"' && title[^1] == '"')
            {
                return title.Substring(1, title.Length - 2).Trim();
            }

            return title;
        }

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _questIdsByEnglishTitle = null;
                _localizedQuestTitlesById = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }
    }
}
