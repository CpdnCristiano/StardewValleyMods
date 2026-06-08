using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedItemName(string englishItemName)
        {
            if (string.IsNullOrWhiteSpace(englishItemName))
            {
                return englishItemName;
            }

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedItemNamesCache.TryGetValue(englishItemName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishItemName;
            try
            {
                result = ResolveLocalizedItemName(englishItemName);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error resolving item name for '{englishItemName}': {ex}",
                    LogLevel.Error
                );
            }

            lock (_cachesLock)
            {
                _resolvedItemNamesCache[englishItemName] = result;
            }

            ModEntry.Instance.Monitor.Log(
                $"GetLocalizedItemName (Cache Miss): Input = '{englishItemName}', Output = '{result}'",
                LogLevel.Trace
            );
            return result;
        }

        private static string ResolveLocalizedItemName(string englishItemName)
        {
            var result = englishItemName;

            foreach (var resolver in _itemResolvers)
            {
                if (resolver.TryResolve(englishItemName, out var localized) && localized != null)
                {
                    result = localized;
                    break;
                }
            }

            return result;
        }
    }
}
