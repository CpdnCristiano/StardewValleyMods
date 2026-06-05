namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantItemResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (TranslationHelper.TryResolveTravelingMerchantItemName(englishName, out var loc))
            {
                localizedName = loc;
                return true;
            }
            return false;
        }
    }
}
