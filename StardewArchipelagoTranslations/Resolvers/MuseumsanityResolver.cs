using System;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class MuseumsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Museumsanity: ";
            if (!englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var detail = englishName.Substring(prefix.Length).Trim();

            // Check for specific special collections
            if (detail.Equals("Dwarf Scrolls", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.dwarf_scrolls")
                    .ToString();
                return true;
            }
            if (detail.Equals("Skeleton Front", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.skeleton_front")
                    .ToString();
                return true;
            }
            if (detail.Equals("Skeleton Middle", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.skeleton_middle")
                    .ToString();
                return true;
            }
            if (detail.Equals("Skeleton Back", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.skeleton_back")
                    .ToString();
                return true;
            }

            // Check for milestone regexes
            var donationsMatch = Regex.Match(
                detail,
                @"^(\d+)\s+Donations$",
                RegexOptions.IgnoreCase
            );
            if (donationsMatch.Success)
            {
                var count = donationsMatch.Groups[1].Value;
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.donations", new { count })
                    .ToString();
                return true;
            }

            var mineralsMatch = Regex.Match(detail, @"^(\d+)\s+Minerals$", RegexOptions.IgnoreCase);
            if (mineralsMatch.Success)
            {
                var count = mineralsMatch.Groups[1].Value;
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.minerals", new { count })
                    .ToString();
                return true;
            }

            var artifactsMatch = Regex.Match(
                detail,
                @"^(\d+)\s+Artifacts$",
                RegexOptions.IgnoreCase
            );
            if (artifactsMatch.Success)
            {
                var count = artifactsMatch.Groups[1].Value;
                localizedName = ModEntry
                    .Translation.Get("location.museumsanity.artifacts", new { count })
                    .ToString();
                return true;
            }

            // Otherwise, it is an individual item donation, resolve the item name
            localizedName = ModEntry
                .Translation.Get(
                    "location.museumsanity.item_format",
                    new { item = TranslationHelper.GetLocalizedItemName(detail) }
                )
                .ToString();
            return true;
        }
    }
}
