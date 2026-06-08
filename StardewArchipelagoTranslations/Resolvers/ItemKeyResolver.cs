namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ItemKeyResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var itemKey = $"item.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(itemKey, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
