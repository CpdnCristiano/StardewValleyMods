using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class VanillaBuildingResolver : IBuildingResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (
                Game1.buildingData != null
                && Game1.buildingData.TryGetValue(englishName, out var buildingData)
            )
            {
                if (buildingData != null && !string.IsNullOrWhiteSpace(buildingData.Name))
                {
                    var parsed = TokenParser.ParseText(buildingData.Name);
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        localizedName = parsed;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
