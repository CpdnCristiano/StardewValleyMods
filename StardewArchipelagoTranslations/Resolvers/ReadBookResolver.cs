using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ReadBookResolver : IItemResolver
    {
        public bool TryResolve(string englishItemName, out string localizedItemName)
        {
            localizedItemName = englishItemName;

            const string readPrefix = "Read ";

            if (!englishItemName.StartsWith(readPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string englishBookName = englishItemName.Substring(readPrefix.Length).Trim();

            if (string.IsNullOrWhiteSpace(englishBookName))
            {
                return false;
            }

            if (
                !PowerBookNameResolver.TryResolve(englishBookName, out var localizedBookTitle)
                || string.IsNullOrWhiteSpace(localizedBookTitle)
            )
            {
                localizedBookTitle = englishBookName;
            }

            if (!ModEntry.Translation.Get("book.read").HasValue())
            {
                return false;
            }

            localizedItemName = ModEntry
                .Translation.Get("book.read", new { book = localizedBookTitle })
                .ToString();

            return true;
        }

        internal static void WarmUp() => PowerBookNameResolver.WarmUp();

        internal static void ClearCache() => PowerBookNameResolver.ClearCache();
    }
}
