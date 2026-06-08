using System;
using System.Text;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class ResolverText
    {
        internal static string ToKeySegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var previousWasSeparator = false;

            foreach (var character in value.Trim().Normalize(NormalizationForm.FormKC))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                    continue;
                }

                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        internal static string ToCompactKeySegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);

            foreach (var character in value.Trim().Normalize(NormalizationForm.FormKC))
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        internal static string ToPascalAssetSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"[\s']+", string.Empty);
        }

        internal static bool TryGetTranslation(string key, out string localized)
        {
            localized = string.Empty;

            if (!ModEntry.Translation.ContainsKey(key))
            {
                return false;
            }

            localized = ModEntry.Translation.Get(key).ToString();
            return !string.IsNullOrWhiteSpace(localized);
        }

        internal static bool TryLoadGameString(string key, out string localized)
        {
            localized = string.Empty;

            try
            {
                localized = Game1.content.LoadString(key);
                return !string.IsNullOrWhiteSpace(localized) && !localized.Equals(key);
            }
            catch
            {
                return false;
            }
        }
    }
}
