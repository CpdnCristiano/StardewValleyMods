using System;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class SkillLevelResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var levelSkillMatch = Regex.Match(
                englishName,
                @"^Level\s+(\d+)\s+([A-Za-z][A-Za-z\s']*)$",
                RegexOptions.IgnoreCase
            );
            if (levelSkillMatch.Success)
            {
                var levelNumber = levelSkillMatch.Groups[1].Value.Trim();
                var rawSkillName = levelSkillMatch.Groups[2].Value.Trim();
                var skillLevelKey = $"level.{ResolverText.ToKeySegment(rawSkillName)}_level";

                if (ResolverText.TryGetTranslation(skillLevelKey, out var localizedSkillLevel))
                {
                    localizedName = ModEntry
                        .Translation.Get(
                            "hints.skill_level_format",
                            new { level = levelNumber, skill = localizedSkillLevel }
                        )
                        .ToString();
                    return true;
                }
            }
            return false;
        }
    }
}
