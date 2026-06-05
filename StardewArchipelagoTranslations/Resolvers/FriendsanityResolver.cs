using System;
using System.Text.RegularExpressions;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class FriendsanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            // Format: "Friendsanity: <NPC> <Hearts> <3"
            var match = Regex.Match(
                englishName,
                @"^Friendsanity:\s+(.+?)\s+(\d+)\s+<3$",
                RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                var npcName = match.Groups[1].Value.Trim();
                var hearts = match.Groups[2].Value.Trim();

                // Get localized NPC name
                var localizedNpc = GetLocalizedNpcName(npcName);

                // Translate to: "Amizade: <NPC> (<Hearts> ♡)"
                localizedName = $"Amizade: {localizedNpc} ({hearts} ♡)";
                return true;
            }

            return false;
        }

        private string GetLocalizedNpcName(string npcName)
        {
            // First try to get it from loaded game data
            try
            {
                var character = Game1.getCharacterFromName(npcName);
                if (character != null && !string.IsNullOrWhiteSpace(character.displayName))
                {
                    return character.displayName;
                }
            }
            catch { }

            // Fallback: look up in i18n using npc.<sanitized_name> key
            var sanitized = npcName.Replace(" ", "_").Replace(".", "").ToLowerInvariant();
            var key = $"npc.{sanitized}";
            if (ModEntry.Translation.ContainsKey(key))
            {
                return ModEntry.Translation.Get(key).ToString();
            }

            // Last resort: return original name
            return npcName;
        }
    }
}
