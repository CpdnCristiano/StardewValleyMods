using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class FriendshipBonusResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            var match = Regex.Match(
                englishName,
                @"^Friendship Bonus \(([\d.,]+)\s*<3\)$",
                RegexOptions.IgnoreCase
            );
            if (!match.Success)
            {
                return false;
            }

            var heartsText = match.Groups[1].Value.Trim().Replace(',', '.');
            if (!double.TryParse(heartsText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hearts))
            {
                return false;
            }

            localizedName = ModEntry
                .Translation.Get("item.friendship_bonus_format", new { hearts = FormatHearts(hearts) })
                .ToString();
            return true;
        }

        private static string FormatHearts(double hearts)
        {
            if (Math.Abs(hearts % 1) < 0.0001)
            {
                return ((int)hearts).ToString(CultureInfo.InvariantCulture);
            }

            return hearts.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
