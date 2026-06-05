using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class PowerKeyResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitizedPowerName = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var powerKey = $"power.{sanitizedPowerName}.name";
            if (ModEntry.Translation.ContainsKey(powerKey))
            {
                localizedName = ModEntry.Translation.Get(powerKey).ToString();
                return true;
            }

            try
            {
                if (TranslationHelper._vanillaPowersNameMap == null)
                {
                    lock (TranslationHelper._powersLock)
                    {
                        if (TranslationHelper._vanillaPowersNameMap == null)
                        {
                            TranslationHelper._vanillaPowersNameMap = new Dictionary<
                                string,
                                string
                            >(StringComparer.OrdinalIgnoreCase);
                            var powers = Game1.content.Load<
                                Dictionary<string, StardewValley.GameData.Powers.PowersData>
                            >("Data\\Powers");
                            if (powers != null)
                            {
                                foreach (var pair in powers)
                                {
                                    if (
                                        pair.Value != null
                                        && !string.IsNullOrWhiteSpace(pair.Value.DisplayName)
                                    )
                                    {
                                        TranslationHelper._vanillaPowersNameMap[pair.Key] =
                                            pair.Value.DisplayName;
                                        var cleanPairKey = pair
                                            .Key.Replace(" ", "")
                                            .Replace("'", "")
                                            .Replace("_", "");
                                        TranslationHelper._vanillaPowersNameMap[cleanPairKey] =
                                            pair.Value.DisplayName;
                                    }
                                }
                            }
                        }
                    }
                }

                var cleanKey = englishName.Replace(" ", "").Replace("'", "").Replace("_", "");
                if (
                    TranslationHelper._vanillaPowersNameMap.TryGetValue(
                        cleanKey,
                        out var rawDisplayName
                    )
                    || TranslationHelper._vanillaPowersNameMap.TryGetValue(
                        englishName,
                        out rawDisplayName
                    )
                )
                {
                    var localized = TokenParser.ParseText(rawDisplayName);
                    if (!string.IsNullOrWhiteSpace(localized))
                    {
                        localizedName = localized;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
