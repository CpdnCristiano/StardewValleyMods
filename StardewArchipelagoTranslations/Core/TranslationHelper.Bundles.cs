namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedBundleName(string englishBundleName) =>
            BundleResolver.ResolveLocalizedBundleName(englishBundleName);
    }
}
