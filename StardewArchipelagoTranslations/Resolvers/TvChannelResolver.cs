using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TvChannelResolver : IItemResolver
    {
        internal static readonly Dictionary<string, string> GameStringKeys = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Weather Report", "Strings\\StringsFromCSFiles:TV.cs.13105" },
            { "Fortune Teller", "Strings\\StringsFromCSFiles:TV.cs.13107" },
            { "Livin' Off The Land", "Strings\\StringsFromCSFiles:TV.cs.13111" },
            { "The Queen of Sauce", "Strings\\StringsFromCSFiles:TV.cs.13114" },
            { "The Queen of Sauce (Re-run)", "Strings\\StringsFromCSFiles:TV.cs.13117" },
            {
                "Fishing Information Broadcasting Service",
                "Strings\\StringsFromCSFiles:TV_Fishing_Channel"
            },
        };

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (GameStringKeys.TryGetValue(englishName, out var tvContentPath))
            {
                try
                {
                    var tvName = Game1.content.LoadString(tvContentPath);
                    if (!string.IsNullOrWhiteSpace(tvName))
                    {
                        localizedName = ModEntry
                            .Translation.Get("hints.tv_channel_format", new { name = tvName })
                            .ToString();
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
