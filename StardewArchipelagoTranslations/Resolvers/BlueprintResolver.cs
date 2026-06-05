using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BlueprintResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.EndsWith(" Blueprint", StringComparison.OrdinalIgnoreCase))
            {
                var cleanBuildingName = englishName.Substring(0, englishName.Length - 10).Trim();
                var localizedBuilding = TranslationHelper.GetLocalizedBuildingName(
                    cleanBuildingName
                );
                localizedName = ModEntry
                    .Translation.Get("hints.blueprint_format", new { name = localizedBuilding })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
