namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class LocationKeyResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var key = $"location.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(key, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
