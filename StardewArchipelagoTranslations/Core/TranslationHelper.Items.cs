using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedItemName(string englishItemName)
        {
            if (string.IsNullOrWhiteSpace(englishItemName))
            {
                return englishItemName;
            }

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedItemNamesCache.TryGetValue(englishItemName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishItemName;
            try
            {
                result = ResolveLocalizedItemName(englishItemName);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error resolving item name for '{englishItemName}': {ex}",
                    LogLevel.Error
                );
            }

            lock (_cachesLock)
            {
                _resolvedItemNamesCache[englishItemName] = result;
            }

            ModEntry.Instance.Monitor.Log(
                $"GetLocalizedItemName (Cache Miss): Input = '{englishItemName}', Output = '{result}'",
                LogLevel.Trace
            );
            return result;
        }

        private static string ResolveLocalizedItemName(string englishItemName)
        {
            var result = englishItemName;

            foreach (var resolver in _itemResolvers)
            {
                if (resolver.TryResolve(englishItemName, out var localized) && localized != null)
                {
                    result = localized;
                    break;
                }
            }

            return result;
        }

        internal static bool TryResolveProgressiveItemName(
            string englishItemName,
            out string localizedItemName
        )
        {
            localizedItemName = englishItemName;

            if (!Regex.IsMatch(englishItemName, @"\bProgressive\b", RegexOptions.IgnoreCase))
            {
                return false;
            }

            var normalized = Regex.Replace(englishItemName, @"\s+", " ").Trim();
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
                    localizedItemName = ModEntry.Translation.Get(key).ToString();
                    return true;
                }
            }

            var localizedCore = ResolveLocalizedItemName(coreName);
            localizedItemName = ModEntry
                .Translation.Get("hints.progressive_format", new { name = localizedCore })
                .ToString();
            return true;
        }

        internal static bool TryResolveTravelingMerchantItemName(
            string englishItemName,
            out string localizedItemName
        )
        {
            localizedItemName = englishItemName;

            const string dayPrefix = "Traveling Merchant: ";
            if (englishItemName.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishItemName.Substring(dayPrefix.Length).Trim();
                var localizedDay = GetLocalizedWeekday(englishDay);
                localizedItemName = ModEntry
                    .Translation.Get(
                        "item.traveling_merchant_day_format",
                        new { day = localizedDay }
                    )
                    .ToString();
                return true;
            }

            if (
                englishItemName.Equals(
                    "Traveling Merchant Stock Size",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedItemName = ModEntry
                    .Translation.Get("item.traveling_merchant_stock_size")
                    .ToString();
                return true;
            }

            if (
                englishItemName.Equals(
                    "Traveling Merchant Discount",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedItemName = ModEntry
                    .Translation.Get("item.traveling_merchant_discount")
                    .ToString();
                return true;
            }

            if (
                englishItemName.Equals(
                    "Traveling Merchant Metal Detector",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedItemName = ModEntry
                    .Translation.Get("item.traveling_merchant_metal_detector")
                    .ToString();
                return true;
            }

            return false;
        }

        internal static bool RecipeExists(string recipeName, bool isCooking)
        {
            try
            {
                var recipes = isCooking
                    ? DataLoader.CookingRecipes(Game1.content)
                    : DataLoader.CraftingRecipes(Game1.content);
                return recipes != null && recipes.ContainsKey(recipeName);
            }
            catch
            {
                return false;
            }
        }
    }
}
