using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class CraftLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            const string craftPrefix = "Craft ";
            if (!englishName.StartsWith(craftPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var itemName = englishName.Substring(craftPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
            localizedName = ModEntry
                .Translation.Get("location.craft_format", new { item = localizedItem })
                .ToString();
            return true;
        }
    }
}
