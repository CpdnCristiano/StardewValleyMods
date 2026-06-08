using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class EatsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            const string eatPrefix = "Eat ";
            if (englishName.StartsWith(eatPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var itemName = englishName.Substring(eatPrefix.Length).Trim();
                var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
                localizedName = ModEntry
                    .Translation.Get("eatsanity.eat_format", new { item = localizedItem })
                    .ToString();
                return true;
            }

            const string drinkPrefix = "Drink ";
            if (englishName.StartsWith(drinkPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var itemName = englishName.Substring(drinkPrefix.Length).Trim();
                var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
                localizedName = ModEntry
                    .Translation.Get(
                        "eatsanity.drink_format",
                        new { item = localizedItem }
                    )
                    .ToString();
                return true;
            }

            return false;
        }
    }
}
