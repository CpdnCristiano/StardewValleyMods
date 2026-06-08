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

            var sanitized = ResolverText.ToKeySegment(englishName);
            var progressiveKey = $"progressive.{sanitized}";
            if (ResolverText.TryGetTranslation(progressiveKey, out var progressiveTranslation))
            {
                localizedName = progressiveTranslation;
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
            var coreSanitized = ResolverText.ToKeySegment(coreName);
            var progressiveCandidates = new[]
            {
                $"progressive.progressive_{coreSanitized}",
                $"progressive.{coreSanitized}_progressive",
                $"progressive.{coreSanitized}",
            };

            foreach (var key in progressiveCandidates)
            {
                if (ResolverText.TryGetTranslation(key, out progressiveTranslation))
                {
                    localizedName = progressiveTranslation;
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
