namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TrapResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var trapKey = $"trap.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(trapKey, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
