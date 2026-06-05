using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class MoneyResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.Equals("Money", StringComparison.OrdinalIgnoreCase))
            {
                var key = "item.money";
                if (ModEntry.Translation.ContainsKey(key))
                {
                    localizedName = ModEntry.Translation.Get(key).ToString();
                    return true;
                }

                try
                {
                    var gSuffix = Game1.content.LoadString(
                        "Strings\\StringsFromCSFiles:Utility.cs.5627"
                    );
                    if (!string.IsNullOrWhiteSpace(gSuffix))
                    {
                        localizedName = gSuffix;
                        return true;
                    }
                }
                catch { }

                try
                {
                    var goldText = Game1.content.LoadString(
                        "Strings\\StringsFromCSFiles:ShopMenu.cs.11474"
                    );
                    if (!string.IsNullOrWhiteSpace(goldText))
                    {
                        localizedName = goldText;
                        return true;
                    }
                }
                catch { }

                localizedName = "g";
                return true;
            }
            return false;
        }
    }
}
