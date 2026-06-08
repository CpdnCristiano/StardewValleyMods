using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class PowerKeyResolver : IItemResolver
    {
        private static Dictionary<string, string>? _vanillaPowersNameMap;
        private static LocalizedContentManager.LanguageCode _cacheLang =
            (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _powersLock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var powerKey = $"power.{ResolverText.ToKeySegment(englishName)}.name";
            if (ResolverText.TryGetTranslation(powerKey, out var localizedPowerName))
            {
                localizedName = localizedPowerName;
                return true;
            }

            try
            {
                EnsurePowersMap();

                var cleanKey = ResolverText.ToCompactKeySegment(englishName);
                if (
                    _vanillaPowersNameMap!.TryGetValue(cleanKey, out var rawDisplayName)
                    || _vanillaPowersNameMap.TryGetValue(englishName, out rawDisplayName)
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

        internal static void WarmUp() => EnsurePowersMap();

        private static void EnsurePowersMap()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_vanillaPowersNameMap != null && _cacheLang == currentLang)
            {
                return;
            }

            lock (_powersLock)
            {
                if (_vanillaPowersNameMap != null && _cacheLang == currentLang)
                {
                    return;
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var powers = Game1.content.Load<
                    Dictionary<string, StardewValley.GameData.Powers.PowersData>
                >("Data\\Powers");
                if (powers != null)
                {
                    foreach (var pair in powers)
                    {
                        if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.DisplayName))
                        {
                            continue;
                        }

                        map[pair.Key] = pair.Value.DisplayName;
                        var cleanPairKey = ResolverText.ToCompactKeySegment(pair.Key);
                        map[cleanPairKey] = pair.Value.DisplayName;
                    }
                }

                _vanillaPowersNameMap = map;
                _cacheLang = currentLang;
            }
        }

        internal static void ClearCache()
        {
            lock (_powersLock)
            {
                _vanillaPowersNameMap = null;
                _cacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }
    }
}
