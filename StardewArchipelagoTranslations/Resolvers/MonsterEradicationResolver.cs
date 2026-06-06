using System;
using System.Collections.Generic;
using System.Globalization;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class MonsterEradicationResolver : ILocationResolver
    {
        private const string Prefix = "Monster Eradication: ";
        
        // Cache to avoid reloading game assets and repeating lookup logic
        private static readonly Dictionary<string, string> _resolvedMonstersCache = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string>? _rawMonstersData;
        private static readonly object _lock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (string.IsNullOrWhiteSpace(englishName))
                return false;

            if (!englishName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var remaining = englishName.Substring(Prefix.Length).Trim();
            if (string.IsNullOrEmpty(remaining))
                return false;

            // Parse the monster name and potential count (e.g. "200 Green Slimes" or "Serpent")
            ParseMonsterNameAndCount(remaining, out var numberStr, out var monsterName);

            // Translate the monster/category name dynamically
            var localizedMonster = GetLocalizedMonsterName(monsterName);

            // Format the final localized string
            var translationHelper = ModEntry.Translation;
            if (!string.IsNullOrEmpty(numberStr))
            {
                if (translationHelper.ContainsKey("monster_eradication.format_with_count"))
                {
                    localizedName = translationHelper.Get(
                        "monster_eradication.format_with_count",
                        new { count = numberStr, monster = localizedMonster }
                    ).ToString();
                }
                else
                {
                    localizedName = $"{Prefix}{numberStr} {localizedMonster}";
                }
            }
            else
            {
                if (translationHelper.ContainsKey("monster_eradication.format"))
                {
                    localizedName = translationHelper.Get(
                        "monster_eradication.format",
                        new { monster = localizedMonster }
                    ).ToString();
                }
                else
                {
                    localizedName = $"{Prefix}{localizedMonster}";
                }
            }

            return true;
        }

        private static void ParseMonsterNameAndCount(string input, out string numberStr, out string monsterName)
        {
            numberStr = string.Empty;
            monsterName = input;

            var parts = input.Split(' ', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out _))
            {
                numberStr = parts[0];
                monsterName = parts[1];
            }
        }

        private static string GetLocalizedMonsterName(string monsterName)
        {
            lock (_lock)
            {
                if (_resolvedMonstersCache.TryGetValue(monsterName, out var cached))
                {
                    return cached;
                }

                var localized = ResolveMonsterName(monsterName);
                _resolvedMonstersCache[monsterName] = localized;
                return localized;
            }
        }

        private static string ResolveMonsterName(string monsterName)
        {
            // Normalize names that differ between Archipelago and game internal keys
            var lookupName = monsterName;
            if (string.Equals(monsterName, "Dust Sprite", StringComparison.OrdinalIgnoreCase))
            {
                lookupName = "Dust Spirit";
            }
            else if (string.Equals(monsterName, "Dust Sprites", StringComparison.OrdinalIgnoreCase))
            {
                lookupName = "Dust Spirits";
            }

            // A. Check SMAPI translation JSON helper (for user overrides)
            var sanitizedMonster = lookupName.Replace(" ", "_").Replace("'", "").ToLowerInvariant();
            var monsterKey = $"monster.{sanitizedMonster}";
            var translationHelper = ModEntry.Translation;

            if (translationHelper.ContainsKey(monsterKey))
            {
                return translationHelper.Get(monsterKey).ToString();
            }

            // B. Try to load display name from game's Data\Monsters asset (slash-separated string)
            try
            {
                _rawMonstersData ??= Game1.content.Load<Dictionary<string, string>>("Data\\Monsters");
                if (_rawMonstersData != null && _rawMonstersData.TryGetValue(lookupName, out var rawValue))
                {
                    if (!string.IsNullOrWhiteSpace(rawValue))
                    {
                        var fields = rawValue.Split('/');
                        if (fields.Length > 14)
                        {
                            var displayName = fields[14].Trim();
                            if (!string.IsNullOrWhiteSpace(displayName))
                            {
                                return displayName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error loading monster name from Data\\Monsters for '{lookupName}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            // C. Try Stardew Valley native category strings (e.g. Strings\Locations:AdventureGuild_KillList_Slimes)
            try
            {
                var cleanCategoryName = lookupName.Replace(" ", "");
                var nativeKey = $"Strings\\Locations:AdventureGuild_KillList_{cleanCategoryName}";
                var nativeString = Game1.content.LoadString(nativeKey);
                if (!string.IsNullOrWhiteSpace(nativeString) && nativeString != nativeKey)
                {
                    return Capitalize(nativeString.Trim());
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error loading native category string from AdventureGuild_KillList for '{lookupName}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            // D. Fallback to the original English name
            return monsterName;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
        }
    }
}
