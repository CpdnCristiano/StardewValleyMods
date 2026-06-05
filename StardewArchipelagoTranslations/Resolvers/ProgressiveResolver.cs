using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ProgressiveResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var progressiveKey = $"progressive.{sanitized}";
            if (ModEntry.Translation.ContainsKey(progressiveKey))
            {
                localizedName = ModEntry.Translation.Get(progressiveKey).ToString();
                return true;
            }
            if (
                TranslationHelper.TryResolveProgressiveItemName(
                    englishName,
                    out var progressiveName
                )
            )
            {
                localizedName = progressiveName;
                return true;
            }
            return false;
        }
    }
}
