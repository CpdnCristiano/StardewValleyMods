using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class TvChannelLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (TvChannelResolver.GameStringKeys.TryGetValue(englishName, out var tvContentPath))
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
