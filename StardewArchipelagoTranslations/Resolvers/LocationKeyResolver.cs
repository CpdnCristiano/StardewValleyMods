using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class LocationKeyResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var key = $"location.{sanitized}";
            if (ModEntry.Translation.ContainsKey(key))
            {
                localizedName = ModEntry.Translation.Get(key).ToString();
                return true;
            }
            return false;
        }
    }
}
