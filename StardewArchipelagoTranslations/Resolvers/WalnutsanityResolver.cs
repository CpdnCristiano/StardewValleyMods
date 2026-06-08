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
            var sanitizedDetail = ResolverText.ToKeySegment(detail);
            var detailKey = $"location.walnutsanity.{sanitizedDetail}";

            if (ResolverText.TryGetTranslation(detailKey, out var localizedDetail))
            {
                localizedName = localizedDetail;
                return true;
            }

            var generalKey = $"location.{sanitizedDetail}";
            if (ResolverText.TryGetTranslation(generalKey, out localizedDetail))
            {
                localizedName = localizedDetail;
                return true;
            }

            return false;
        }
    }
}
