using System;
using System.Text.RegularExpressions;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class RarecrowResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            var match = Regex.Match(
                englishName,
                @"^Rarecrow(?:\s+#?(?<number>\d+))?(?:\s*\((?<descriptor>[^)]+)\))?$",
                RegexOptions.IgnoreCase
            );
            if (!match.Success)
            {
                return false;
            }

            var rarecrowName = TranslationHelper.GetLocalizedItemName("Rarecrow");
            if (rarecrowName.Equals("Rarecrow", StringComparison.OrdinalIgnoreCase))
            {
                rarecrowName = "Rarecrow";
            }

            var number = match.Groups["number"].Value;
            var descriptor = ResolveDescriptor(match.Groups["descriptor"].Value.Trim());

            if (!string.IsNullOrWhiteSpace(number) && !string.IsNullOrWhiteSpace(descriptor))
            {
                localizedName = FormatNumberedWithDescriptor(rarecrowName, number, descriptor);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(number))
            {
                localizedName = FormatNumbered(rarecrowName, number);
                return true;
            }

            localizedName = rarecrowName;
            return true;
        }

        private static string ResolveDescriptor(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return string.Empty;
            }

            if (TranslationHelper.TryGetLocalizedGameString(descriptor, out var localized))
            {
                return localized;
            }

            var itemName = TranslationHelper.GetLocalizedItemName(descriptor);
            if (!itemName.Equals(descriptor, StringComparison.OrdinalIgnoreCase))
            {
                return itemName;
            }

            return descriptor;
        }

        private static string FormatNumbered(string rarecrowName, string number)
        {
            var fallback = $"{rarecrowName} #{number}";

            return FormatWithFallback(
                "rarecrow.numbered_format",
                fallback,
                new { name = rarecrowName, number }
            );
        }

        private static string FormatNumberedWithDescriptor(
            string rarecrowName,
            string number,
            string descriptor
        )
        {
            var fallback = $"{rarecrowName} #{number} ({descriptor})";

            return FormatWithFallback(
                "rarecrow.numbered_with_descriptor_format",
                fallback,
                new
                {
                    name = rarecrowName,
                    number,
                    descriptor,
                }
            );
        }

        private static string FormatWithFallback(string key, string fallback, object tokens)
        {
            try
            {
                var translation = ModEntry.Translation.Get(key, tokens);
                if (translation.HasValue())
                {
                    var localized = translation.ToString();
                    if (!string.IsNullOrWhiteSpace(localized))
                    {
                        return localized;
                    }
                }
            }
            catch { }

            return fallback;
        }
    }
}
