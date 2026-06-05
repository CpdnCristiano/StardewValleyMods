using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class StringsBuildingsResolver : IBuildingResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var cleanBuildingName = englishName.Replace(" ", "").Replace("'", "");

            // Try standard building strings first
            try
            {
                var buildingStringKey = $"Strings\\Buildings:{cleanBuildingName}_Name";
                var localizedBuildingName = Game1.content.LoadString(buildingStringKey);
                if (
                    !string.IsNullOrWhiteSpace(localizedBuildingName)
                    && localizedBuildingName != buildingStringKey
                )
                {
                    localizedName = localizedBuildingName;
                    return true;
                }
            }
            catch { }

            // Try farmhouse upgrades (e.g. Kitchen -> FarmHouse_Kitchen)
            try
            {
                var farmhouseStringKey = $"Strings\\Locations:FarmHouse_{cleanBuildingName}";
                var localizedFarmhouse = Game1.content.LoadString(farmhouseStringKey);
                if (
                    !string.IsNullOrWhiteSpace(localizedFarmhouse)
                    && localizedFarmhouse != farmhouseStringKey
                )
                {
                    localizedName = localizedFarmhouse;
                    return true;
                }
            }
            catch { }

            return false;
        }
    }
}
