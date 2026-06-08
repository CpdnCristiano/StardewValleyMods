using System;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class SecretNoteLocationResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            var match = Regex.Match(
                englishName,
                @"^Secret Note #(?<number>\d+)(?::\s*(?<title>.+))?$",
                RegexOptions.IgnoreCase
            );
            if (!match.Success)
            {
                return false;
            }

            var number = match.Groups["number"].Value;
            var title = match.Groups["title"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                var localizedTitle = TranslationHelper.GetLocalizedLocationName(title);

                if (!localizedTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
                {
                    localizedName = ModEntry
                        .Translation.Get(
                            "secret_note.with_title_format",
                            new { number, title = localizedTitle }
                        )
                        .ToString();
                    return true;
                }
            }

            localizedName = ModEntry
                .Translation.Get("secret_note.format", new { number })
                .ToString();
            return true;
        }
    }
}
