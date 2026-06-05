using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class HarvestResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.StartsWith("Harvest ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanItemName = englishName.Substring(8).Trim();
                var localizedItem = TranslationHelper.GetLocalizedItemName(cleanItemName);
                localizedName = ModEntry
                    .Translation.Get("hints.harvest_format", new { name = localizedItem })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
