using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantLocationResolver : ILocationResolver
    {
        private static readonly Regex _itemPattern = new(
            @"^Traveling Merchant\s+(.+?)\s+Item\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            // "Traveling Merchant <Day> Item <N>"  →  location check de compra
            var itemMatch = _itemPattern.Match(englishName);
            if (itemMatch.Success)
            {
                var englishDay = itemMatch.Groups[1].Value.Trim();
                var itemLabel = itemMatch.Groups[2].Value.Trim();
                var localizedDay = WeekdayResolver.GetLocalizedWeekday(englishDay);
                var template = ModEntry
                    .Translation.Get("traveling_merchant_item_format")
                    .ToString();
                localizedName = template
                    .Replace("{{day}}", localizedDay)
                    .Replace("{{item}}", itemLabel)
                    .Replace("{day}", localizedDay)
                    .Replace("{item}", itemLabel);
                return true;
            }

            // "Traveling Merchant: <Day>"  →  location check de visita
            const string dayPrefix = "Traveling Merchant: ";
            if (englishName.StartsWith(dayPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishName.Substring(dayPrefix.Length).Trim();
                var localizedDay = WeekdayResolver.GetLocalizedWeekday(englishDay);
                var template = ModEntry.Translation.Get("traveling_merchant_day_format").ToString();
                localizedName = template
                    .Replace("{{day}}", localizedDay)
                    .Replace("{day}", localizedDay);
                return true;
            }

            // Stock Size, Discount e Metal Detector são ITEMS recebidos,
            // não location checks — tratados no TravelingMerchantItemResolver.
            return false;
        }
    }
}
