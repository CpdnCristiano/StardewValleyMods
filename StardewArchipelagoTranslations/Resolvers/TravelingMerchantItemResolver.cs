namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantItemResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            // "Traveling Merchant: <Day>"  →  item de desbloqueio de dia de visita
            const string dayPrefix = "Traveling Merchant: ";
            if (englishName.StartsWith(dayPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishName.Substring(dayPrefix.Length).Trim();
                var localizedDay = WeekdayResolver.GetLocalizedWeekday(englishDay);
                localizedName = ModEntry
                    .Translation.Get("traveling_merchant_day_format", new { day = localizedDay })
                    .ToString();
                return true;
            }

            // "Traveling Merchant Stock Size"  →  item recebido
            if (
                englishName.Equals(
                    "Traveling Merchant Stock Size",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedName = ModEntry
                    .Translation.Get("traveling_merchant_stock_size")
                    .ToString();
                return true;
            }

            // "Traveling Merchant Discount"  →  item recebido
            if (
                englishName.Equals(
                    "Traveling Merchant Discount",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedName = ModEntry.Translation.Get("traveling_merchant_discount").ToString();
                return true;
            }

            // "Traveling Merchant Metal Detector"  →  item recebido
            if (
                englishName.Equals(
                    "Traveling Merchant Metal Detector",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedName = ModEntry
                    .Translation.Get("traveling_merchant_metal_detector")
                    .ToString();
                return true;
            }

            return false;
        }
    }
}
