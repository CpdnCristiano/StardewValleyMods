using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BundleResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.EndsWith(" Bundle", StringComparison.OrdinalIgnoreCase))
            {
                string bundleBaseName = englishName;
                var localizedBundle = TranslationHelper.GetLocalizedBundleName(englishName);
                if (localizedBundle != englishName)
                {
                    bundleBaseName = localizedBundle;
                }
                else
                {
                    var bundlePrefix = englishName.Substring(0, englishName.Length - 7).Trim();
                    var localizedPrefix = TranslationHelper.GetLocalizedBundleName(bundlePrefix);
                    if (localizedPrefix != bundlePrefix)
                        bundleBaseName = localizedPrefix;
                }

                localizedName = ModEntry
                    .Translation.Get("hints.bundle_format", new { name = bundleBaseName })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
