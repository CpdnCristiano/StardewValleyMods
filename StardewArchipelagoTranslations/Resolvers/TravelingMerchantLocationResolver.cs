namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (TranslationHelper.TryResolveTravelingMerchantLocation(englishName, out var loc))
            {
                localizedName = loc;
                return true;
            }
            return false;
        }
    }
}
