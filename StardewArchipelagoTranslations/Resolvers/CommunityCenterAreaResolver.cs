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

            var clean = ResolverText.ToPascalAssetSegment(englishAreaName);

            if (
                ResolverText.TryLoadGameString(
                    $"Strings\\Locations:CommunityCenter_AreaName_{clean}",
                    out var localized
                )
            )
            {
                return localized;
            }

            if (ResolverText.TryLoadGameString($"Strings\\UI:CommunityCenter_AreaName_{clean}", out localized))
            {
                return localized;
            }

            if (ResolverText.TryLoadGameString($"Strings\\Locations:{clean}", out localized))
            {
                return localized;
            }

            var sanitized = ResolverText.ToKeySegment(englishAreaName);
            if (ResolverText.TryGetTranslation($"item.{sanitized}", out localized))
            {
                return localized;
            }

            if (ResolverText.TryGetTranslation($"location.{sanitized}", out localized))
            {
                return localized;
            }

            return englishAreaName;
        }
    }
}
