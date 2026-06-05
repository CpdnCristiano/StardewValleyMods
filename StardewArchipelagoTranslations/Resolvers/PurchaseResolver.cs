using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class PurchaseResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.StartsWith("Purchase ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanItemName = englishName.Substring(9).Trim();
                var localizedItem = TranslationHelper.GetLocalizedItemName(cleanItemName);
                localizedName = ModEntry
                    .Translation.Get("hints.purchase_format", new { name = localizedItem })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
