using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class LevelResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var levelKey = $"level.{sanitized}";
            if (ModEntry.Translation.ContainsKey(levelKey))
            {
                localizedName = ModEntry.Translation.Get(levelKey).ToString();
                return true;
            }
            return false;
        }
    }
}
