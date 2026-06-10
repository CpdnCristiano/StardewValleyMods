using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class CookLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            const string cookPrefix = "Cook ";
            if (!englishName.StartsWith(cookPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var itemName = englishName.Substring(cookPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
            localizedName = ModEntry
                .Translation.Get("location.cook_format", new { item = localizedItem })
                .ToString();
            return true;
        }
    }
}
