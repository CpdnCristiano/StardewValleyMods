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

                var localizedNpc = GetLocalizedNpcName(npcName);

                localizedName = ModEntry
                    .Translation.Get(
                        "friendsanity.format",
                        new { npc = localizedNpc, hearts }
                    )
                    .ToString();
                return true;
            }

            return false;
        }

        private string GetLocalizedNpcName(string npcName)
        {
            try
            {
                var character = Game1.getCharacterFromName(npcName);
                if (character != null && !string.IsNullOrWhiteSpace(character.displayName))
                {
                    return character.displayName;
                }
            }
            catch { }

            var key = $"npc.{ResolverText.ToKeySegment(npcName)}";
            if (ResolverText.TryGetTranslation(key, out var localizedNpc))
            {
                return localizedNpc;
            }

            return npcName;
        }
    }
}
