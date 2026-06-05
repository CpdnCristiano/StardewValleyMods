using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TrapResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var trapKey = $"trap.{sanitized}";
            if (ModEntry.Translation.ContainsKey(trapKey))
            {
                localizedName = ModEntry.Translation.Get(trapKey).ToString();
                return true;
            }
            return false;
        }
    }
}
