namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class DefaultLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = TranslationHelper.GetLocalizedItemName(englishName);
            return true;
        }
    }
}
