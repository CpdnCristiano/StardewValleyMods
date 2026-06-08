namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BuildingKeyResolver : IBuildingResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var buildingKey = $"building.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(buildingKey, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
