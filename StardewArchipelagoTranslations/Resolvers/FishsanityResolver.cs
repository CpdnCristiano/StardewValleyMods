using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class FishsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Fishsanity: ";
            if (!englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fishName = englishName.Substring(prefix.Length).Trim();
            var localizedFish = TranslationHelper.GetLocalizedItemName(fishName);
            localizedName = ModEntry
                .Translation.Get("fishsanity.format", new { fish = localizedFish })
                .ToString();
            return true;
        }
    }
}
