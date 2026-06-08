using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ProgressiveResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (!Regex.IsMatch(englishName, @"\bProgressive\b", RegexOptions.IgnoreCase))
            {
                return false;
            }

            var sanitized = englishName.Replace(" ", "_").Replace("'", "").ToLower();
            var progressiveKey = $"progressive.{sanitized}";
            if (ModEntry.Translation.ContainsKey(progressiveKey))
            {
                localizedName = ModEntry.Translation.Get(progressiveKey).ToString();
                return true;
            }

            var normalized = Regex.Replace(englishName, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var coreTokens = tokens
                .Where(t => !t.Equals("Progressive", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (coreTokens.Length == 0)
            {
                return false;
            }

            var coreName = string.Join(" ", coreTokens);
            var coreSanitized = coreName.Replace(" ", "_").Replace("'", "").ToLowerInvariant();
            var progressiveCandidates = new[]
            {
                $"progressive.progressive_{coreSanitized}",
                $"progressive.{coreSanitized}_progressive",
                $"progressive.{coreSanitized}",
            };

            foreach (var key in progressiveCandidates)
            {
                if (ModEntry.Translation.ContainsKey(key))
                {
                    localizedName = ModEntry.Translation.Get(key).ToString();
                    return true;
                }
            }

            var localizedCore = TranslationHelper.GetLocalizedItemName(coreName);
            localizedName = ModEntry
                .Translation.Get("hints.progressive_format", new { name = localizedCore })
                .ToString();
            return true;
        }
    }
}
