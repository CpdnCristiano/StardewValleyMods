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
                var skillLevelKey =
                    $"level.{rawSkillName.Replace(" ", "_").Replace("'", "").ToLower()}_level";

                if (ModEntry.Translation.ContainsKey(skillLevelKey))
                {
                    var localizedSkillLevel = ModEntry.Translation.Get(skillLevelKey).ToString();
                    var localizedSkillName = localizedSkillLevel;

                    if (localizedSkillName.EndsWith(" Level", StringComparison.OrdinalIgnoreCase))
                    {
                        localizedSkillName = localizedSkillName
                            .Substring(0, localizedSkillName.Length - " Level".Length)
                            .Trim();
                    }
                    if (
                        localizedSkillName.StartsWith(
                            "Nível de ",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        localizedSkillName = localizedSkillName
                            .Substring("Nível de ".Length)
                            .Trim();
                    }

                    localizedName = ModEntry
                        .Translation.Get(
                            "hints.skill_level_format",
                            new { level = levelNumber, skill = localizedSkillName }
                        )
                        .ToString();
                    return true;
                }
            }
            return false;
        }
    }
}
