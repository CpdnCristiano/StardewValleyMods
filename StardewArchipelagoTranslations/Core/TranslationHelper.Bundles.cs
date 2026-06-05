using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedBundleName(string englishBundleName)
        {
            if (string.IsNullOrWhiteSpace(englishBundleName))
                return englishBundleName;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_localizedBundleNamesCache.TryGetValue(englishBundleName, out var cached))
                {
                    return cached;
                }
            }

            string result;

            var sanitized = englishBundleName.Replace(" ", "").Replace("'", "").ToLower();
            var key = $"bundle.{sanitized}";
            if (ModEntry.Translation.ContainsKey(key))
            {
                result = ModEntry.Translation.Get(key).ToString();
            }
            else
            {
                var vanillaResolved = GetVanillaBundleTranslation(englishBundleName);
                if (vanillaResolved != englishBundleName)
                {
                    result = vanillaResolved;
                }
                else
                {
                    var resolved = ResolveLocalizedItemName(englishBundleName);
                    result = resolved;
                }
            }

            lock (_cachesLock)
            {
                _localizedBundleNamesCache[englishBundleName] = result;
            }

            return result;
        }

        private static string GetVanillaBundleTranslation(string englishBundleName)
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_vanillaBundlesMap == null || _vanillaBundlesLang != currentLang)
            {
                lock (_bundlesLock)
                {
                    if (_vanillaBundlesMap == null || _vanillaBundlesLang != currentLang)
                    {
                        _vanillaBundlesMap = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase
                        );
                        _vanillaBundlesLang = currentLang;
                        try
                        {
                            using (
                                var contentManager = new LocalizedContentManager(
                                    Game1.game1.Content.ServiceProvider,
                                    Game1.game1.Content.RootDirectory
                                )
                            )
                            {
                                var bundlesData = contentManager.Load<Dictionary<string, string>>(
                                    "Data\\Bundles"
                                );
                                if (bundlesData != null)
                                {
                                    foreach (var pair in bundlesData)
                                    {
                                        var value = pair.Value;
                                        if (!string.IsNullOrWhiteSpace(value))
                                        {
                                            var parts = value.Split('/');
                                            if (parts.Length > 5)
                                            {
                                                var englishName = parts[0]?.Trim();
                                                var locName = parts[5]?.Trim();
                                                if (
                                                    !string.IsNullOrWhiteSpace(englishName)
                                                    && !string.IsNullOrWhiteSpace(locName)
                                                )
                                                {
                                                    _vanillaBundlesMap[englishName] = locName;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log(
                                $"Failed to load vanilla bundles from clean content manager: {ex.Message}",
                                LogLevel.Error
                            );
                        }
                    }
                }
            }

            if (_vanillaBundlesMap.TryGetValue(englishBundleName, out var localizedName))
            {
                return localizedName;
            }

            return englishBundleName;
        }
    }
}
