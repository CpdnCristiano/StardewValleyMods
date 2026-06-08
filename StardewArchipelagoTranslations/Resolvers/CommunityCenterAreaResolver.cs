using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class CommunityCenterAreaResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.StartsWith("Complete ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanAreaName = englishName.Substring(9).Trim();
                var localizedArea = ResolveLocalizedAreaName(cleanAreaName);
                localizedName = ModEntry
                    .Translation.Get("hints.complete_area_format", new { name = localizedArea })
                    .ToString();
                return true;
            }
            return false;
        }

        private static string ResolveLocalizedAreaName(string englishAreaName)
        {
            if (string.IsNullOrWhiteSpace(englishAreaName))
            {
                return englishAreaName;
            }

            var clean = englishAreaName.Replace(" ", "");

            try
            {
                var key = $"Strings\\Locations:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\UI:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\Locations:{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            var sanitized = englishAreaName.Replace(" ", "_").Replace("'", "").ToLower();
            var itemKey = $"item.{sanitized}";

            if (ModEntry.Translation.ContainsKey(itemKey))
            {
                return ModEntry.Translation.Get(itemKey).ToString();
            }

            var locKey = $"location.{sanitized}";
            if (ModEntry.Translation.ContainsKey(locKey))
            {
                return ModEntry.Translation.Get(locKey).ToString();
            }

            return englishAreaName;
        }
    }
}
