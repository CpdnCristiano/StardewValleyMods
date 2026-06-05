using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class PowerPrefixResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string powerPrefix = "Power: ";
            if (englishName.StartsWith(powerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var barePowerName = englishName.Substring(powerPrefix.Length).Trim();
                var localizedPower = TranslationHelper.GetLocalizedItemName(barePowerName);
                localizedName = ModEntry
                    .Translation.Get("hints.power_item_format", new { power = localizedPower })
                    .ToString();
                return true;
            }
            return false;
        }
    }
}
