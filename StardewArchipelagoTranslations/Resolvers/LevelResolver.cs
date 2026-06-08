namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class LevelResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var levelKey = $"level.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(levelKey, out var localized))
            {
                localizedName = localized;
                return true;
            }
            return false;
        }
    }
}
