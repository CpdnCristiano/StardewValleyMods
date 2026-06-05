namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BuildingItemResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var localizedBuilding = TranslationHelper.GetLocalizedBuildingName(englishName);
            if (localizedBuilding != englishName)
            {
                localizedName = localizedBuilding;
                return true;
            }
            return false;
        }
    }
}
