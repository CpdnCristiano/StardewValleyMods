using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class CommunityCenterAreaResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.StartsWith("Complete ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanAreaName = englishName.Substring(9).Trim();
                var localizedArea = TranslationHelper.GetLocalizedAreaName(cleanAreaName);
                localizedName = ModEntry
                    .Translation.Get("hints.complete_area_format", new { name = localizedArea })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
