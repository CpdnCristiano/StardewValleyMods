using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            if (name.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                if (ModEntry.Translation.ContainsKey("player.server"))
                {
                    return ModEntry.Translation.Get("player.server").ToString();
                }
                return
                    LocalizedContentManager.CurrentLanguageCode
                    == LocalizedContentManager.LanguageCode.pt
                    ? "Servidor"
                    : "Server";
            }

            if (name.Equals("Ministry of Madness", StringComparison.OrdinalIgnoreCase))
            {
                if (ModEntry.Translation.ContainsKey("player.ministryofmadness"))
                {
                    return ModEntry.Translation.Get("player.ministryofmadness").ToString();
                }
                if (ModEntry.Translation.ContainsKey("bundle.ministryofmadness"))
                {
                    return ModEntry.Translation.Get("bundle.ministryofmadness").ToString();
                }
                return
                    LocalizedContentManager.CurrentLanguageCode
                    == LocalizedContentManager.LanguageCode.pt
                    ? "Ministério da Loucura"
                    : "Ministry of Madness";
            }

            return name;
        }
    }
}
