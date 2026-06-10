using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class BooksellerResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            var key = $"item.{ResolverText.ToKeySegment(englishName)}";
            if (ResolverText.TryGetTranslation(key, out var directTranslation))
            {
                localizedName = directTranslation;
                return true;
            }

            const string booksellerStockPrefix = "Bookseller Stock: ";
            if (!englishName.StartsWith(booksellerStockPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var stockName = englishName.Substring(booksellerStockPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(stockName))
            {
                return false;
            }

            var localizedStockName = TranslationHelper.GetLocalizedItemName(stockName);
            localizedName = ModEntry
                .Translation.Get("item.bookseller_stock_format", new { stock = localizedStockName })
                .ToString();
            return true;
        }
    }
}
