using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class SeasonResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var seasonNumber = Utility.getSeasonNumber(englishName);
            if (seasonNumber != -1)
            {
                localizedName = Utility.getSeasonNameFromNumber(seasonNumber);
                return true;
            }
            return false;
        }
    }
}
