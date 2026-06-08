using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class SquidFestResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            var match = Regex.Match(
                englishName,
                @"^SquidFest\s+Day\s+(?<day>\d+):\s*(?<tier>.+)$",
                RegexOptions.IgnoreCase
            );
            if (!match.Success)
            {
                return false;
            }

            localizedName = FormatSquidFestCheck(
                ResolveSquidFestName(),
                match.Groups["day"].Value,
                ResolveTier(match.Groups["tier"].Value.Trim())
            );
            return true;
        }

        private static string ResolveSquidFestName()
        {
            if (TranslationHelper.TryGetLocalizedGameString("SquidFest", out var localized))
            {
                return localized;
            }

            try
            {
                localized = Game1.content.LoadString("Strings\\1_6_Strings:SquidFest");
                if (!string.IsNullOrWhiteSpace(localized) && !localized.Equals("SquidFest"))
                {
                    return localized;
                }
            }
            catch { }

            return "SquidFest";
        }

        private static string ResolveTier(string tier)
        {
            if (TranslationHelper.TryGetLocalizedGameString(tier, out var localized))
            {
                return localized;
            }

            if (TryResolveDebrisString(tier, out localized))
            {
                return localized;
            }

            return tier;
        }

        private static bool TryResolveDebrisString(string englishText, out string localized)
        {
            localized = string.Empty;

            string[] debrisKeys =
            {
                "Debris.cs.621",
                "Debris.cs.622",
                "Debris.cs.623",
                "Debris.cs.624",
                "Debris.cs.625",
                "Debris.cs.626",
            };

            try
            {
                using var rawManager = new ContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );
                var englishStrings = rawManager.Load<Dictionary<string, string>>(
                    "Strings\\StringsFromCSFiles"
                );
                var localizedStrings = Game1.content.Load<Dictionary<string, string>>(
                    "Strings\\StringsFromCSFiles"
                );

                foreach (var key in debrisKeys)
                {
                    if (
                        englishStrings.TryGetValue(key, out var english)
                        && english.Equals(englishText, StringComparison.OrdinalIgnoreCase)
                        && localizedStrings.TryGetValue(key, out localized)
                        && !string.IsNullOrWhiteSpace(localized)
                    )
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static string FormatSquidFestCheck(string eventName, string day, string tier)
        {
            var fallback = $"{eventName} Day {day}: {tier}";

            try
            {
                const string key = "squidfest.day_reward_format";
                var translation = ModEntry.Translation.Get(key, new { eventName, day, tier });
                if (translation.HasValue())
                {
                    var localized = translation.ToString();
                    if (!string.IsNullOrWhiteSpace(localized))
                    {
                        return localized;
                    }
                }
            }
            catch { }

            return fallback;
        }
    }
}
