using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BundleResolver : ILocationResolver
    {
        private static Dictionary<string, string>? _vanillaBundleKeysByEnglishName;
        private static Dictionary<string, string>? _localizedBundleNamesByKey;
        private static LocalizedContentManager.LanguageCode _vanillaBundlesLang =
            (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _bundlesLock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.EndsWith(" Bundle", StringComparison.OrdinalIgnoreCase))
            {
                string bundleBaseName = englishName;
                var localizedBundle = ResolveLocalizedBundleName(englishName);
                if (localizedBundle != englishName)
                {
                    bundleBaseName = localizedBundle;
                }
                else
                {
                    var bundlePrefix = englishName.Substring(0, englishName.Length - 7).Trim();
                    var localizedPrefix = ResolveLocalizedBundleName(bundlePrefix);
                    if (localizedPrefix != bundlePrefix)
                    {
                        bundleBaseName = localizedPrefix;
                    }
                }

                if (TryFormatLocalizedBundleName(bundleBaseName, out localizedName))
                {
                    return true;
                }

                return false;
            }
            return false;
        }

        private static bool TryFormatLocalizedBundleName(
            string bundleBaseName,
            out string? localizedName
        )
        {
            localizedName = null;

            try
            {
                var translation = ModEntry.Translation.Get(
                    "hints.bundle_format",
                    new { name = bundleBaseName }
                );
                if (translation.HasValue())
                {
                    var localized = translation.ToString();
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

        public static string ResolveLocalizedBundleName(string englishBundleName)
        {
            if (string.IsNullOrWhiteSpace(englishBundleName))
            {
                return englishBundleName;
            }

            var key = $"bundle.{ResolverText.ToCompactKeySegment(englishBundleName)}";
            if (ResolverText.TryGetTranslation(key, out var localized))
            {
                return localized;
            }

            var vanillaResolved = LookupVanillaBundleTranslation(englishBundleName);
            if (vanillaResolved != englishBundleName)
            {
                return vanillaResolved;
            }

            return TranslationHelper.GetLocalizedItemName(englishBundleName);
        }

        internal static void WarmUp() => EnsureBundlesMap();

        private static void EnsureBundlesMap()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_vanillaBundlesMap != null && _vanillaBundlesLang == currentLang)
            {
                return;
            }

            lock (_bundlesLock)
            {
                if (
                    _vanillaBundleKeysByEnglishName != null
                    && _localizedBundleNamesByKey != null
                    && _vanillaBundlesLang == currentLang
                )
                {
                    return;
                }

                _vanillaBundleKeysByEnglishName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _localizedBundleNamesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _vanillaBundlesLang = currentLang;
                try
                {
                    using var contentManager = new LocalizedContentManager(
                        Game1.game1.Content.ServiceProvider,
                        Game1.game1.Content.RootDirectory
                    );
                    var bundlesData = contentManager.Load<Dictionary<string, string>>(
                        "Data\\Bundles"
                    );
                    if (bundlesData != null)
                    {
                        foreach (var pair in bundlesData)
                        {
                            var value = pair.Value;
                            if (string.IsNullOrWhiteSpace(value))
                            {
                                continue;
                            }

                            var parts = value.Split('/');
                            if (parts.Length <= 5)
                            {
                                continue;
                            }

                            var englishName = parts[0]?.Trim();
                            var locName = parts[5]?.Trim();
                            if (
                                !string.IsNullOrWhiteSpace(englishName)
                                && !string.IsNullOrWhiteSpace(locName)
                            )
                            {
                                _vanillaBundleKeysByEnglishName[englishName] = pair.Key;
                                _localizedBundleNamesByKey[pair.Key] = locName;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log(
                        $"[BundleResolver] Failed to load vanilla bundles: {ex.Message}",
                        LogLevel.Error
                    );
                }
            }
        }

        private static string LookupVanillaBundleTranslation(string englishBundleName)
        {
            EnsureBundlesMap();

            if (
                _vanillaBundleKeysByEnglishName!.TryGetValue(englishBundleName, out var bundleKey)
                && _localizedBundleNamesByKey!.TryGetValue(bundleKey, out var localizedName)
            )
            {
                return localizedName;
            }

            return englishBundleName;
        }

        internal static void ClearCache()
        {
            lock (_bundlesLock)
            {
                _vanillaBundleKeysByEnglishName = null;
                _localizedBundleNamesByKey = null;
                _vanillaBundlesLang = (LocalizedContentManager.LanguageCode)(-1);
            }
        }
    }
}
