using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ItemKeyResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var itemKey = $"item.{sanitized}";
            if (ModEntry.Translation.ContainsKey(itemKey))
            {
                localizedName = ModEntry.Translation.Get(itemKey).ToString();
                return true;
            }
            return false;
        }
    }
}
