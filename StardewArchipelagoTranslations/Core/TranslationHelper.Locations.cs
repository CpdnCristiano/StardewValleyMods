using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedLocationName(string englishLocationName)
        {
            if (string.IsNullOrWhiteSpace(englishLocationName))
            {
                return englishLocationName;
            }

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedLocationNamesCache.TryGetValue(englishLocationName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishLocationName;

            foreach (var resolver in _locationResolvers)
            {
                if (
                    resolver.TryResolve(englishLocationName, out var localized)
                    && localized != null
                )
                {
                    result = localized;
                    break;
                }
            }

            lock (_cachesLock)
            {
                _resolvedLocationNamesCache[englishLocationName] = result;
            }

            return result;
        }

        public static string GetLocalizedBuildingName(string englishBuildingName)
        {
            if (string.IsNullOrWhiteSpace(englishBuildingName))
            {
                return englishBuildingName;
            }

            foreach (var resolver in _buildingResolvers)
            {
                if (
                    resolver.TryResolve(englishBuildingName, out var localized)
                    && localized != null
                )
                {
                    return localized;
                }
            }

            return englishBuildingName;
        }
    }
}
