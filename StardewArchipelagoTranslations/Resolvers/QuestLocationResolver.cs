namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class QuestLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (TranslationHelper.TryResolveLocalizedQuestLocation(englishName, out var loc))
            {
                localizedName = loc;
                return true;
            }
            return false;
        }
    }
}
