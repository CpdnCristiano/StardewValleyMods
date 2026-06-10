using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ShipsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Shipsanity: ";
            if (!englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var itemName = englishName.Substring(prefix.Length).Trim();
            var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
            localizedName = ModEntry
                .Translation.Get("shipsanity.format", new { item = localizedItem })
                .ToString();
            return true;
        }
    }
}
