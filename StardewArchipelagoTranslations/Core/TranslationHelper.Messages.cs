using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string TranslateHintMessage(string stardewFullMessage)
        {
            try
            {
                var match = Regex.Match(
                    stardewFullMessage,
                    @"^\[Hint\]:\s*(.*?)\s+'s\s+(.*?)\s+is at\s+(.*?)\s+in\s+(.*?)\s+'s\s+World\s*\.?\s*(?:\((.*?)\))?\s*\.?$",
                    RegexOptions.IgnoreCase
                );

                if (match.Success)
                {
                    var receiver = match.Groups[1].Value.Trim();
                    var item = match.Groups[2].Value.Trim();
                    var location = match.Groups[3].Value.Trim();
                    var finder = match.Groups[4].Value.Trim();
                    var statusRaw = match.Groups[5].Value.Trim();

                    item = GetLocalizedItemName(item);
                    location = GetLocalizedLocationName(location);

                    var status = "";
                    if (!string.IsNullOrWhiteSpace(statusRaw))
                    {
                        if (statusRaw.Equals("Found", StringComparison.OrdinalIgnoreCase))
                        {
                            status = ModEntry.Translation.Get("hints.found").ToString();
                        }
                        else
                        {
                            status = " (" + statusRaw + ")";
                        }
                    }

                    return ModEntry
                        .Translation.Get(
                            "hints.format",
                            new
                            {
                                item,
                                receiver,
                                location,
                                finder,
                                status,
                            }
                        )
                        .ToString();
                }
            }
            catch (Exception) { }

            return stardewFullMessage;
        }

        public static string TranslateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return description;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_translatedDescriptionsCache.TryGetValue(description, out var cached))
                {
                    return cached;
                }
            }

            var lines = description.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var translatedLines = new List<string>();
            foreach (var line in lines)
            {
                translatedLines.Add(TranslateDescriptionLine(line));
            }

            var result = string.Join(Environment.NewLine, translatedLines);

            lock (_cachesLock)
            {
                _translatedDescriptionsCache[description] = result;
            }

            return result;
        }

        private static string TranslateDescriptionLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return line;

            var cleanLine = line.Trim();

            var powerMatch = Regex.Match(
                cleanLine,
                @"^(.*)'s\s+Power:\s+(.*)$",
                RegexOptions.IgnoreCase
            );
            if (powerMatch.Success)
            {
                var player = powerMatch.Groups[1].Value.Trim();
                var power = powerMatch.Groups[2].Value.Trim();
                var localizedPower = GetLocalizedItemName(power);
                return ModEntry
                    .Translation.Get(
                        "hints.player_power_format",
                        new { player, power = localizedPower }
                    )
                    .ToString();
            }

            var itemMatch = Regex.Match(cleanLine, @"^(.*)'s\s+(.*)$", RegexOptions.IgnoreCase);
            if (itemMatch.Success)
            {
                var player = itemMatch.Groups[1].Value.Trim();
                var item = itemMatch.Groups[2].Value.Trim();
                var localizedItem = GetLocalizedItemName(item);
                return ModEntry
                    .Translation.Get(
                        "hints.player_item_format",
                        new { player, item = localizedItem }
                    )
                    .ToString();
            }

            var ptMatch = Regex.Match(cleanLine, @"^(.*?)\s+para\s+(.*)$", RegexOptions.IgnoreCase);
            if (ptMatch.Success)
            {
                var item = ptMatch.Groups[1].Value.Trim();
                var player = ptMatch.Groups[2].Value.Trim();

                var powerPrefixEn = "Power: ";
                var powerPrefixPt = "Poder: ";
                if (item.StartsWith(powerPrefixEn, StringComparison.OrdinalIgnoreCase))
                {
                    var power = item.Substring(powerPrefixEn.Length).Trim();
                    var localizedPower = GetLocalizedItemName(power);
                    return ModEntry
                        .Translation.Get(
                            "hints.player_power_format",
                            new { player, power = localizedPower }
                        )
                        .ToString();
                }
                else if (item.StartsWith(powerPrefixPt, StringComparison.OrdinalIgnoreCase))
                {
                    var power = item.Substring(powerPrefixPt.Length).Trim();
                    var localizedPower = GetLocalizedItemName(power);
                    return ModEntry
                        .Translation.Get(
                            "hints.player_power_format",
                            new { player, power = localizedPower }
                        )
                        .ToString();
                }

                var localizedItem = GetLocalizedItemName(item);
                return ModEntry
                    .Translation.Get(
                        "hints.player_item_format",
                        new { player, item = localizedItem }
                    )
                    .ToString();
            }

            var reqMatch = Regex.Match(cleanLine, @"^^(\d+)\s+(.*)$");
            if (reqMatch.Success)
            {
                var quantity = reqMatch.Groups[1].Value;
                var itemName = reqMatch.Groups[2].Value.Trim();
                var localizedItemName = GetLocalizedItemName(itemName);
                if (localizedItemName != itemName)
                {
                    return $"{quantity} {localizedItemName}";
                }
            }

            // Try to translate as location name first
            var localizedLoc = GetLocalizedLocationName(cleanLine);
            if (localizedLoc != cleanLine)
            {
                return localizedLoc;
            }

            // Try to translate as item name
            var translatedItem = GetLocalizedItemName(cleanLine);
            if (translatedItem != cleanLine)
            {
                return translatedItem;
            }

            return line;
        }

        public static void TranslateRewardName(ref string rewardName)
        {
            if (string.IsNullOrWhiteSpace(rewardName))
                return;

            if (rewardName.Equals("No Reward Remaining", StringComparison.OrdinalIgnoreCase))
            {
                rewardName = ModEntry.Translation.Get("bundle.no_reward_remaining").ToString();
                return;
            }

            if (rewardName.Equals("Unknown Reward", StringComparison.OrdinalIgnoreCase))
            {
                rewardName = ModEntry.Translation.Get("bundle.reward_unknown").ToString();
                return;
            }

            var scamMatch = Regex.Match(rewardName, @"Guaranteed return of ([\d,]+) to ([\d,]+)");
            if (scamMatch.Success)
            {
                rewardName = ModEntry
                    .Translation.Get(
                        "bundle.reward_scam_format",
                        new { min = scamMatch.Groups[1].Value, max = scamMatch.Groups[2].Value }
                    )
                    .ToString();
                return;
            }

            var cleanRewardName = rewardName;
            if (cleanRewardName.StartsWith("Reward:", StringComparison.OrdinalIgnoreCase))
            {
                cleanRewardName = cleanRewardName.Substring(7).Trim();
            }

            var match = Regex.Match(cleanRewardName, @"^(.*)'s (.*)$");
            if (match.Success)
            {
                var player = match.Groups[1].Value;
                var item = match.Groups[2].Value;
                var localizedItem = GetLocalizedItemName(item);
                rewardName = ModEntry
                    .Translation.Get("bundle.reward_format", new { player, item = localizedItem })
                    .ToString();
            }
        }
    }
}
