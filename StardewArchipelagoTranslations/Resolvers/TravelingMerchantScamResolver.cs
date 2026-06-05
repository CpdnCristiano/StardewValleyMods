using System.Collections.Generic;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TravelingMerchantScamResolver : ILocationResolver
    {
        private static readonly HashSet<string> _scamItems = new(
            System.StringComparer.OrdinalIgnoreCase
        )
        {
            "Snake Oil",
            "Glass of time",
            "Orb of Slope Detection",
            "Dagger of Time",
            "Harpy's Quill",
            "Oil of Slipperiness",
            "Gauntlets of Touch",
            "Disk of Enlargement",
            "Torch of Night Vision",
            "Potion of Hydration",
            "Viper Liquid",
            "Fire Distinguisher",
            "Bag of Holding",
            "Stone of Weather Detection",
            "Tigerbane Stone",
            "Eyepatch of 2D Vision",
            "Mirror of Reflection",
            "Potion of Courage",
            "Moveable Rod",
            "Orb of shattering",
            "Spectacles of Darkness",
            "Leash of Holding",
            "Dagger of Desperation",
            "Dihydrogen Monoxide Grenade",
            "Pan of Frying",
            "Pan of Drying",
            "Ringing Ring",
        };

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (_scamItems.Contains(englishName))
            {
                var key =
                    "traveling_merchant.scam."
                    + englishName.Replace(" ", "_").Replace("'", "").ToLowerInvariant();
                if (ModEntry.Translation.ContainsKey(key))
                {
                    localizedName = ModEntry.Translation.Get(key).ToString();
                    return true;
                }
            }
            return false;
        }
    }
}
