namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantScamResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var key = $"traveling_merchant.scam.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(key, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
