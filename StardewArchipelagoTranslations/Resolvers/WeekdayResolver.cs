using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class WeekdayResolver
    {
        private static Dictionary<string, string>? _weekdayCache;
        private static readonly object _cacheLock = new();

        internal static string GetLocalizedWeekday(string englishWeekday)
        {
            if (string.IsNullOrWhiteSpace(englishWeekday))
            {
                return englishWeekday;
            }

            EnsureCache();

            if (_weekdayCache!.TryGetValue(englishWeekday.Trim(), out var localized))
            {
                return localized;
            }

            if (
                TranslationHelper.TryGetLocalizedGameString(englishWeekday, out var globalLocalized)
            )
            {
                return globalLocalized;
            }

            return englishWeekday;
        }

        internal static void WarmUp() => EnsureCache();

        private static void EnsureCache()
        {
            if (_weekdayCache != null)
            {
                return;
            }

            lock (_cacheLock)
            {
                if (_weekdayCache != null)
                {
                    return;
                }

                var newCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    string[] engDays =
                    {
                        "Wednesday",
                        "Thursday",
                        "Tuesday",
                        "Monday",
                        "Saturday",
                        "Sunday",
                        "Friday",
                    };

                    string locString = Game1.content.LoadString(
                        "Strings\\1_6_Strings:Scholar_Question_1_2_Answers"
                    );
                    string[] locDays = locString.Split(',');

                    if (engDays.Length == locDays.Length)
                    {
                        for (int i = 0; i < engDays.Length; i++)
                        {
                            newCache[engDays[i]] = locDays[i].Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log(
                        $"[WeekdayResolver] Error building cache: {ex.Message}",
                        LogLevel.Trace
                    );
                }

                _weekdayCache = newCache;
            }
        }

        internal static void ClearCache()
        {
            lock (_cacheLock)
            {
                _weekdayCache = null;
            }
        }
    }
}
