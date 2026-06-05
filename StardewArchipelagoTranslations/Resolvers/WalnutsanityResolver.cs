using System;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class WalnutsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Walnutsanity: ";
            if (!englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var detail = englishName.Substring(prefix.Length).Trim();
            var sanitizedDetail = detail
                .Replace(" ", "_")
                .Replace("'", "")
                .Replace("#", "")
                .ToLowerInvariant();
            sanitizedDetail = System.Text.RegularExpressions.Regex.Replace(
                sanitizedDetail,
                @"_+",
                "_"
            );
            var detailKey = $"location.walnutsanity.{sanitizedDetail}";

            if (ModEntry.Translation.ContainsKey(detailKey))
            {
                var localizedDetail = ModEntry.Translation.Get(detailKey).ToString();
                localizedName = localizedDetail;
                return true;
            }

            var generalKey = $"location.{sanitizedDetail}";
            if (ModEntry.Translation.ContainsKey(generalKey))
            {
                var localizedDetail = ModEntry.Translation.Get(generalKey).ToString();
                localizedName = localizedDetail;
                return true;
            }

            return false;
        }
    }
}
