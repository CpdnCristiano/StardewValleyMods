using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BuildingKeyResolver : IBuildingResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLowerInvariant();
            var buildingKey = $"building.{sanitized}";
            if (ModEntry.Translation.ContainsKey(buildingKey))
            {
                localizedName = ModEntry.Translation.Get(buildingKey).ToString();
                return true;
            }
            return false;
        }
    }
}
