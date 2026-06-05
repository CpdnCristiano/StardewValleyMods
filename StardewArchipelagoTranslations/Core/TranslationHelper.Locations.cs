using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedLocationName(string englishLocationName)
        {
            if (string.IsNullOrWhiteSpace(englishLocationName))
                return englishLocationName;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedLocationNamesCache.TryGetValue(englishLocationName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishLocationName;

            foreach (var resolver in _locationResolvers)
            {
                if (
                    resolver.TryResolve(englishLocationName, out var localized)
                    && localized != null
                )
                {
                    result = localized;
                    break;
                }
            }

            lock (_cachesLock)
            {
                _resolvedLocationNamesCache[englishLocationName] = result;
            }

            return result;
        }

        internal static bool TryResolveTravelingMerchantLocation(
            string englishLocationName,
            out string localizedLocation
        )
        {
            localizedLocation = englishLocationName;

            var travelingMerchantItemMatch = Regex.Match(
                englishLocationName,
                @"^Traveling Merchant\s+(.+?)\s+Item\s+(.+)$",
                RegexOptions.IgnoreCase
            );
            if (travelingMerchantItemMatch.Success)
            {
                var englishDay = travelingMerchantItemMatch.Groups[1].Value.Trim();
                var itemLabel = travelingMerchantItemMatch.Groups[2].Value.Trim();
                var localizedDay = GetLocalizedWeekday(englishDay);
                var template = ModEntry
                    .Translation.Get("location.traveling_merchant_item_format")
                    .ToString();
                localizedLocation = template
                    .Replace("{{day}}", localizedDay)
                    .Replace("{{item}}", itemLabel)
                    .Replace("{day}", localizedDay)
                    .Replace("{item}", itemLabel);
                return true;
            }

            const string dayPrefix = "Traveling Merchant: ";
            if (englishLocationName.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishLocationName.Substring(dayPrefix.Length).Trim();
                var localizedDay = GetLocalizedWeekday(englishDay);
                var template = ModEntry
                    .Translation.Get("location.traveling_merchant_day_format")
                    .ToString();
                localizedLocation = template
                    .Replace("{{day}}", localizedDay)
                    .Replace("{day}", localizedDay);
                return true;
            }

            if (
                englishLocationName.Equals(
                    "Traveling Merchant Stock Size",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedLocation = ModEntry
                    .Translation.Get("location.traveling_merchant_stock_size")
                    .ToString();
                return true;
            }

            if (
                englishLocationName.Equals(
                    "Traveling Merchant Discount",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedLocation = ModEntry
                    .Translation.Get("location.traveling_merchant_discount")
                    .ToString();
                return true;
            }

            if (
                englishLocationName.Equals(
                    "Traveling Merchant Metal Detector",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                localizedLocation = ModEntry
                    .Translation.Get("location.traveling_merchant_metal_detector")
                    .ToString();
                return true;
            }

            return false;
        }

        internal static string GetLocalizedWeekday(string englishWeekday)
        {
            try
            {
                var weekdayKey = englishWeekday.Trim().ToLowerInvariant() switch
                {
                    "sunday" => "Strings\\StringsFromCSFiles:Utility.cs.11068",
                    "monday" => "Strings\\StringsFromCSFiles:Utility.cs.11069",
                    "tuesday" => "Strings\\StringsFromCSFiles:Utility.cs.11070",
                    "wednesday" => "Strings\\StringsFromCSFiles:Utility.cs.11071",
                    "thursday" => "Strings\\StringsFromCSFiles:Utility.cs.11072",
                    "friday" => "Strings\\StringsFromCSFiles:Utility.cs.11073",
                    "saturday" => "Strings\\StringsFromCSFiles:Utility.cs.11074",
                    _ => string.Empty,
                };

                if (!string.IsNullOrEmpty(weekdayKey))
                {
                    return Game1.content.LoadString(weekdayKey);
                }
            }
            catch (Exception)
            {
                // Fallback to English if loading fails
            }

            return englishWeekday;
        }

        private static readonly Dictionary<string, string> _questEnglishTitleToRealId = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Strange Note", "29" },
            { "Fresh Fruit", "31" },
            { "Aquatic Research", "32" },
            { "A Soldier's Star", "33" },
            { "Mayor's Need", "34" },
            { "Wanted: Lobster", "35" },
            { "Pam Needs Juice", "36" },
            { "Fish Casserole", "37" },
            { "Catch A Squid", "38" },
            { "Fish Stew", "39" },
            { "Pierre's Notice", "40" },
            { "Clint's Attempt", "41" },
            { "A Favor For Clint", "42" },
            { "Staff Of Power", "43" },
            { "Granny's Gift", "44" },
            { "Exotic Spirits", "45" },
            { "Catch a Lingcod", "46" },
            { "The Pirate's Wife", "130" },
            { "Dark Talisman", "128" },
            { "Goblin Problem", "129" },
            { "Magic Ink", "131" },
            { "The Giant Stump", "134" },
            { "Rat Problem", "27" },
        };

        internal static bool TryResolveLocalizedQuestLocation(
            string englishLocationName,
            out string localizedLocation
        )
        {
            localizedLocation = englishLocationName;

            const string questPrefix = "Quest: ";
            if (!englishLocationName.StartsWith(questPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var englishQuestTitle = englishLocationName.Substring(questPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(englishQuestTitle))
            {
                return false;
            }

            if (!TryBuildQuestCodeMap())
            {
                return false;
            }

            if (
                _questCodeByEnglishTitle == null
                || !_questCodeByEnglishTitle.TryGetValue(englishQuestTitle, out var locationCode)
            )
            {
                return false;
            }

            string questIdStr;
            if (_questEnglishTitleToRealId.TryGetValue(englishQuestTitle, out var realId))
            {
                questIdStr = realId;
            }
            else
            {
                var questId = locationCode - 717500;
                if (questId <= 0)
                {
                    return false;
                }
                questIdStr = questId.ToString();
            }

            try
            {
                var quest = StardewValley.Quests.Quest.getQuestFromId(questIdStr);
                if (quest != null && !string.IsNullOrWhiteSpace(quest.questTitle))
                {
                    localizedLocation = $"Quest: {quest.questTitle}";
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Quest localization fallback failed for '{englishLocationName}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            return false;
        }

        private static bool TryBuildQuestCodeMap()
        {
            if (_questCodeByEnglishTitle != null)
            {
                return _questCodeByEnglishTitle.Count > 0;
            }

            lock (_questsLock)
            {
                if (_questCodeByEnglishTitle != null)
                {
                    return _questCodeByEnglishTitle.Count > 0;
                }

                _questCodeByEnglishTitle = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
                );

                try
                {
                    var apAssemblyPath = typeof(StardewArchipelago.ModEntry).Assembly.Location;
                    var apBaseDir = Path.GetDirectoryName(apAssemblyPath);
                    if (string.IsNullOrWhiteSpace(apBaseDir))
                    {
                        return false;
                    }

                    var locationTablePath = Path.Combine(
                        apBaseDir,
                        "IdTables",
                        "stardew_valley_location_table.json"
                    );
                    if (!File.Exists(locationTablePath))
                    {
                        ModEntry.Instance.Monitor.Log(
                            $"Quest lookup table not found: {locationTablePath}",
                            LogLevel.Trace
                        );
                        return false;
                    }

                    using var stream = File.OpenRead(locationTablePath);
                    using var document = JsonDocument.Parse(stream);
                    if (
                        !document.RootElement.TryGetProperty("locations", out var locationsElement)
                        || locationsElement.ValueKind != JsonValueKind.Object
                    )
                    {
                        return false;
                    }

                    foreach (var locationEntry in locationsElement.EnumerateObject())
                    {
                        var locationName = locationEntry.Name;
                        if (!locationName.StartsWith("Quest: ", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (
                            !locationEntry.Value.TryGetProperty("code", out var codeElement)
                            || codeElement.ValueKind != JsonValueKind.Number
                        )
                        {
                            continue;
                        }

                        var englishQuestTitle = locationName.Substring("Quest: ".Length).Trim();
                        if (string.IsNullOrWhiteSpace(englishQuestTitle))
                        {
                            continue;
                        }

                        var locationCode = codeElement.GetInt32();
                        _questCodeByEnglishTitle[englishQuestTitle] = locationCode;
                    }

                    return _questCodeByEnglishTitle.Count > 0;
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log(
                        $"Failed to build quest lookup map: {ex.Message}",
                        LogLevel.Trace
                    );
                    return false;
                }
            }
        }

        internal static string GetLocalizedAreaName(string englishAreaName)
        {
            if (string.IsNullOrWhiteSpace(englishAreaName))
                return englishAreaName;

            var clean = englishAreaName.Replace(" ", "");

            try
            {
                var key = $"Strings\\Locations:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\UI:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\Locations:{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            var sanitized = englishAreaName.Replace(" ", "_").Replace("'", "").ToLower();
            var itemKey = $"item.{sanitized}";
            if (ModEntry.Translation.ContainsKey(itemKey))
            {
                return ModEntry.Translation.Get(itemKey).ToString();
            }
            var locKey = $"location.{sanitized}";
            if (ModEntry.Translation.ContainsKey(locKey))
            {
                return ModEntry.Translation.Get(locKey).ToString();
            }

            return englishAreaName;
        }

        public static string GetLocalizedBuildingName(string englishBuildingName)
        {
            if (string.IsNullOrWhiteSpace(englishBuildingName))
                return englishBuildingName;

            foreach (var resolver in _buildingResolvers)
            {
                if (
                    resolver.TryResolve(englishBuildingName, out var localized)
                    && localized != null
                )
                {
                    return localized;
                }
            }

            return englishBuildingName;
        }
    }
}
