using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class PowerKeyResolver : IItemResolver
    {
        private static Dictionary<string, string>? _powerKeysByEnglishName;
        private static Dictionary<string, string>? _localizedPowerNamesByKey;
        private static LocalizedContentManager.LanguageCode _localizedCacheLang =
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

            if (PowerBookNameResolver.TryResolve(englishName, out localizedName))
            {
                return true;
            }

            try
            {
                EnsurePowersMap();

                var cleanKey = ResolverText.ToCompactKeySegment(englishName);
                if (
                    _powerKeysByEnglishName!.TryGetValue(cleanKey, out var powerKeyName)
                    || _powerKeysByEnglishName.TryGetValue(englishName, out powerKeyName)
                )
                {
                    if (
                        _localizedPowerNamesByKey!.TryGetValue(powerKeyName, out var localized)
                        && !string.IsNullOrWhiteSpace(localized)
                    )
                    {
                        localizedName = localized;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        internal static void WarmUp()
        {
            PowerBookNameResolver.WarmUp();
            EnsurePowersMap();
        }

        private static void EnsurePowersMap()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (
                _powerKeysByEnglishName != null
                && _localizedPowerNamesByKey != null
                && _localizedCacheLang == currentLang
            )
            {
                return;
            }

            lock (_powersLock)
            {
                if (
                    _powerKeysByEnglishName != null
                    && _localizedPowerNamesByKey != null
                    && _localizedCacheLang == currentLang
                )
                {
                    return;
                }

                _powerKeysByEnglishName ??= BuildEnglishPowerKeys();
                _localizedPowerNamesByKey = BuildLocalizedPowerNamesByKey();
                _localizedCacheLang = currentLang;
            }
        }

        private static Dictionary<string, string> BuildEnglishPowerKeys()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var savedLang = LocalizedContentManager.CurrentLanguageCode;
            LocalizedContentManager.CurrentLanguageCode = LocalizedContentManager.LanguageCode.en;
            try
            {
                var powers = Game1.content.Load<
                    Dictionary<string, StardewValley.GameData.Powers.PowersData>
                >("Data\\Powers");
                if (powers != null)
                {
                    foreach (var pair in powers)
                    {
                        if (pair.Value == null)
                        {
                            continue;
                        }

                        map[pair.Key] = pair.Key;
                        map[ResolverText.ToCompactKeySegment(pair.Key)] = pair.Key;

                        if (!string.IsNullOrWhiteSpace(pair.Value.DisplayName))
                        {
                            map[pair.Value.DisplayName] = pair.Key;
                            map[ResolverText.ToCompactKeySegment(pair.Value.DisplayName)] = pair.Key;
                        }
                    }
                }
            }
            finally
            {
                LocalizedContentManager.CurrentLanguageCode = savedLang;
            }

            return map;
        }

        private static Dictionary<string, string> BuildLocalizedPowerNamesByKey()
        {
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

                    var localized = TokenParser.ParseText(pair.Value.DisplayName);
                    if (!string.IsNullOrWhiteSpace(localized))
                    {
                        map[pair.Key] = localized;
                    }
                }
            }

            return map;
        }

        internal static void ClearCache()
        {
            lock (_powersLock)
            {
                _powerKeysByEnglishName = null;
                _localizedPowerNamesByKey = null;
                _localizedCacheLang = (LocalizedContentManager.LanguageCode)(-1);
            }

            PowerBookNameResolver.ClearCache();
        }
    }
}
