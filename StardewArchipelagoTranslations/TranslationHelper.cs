using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using StardewValley;
using StardewValley.TokenizableStrings;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static class TranslationHelper
    {
        private static Dictionary<string, string>? _vanillaBundlesMap;
        private static readonly object _bundlesLock = new object();
        private static LocalizedContentManager.LanguageCode _vanillaBundlesLang = (LocalizedContentManager.LanguageCode)(-1);

        private static Dictionary<string, string>? _vanillaObjectsNameMap;
        private static readonly object _objectsLock = new object();
        private static Dictionary<string, string>? _vanillaPowersNameMap;
        private static readonly object _powersLock = new object();
        private static Dictionary<string, int>? _questCodeByEnglishTitle;
        private static readonly object _questsLock = new object();

        // Maps Archipelago item names for TV channels to the game's own content string keys
        // so the game's native localization is used instead of hardcoded translations.
        // OrdinalIgnoreCase means case variants (e.g. "of" vs "Of") already match the same key.
        private static readonly Dictionary<string, string> _tvChannelGameStringKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Weather Report",                           "Strings\\StringsFromCSFiles:TV.cs.13105" },
            { "Fortune Teller",                           "Strings\\StringsFromCSFiles:TV.cs.13107" },
            { "Livin' Off The Land",                      "Strings\\StringsFromCSFiles:TV.cs.13111" },
            { "The Queen of Sauce",                       "Strings\\StringsFromCSFiles:TV.cs.13114" },
            { "The Queen of Sauce (Re-run)",              "Strings\\StringsFromCSFiles:TV.cs.13117" },
            { "Fishing Information Broadcasting Service", "Strings\\StringsFromCSFiles:TV_Fishing_Channel" },
        };


        // Performance Caches to prevent repeated reflection, DataLoader, and lookups during active gameplay / loads
        private static readonly Dictionary<string, string> _resolvedItemNamesCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _resolvedLocationNamesCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _translatedDescriptionsCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _localizedBundleNamesCache = new(StringComparer.OrdinalIgnoreCase);
        private static LocalizedContentManager.LanguageCode _cachesLang = (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _cachesLock = new object();

        // Cache for slow reflection field info
        private static System.Reflection.FieldInfo? _itemManagerField;
        private static bool _hasPreScouted = false;
        private static readonly object _preScoutLock = new object();

        public static void ResetPreScout()
        {
            lock (_preScoutLock)
            {
                _hasPreScouted = false;
            }
        }

        public static void CheckAndTriggerPreScout()
        {
            if (_hasPreScouted) return;
            if (!Context.IsWorldReady || Game1.player == null) return;

            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance == null) return;

                var archipelagoField = typeof(StardewArchipelago.ModEntry).GetField("_archipelago", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var locationCheckerField = typeof(StardewArchipelago.ModEntry).GetField("_locationChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (archipelagoField == null || locationCheckerField == null) return;

                var archipelago = archipelagoField.GetValue(saInstance) as StardewArchipelago.Archipelago.StardewArchipelagoClient;
                var locationChecker = locationCheckerField.GetValue(saInstance) as StardewArchipelago.Locations.StardewLocationChecker;

                if (archipelago != null && locationChecker != null && archipelago.IsConnected)
                {
                    lock (_preScoutLock)
                    {
                        if (_hasPreScouted) return;
                        _hasPreScouted = true;
                    }

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            ModEntry.Instance.Monitor.Log("Starting background pre-scouting of all missing locations to prevent shop open lag...", LogLevel.Info);
                            var missingLocations = locationChecker.GetAllMissingLocationNames()?.ToList();
                            if (missingLocations != null && missingLocations.Any())
                            {
                                ModEntry.Instance.Monitor.Log($"Found {missingLocations.Count} missing locations to scout. Querying Archipelago server in bulk...", LogLevel.Info);

                                // Call the bulk scout method on the StardewArchipelagoClient
                                var scouted = archipelago.ScoutStardewLocations(missingLocations);

                                ModEntry.Instance.Monitor.Log($"Successfully pre-scouted {scouted?.Count ?? 0} locations in the background! Shop menus will open instantly.", LogLevel.Info);
                            }
                            else
                            {
                                ModEntry.Instance.Monitor.Log("No missing locations found to scout.", LogLevel.Info);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log($"Error during background pre-scouting: {ex}", LogLevel.Error);
                            lock (_preScoutLock)
                            {
                                _hasPreScouted = false;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error checking pre-scout: {ex.Message}", LogLevel.Trace);
            }
        }

        private static void EnsureCachesValid()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_cachesLang != currentLang)
            {
                lock (_cachesLock)
                {
                    if (_cachesLang != currentLang)
                    {
                        _resolvedItemNamesCache.Clear();
                        _resolvedLocationNamesCache.Clear();
                        _translatedDescriptionsCache.Clear();
                        _localizedBundleNamesCache.Clear();
                        _vanillaObjectsNameMap = null;
                        _vanillaPowersNameMap = null;
                        _cachesLang = currentLang;
                        ModEntry.Instance.Monitor.Log($"Cleared translation caches due to language change to {currentLang}", LogLevel.Trace);
                    }
                }
            }
        }

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
                ModEntry.Instance.Monitor.Log($"Error resolving item name for '{englishItemName}': {ex}", LogLevel.Error);
            }

            lock (_cachesLock)
            {
                _resolvedItemNamesCache[englishItemName] = result;
            }

            // Output SMAPI log showing precisely the translation input and output at Trace level to prevent console flooding
            ModEntry.Instance.Monitor.Log($"GetLocalizedItemName (Cache Miss): Input = '{englishItemName}', Output = '{result}'", LogLevel.Trace);
            return result;
        }

        private static string ResolveLocalizedItemName(string englishItemName)
        {
            // -1. Strip "Power: " prefix sent by Archipelago as ItemMessagePart (e.g. "Power: Animal Catalogue")
            //     Resolve the bare power name and format it with hints.power_item_format.
            const string powerPrefix = "Power: ";
            if (englishItemName.StartsWith(powerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var barePowerName = englishItemName.Substring(powerPrefix.Length).Trim();
                var localizedPower = ResolveLocalizedItemName(barePowerName);
                return ModEntry.Translation.Get("hints.power_item_format", new { power = localizedPower }).ToString();
            }

            // 0. Check if it's a TV channel item — load from game strings and wrap as "TV: {{name}}"
            if (_tvChannelGameStringKeys.TryGetValue(englishItemName, out var tvContentPath))
            {
                try
                {
                    var tvName = Game1.content.LoadString(tvContentPath);
                    if (!string.IsNullOrWhiteSpace(tvName))
                    {
                        return ModEntry.Translation.Get("hints.tv_channel_format", new { name = tvName }).ToString();
                    }
                }
                catch { }
            }

            var sanitized = englishItemName.Replace(" ", "_").Replace("'", "").ToLower();

            // 1.5. Parse dynamic skill-level strings sent by Archipelago, e.g. "Level 2 Farming".
            //      We localize the skill name and then rebuild with a localized format template.
            var levelSkillMatch = System.Text.RegularExpressions.Regex.Match(
                englishItemName,
                @"^Level\s+(\d+)\s+([A-Za-z][A-Za-z\s']*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (levelSkillMatch.Success)
            {
                var levelNumber = levelSkillMatch.Groups[1].Value.Trim();
                var rawSkillName = levelSkillMatch.Groups[2].Value.Trim();
                var skillLevelKey = $"level.{rawSkillName.Replace(" ", "_").Replace("'", "").ToLower()}_level";

                if (ModEntry.Translation.ContainsKey(skillLevelKey))
                {
                    var localizedSkillLevel = ModEntry.Translation.Get(skillLevelKey).ToString();
                    var localizedSkillName = localizedSkillLevel;

                    // Normalize common patterns from our i18n entries:
                    // EN: "Farming Level" -> "Farming"
                    // PT: "Nível de Cultivo" -> "Cultivo"
                    if (localizedSkillName.EndsWith(" Level", StringComparison.OrdinalIgnoreCase))
                    {
                        localizedSkillName = localizedSkillName.Substring(0, localizedSkillName.Length - " Level".Length).Trim();
                    }
                    if (localizedSkillName.StartsWith("Nível de ", StringComparison.OrdinalIgnoreCase))
                    {
                        localizedSkillName = localizedSkillName.Substring("Nível de ".Length).Trim();
                    }

                    return ModEntry.Translation.Get("hints.skill_level_format", new { level = levelNumber, skill = localizedSkillName }).ToString();
                }
            }

            // 1.6. Parse Traveling Merchant items that can appear in chat/mail item text.
            if (TryResolveTravelingMerchantItemName(englishItemName, out var localizedTravelingMerchantItem))
            {
                return localizedTravelingMerchantItem;
            }

            // 1. Check if it's a custom trap first (highly useful for Archipelago traps)
            var trapKey = $"trap.{sanitized}";
            if (ModEntry.Translation.ContainsKey(trapKey))
            {
                return ModEntry.Translation.Get(trapKey).ToString();
            }

            // 2. Check if it's a progressive skill level (highly useful for skill level checks)
            var levelKey = $"level.{sanitized}";
            if (ModEntry.Translation.ContainsKey(levelKey))
            {
                return ModEntry.Translation.Get(levelKey).ToString();
            }

            // 3. Check if we have a custom translation in i18n files first (highly useful for virtual items)
            var progressiveKey = $"progressive.{sanitized}";
            if (ModEntry.Translation.ContainsKey(progressiveKey))
            {
                return ModEntry.Translation.Get(progressiveKey).ToString();
            }

            if (TryResolveProgressiveItemName(englishItemName, out var localizedProgressiveItem))
            {
                return localizedProgressiveItem;
            }

            var itemKey = $"item.{sanitized}";
            if (ModEntry.Translation.ContainsKey(itemKey))
            {
                return ModEntry.Translation.Get(itemKey).ToString();
            }

            // 3. Check if it's an Archipelago custom power
            var sanitizedPowerName = englishItemName.Replace(" ", "_").Replace("'", "").ToLower();
            var powerKey = $"power.{sanitizedPowerName}.name";
            if (ModEntry.Translation.ContainsKey(powerKey))
            {
                return ModEntry.Translation.Get(powerKey).ToString();
            }

            // 3.5. Check if it's a native base game power/book (new in Stardew Valley 1.6)
            try
            {
                if (_vanillaPowersNameMap == null)
                {
                    lock (_powersLock)
                    {
                        if (_vanillaPowersNameMap == null)
                        {
                            _vanillaPowersNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var powers = Game1.content.Load<Dictionary<string, StardewValley.GameData.Powers.PowersData>>("Data\\Powers");
                            if (powers != null)
                            {
                                foreach (var pair in powers)
                                {
                                    if (pair.Value != null && !string.IsNullOrWhiteSpace(pair.Value.DisplayName))
                                    {
                                        _vanillaPowersNameMap[pair.Key] = pair.Value.DisplayName;
                                        // Also map without spaces/punctuation
                                        var cleanPairKey = pair.Key.Replace(" ", "").Replace("'", "").Replace("_", "");
                                        _vanillaPowersNameMap[cleanPairKey] = pair.Value.DisplayName;
                                    }
                                }
                            }
                        }
                    }
                }

                var cleanKey = englishItemName.Replace(" ", "").Replace("'", "").Replace("_", "");
                if (_vanillaPowersNameMap.TryGetValue(cleanKey, out var rawDisplayName) || _vanillaPowersNameMap.TryGetValue(englishItemName, out rawDisplayName))
                {
                    var localized = TokenParser.ParseText(rawDisplayName);
                    if (!string.IsNullOrWhiteSpace(localized)) return localized;
                }
            }
            catch { }

            // 3.7. Check if it's a native base game object/book in Data\Objects (new/existing in Stardew Valley 1.6)
            try
            {
                if (_vanillaObjectsNameMap == null)
                {
                    lock (_objectsLock)
                    {
                        if (_vanillaObjectsNameMap == null)
                        {
                            _vanillaObjectsNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var objects = Game1.content.Load<Dictionary<string, StardewValley.GameData.Objects.ObjectData>>("Data\\Objects");
                            if (objects != null)
                            {
                                // 1. Map native object Names (like pair.Value.Name) to DisplayNames
                                foreach (var pair in objects)
                                {
                                    if (pair.Value != null && !string.IsNullOrWhiteSpace(pair.Value.Name) && !string.IsNullOrWhiteSpace(pair.Value.DisplayName))
                                    {
                                        _vanillaObjectsNameMap[pair.Value.Name] = pair.Value.DisplayName;
                                    }
                                }

                                // 2. Map ObjectIds constants dynamically to their localized DisplayNames via Reflection!
                                try
                                {
                                    var objectIdsType = typeof(StardewArchipelago.Constants.Vanilla.ObjectIds);
                                    var fields = objectIdsType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                                    foreach (var field in fields)
                                    {
                                        if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                                        {
                                            var objectId = field.GetValue(null) as string;
                                            if (!string.IsNullOrWhiteSpace(objectId))
                                            {
                                                if (objects.TryGetValue(objectId, out var objData) && !string.IsNullOrWhiteSpace(objData.DisplayName))
                                                {
                                                    var cleanFieldName = field.Name.Replace("_", "").Replace("'", "").ToLower();
                                                    _vanillaObjectsNameMap[cleanFieldName] = objData.DisplayName;

                                                    var fieldNameWithUnderscores = field.Name.Replace("'", "").ToLower();
                                                    _vanillaObjectsNameMap[fieldNameWithUnderscores] = objData.DisplayName;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModEntry.Instance.Monitor.Log($"Error building ObjectIds reflection map: {ex.Message}", LogLevel.Trace);
                                }
                            }
                        }
                    }
                }

                // Try lookups in order of specificity
                var cleanLookupKey = englishItemName.Replace(" ", "").Replace("'", "").Replace("_", "");
                var underscoreLookupKey = englishItemName.Replace(" ", "_").Replace("'", "");

                if (_vanillaObjectsNameMap.TryGetValue(englishItemName, out var rawDisplayName) ||
                    _vanillaObjectsNameMap.TryGetValue(underscoreLookupKey, out rawDisplayName) ||
                    _vanillaObjectsNameMap.TryGetValue(cleanLookupKey, out rawDisplayName))
                {
                    var localized = TokenParser.ParseText(rawDisplayName);
                    if (!string.IsNullOrWhiteSpace(localized)) return localized;
                }
            }
            catch { }

            // 3. Check if it's a Season
            var seasonNumber = Utility.getSeasonNumber(englishItemName);
            if (seasonNumber != -1)
            {
                return Utility.getSeasonNameFromNumber(seasonNumber);
            }

            // 4. Check if it's a Tool (using Game1.toolData / ItemRegistry)
            // Try both with spaces and without (e.g. "Training Rod" -> "(T)Training Rod" OR "(T)TrainingRod")
            var toolNameNoSpaces = englishItemName.Replace(" ", "");
            var toolData = ItemRegistry.GetData($"(T){englishItemName}")
                        ?? ItemRegistry.GetData($"(T){toolNameNoSpaces}");
            if (toolData != null && !string.IsNullOrWhiteSpace(toolData.DisplayName))
            {
                return toolData.DisplayName;
            }

            // 5. Check if it's a Building (using Game1.buildingData)
            if (Game1.buildingData != null && Game1.buildingData.TryGetValue(englishItemName, out var buildingData))
            {
                if (buildingData != null && !string.IsNullOrWhiteSpace(buildingData.Name))
                {
                    var localizedName = TokenParser.ParseText(buildingData.Name);
                    if (!string.IsNullOrWhiteSpace(localizedName))
                    {
                        return localizedName;
                    }
                }
            }

            // 6. Check direct Strings\Buildings database fallback (extremely useful if buildingData isn't loaded yet)
            var cleanBuildingName = englishItemName.Replace(" ", "").Replace("'", "");
            try
            {
                var buildingStringKey = $"Strings\\Buildings:{cleanBuildingName}_Name";
                var localizedBuildingName = Game1.content.LoadString(buildingStringKey);
                if (!string.IsNullOrWhiteSpace(localizedBuildingName) && localizedBuildingName != buildingStringKey)
                {
                    return localizedBuildingName;
                }
            }
            catch { }

            // 6. Query StardewItemManager from StardewArchipelago in real-time!
            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance != null)
                {
                    _itemManagerField ??= typeof(StardewArchipelago.ModEntry).GetField("_stardewItemManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (_itemManagerField != null)
                    {
                        var stardewItemManager = _itemManagerField.GetValue(saInstance) as StardewArchipelago.Stardew.StardewItemManager;
                        if (stardewItemManager != null)
                        {
                            if (stardewItemManager.ItemExists(englishItemName))
                            {
                                var stardewItem = stardewItemManager.GetItemByName(englishItemName);
                                if (stardewItem != null)
                                {
                                    var qualifiedId = stardewItem.GetQualifiedId();
                                    var data = ItemRegistry.GetData(qualifiedId);
                                    if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
                                    {
                                        return data.DisplayName;
                                    }
                                }
                            }

                            // 7. Check if it's a Recipe via StardewItemManager
                            var recipeCleanName = englishItemName;
                            var hasRecipeSuffix = false;
                            if (recipeCleanName.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase))
                            {
                                recipeCleanName = recipeCleanName.Substring(0, recipeCleanName.Length - 7).Trim();
                                hasRecipeSuffix = true;
                            }
                            else if (recipeCleanName.EndsWith(" recipe", StringComparison.OrdinalIgnoreCase))
                            {
                                recipeCleanName = recipeCleanName.Substring(0, recipeCleanName.Length - 7).Trim();
                                hasRecipeSuffix = true;
                            }

                            var stardewRecipe = stardewItemManager.GetRecipeByName(recipeCleanName, false);
                            if (stardewRecipe != null)
                            {
                                var isCooking = stardewRecipe is StardewArchipelago.Stardew.StardewCookingRecipe;
                                if (RecipeExists(recipeCleanName, isCooking))
                                {
                                    var nativeRecipe = new CraftingRecipe(recipeCleanName, isCooking);
                                    if (!string.IsNullOrWhiteSpace(nativeRecipe.DisplayName))
                                    {
                                        return hasRecipeSuffix
                                            ? ModEntry.Translation.Get("hints.recipe_format", new { name = nativeRecipe.DisplayName }).ToString()
                                            : nativeRecipe.DisplayName;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error querying StardewItemManager for '{englishItemName}': {ex.Message}", LogLevel.Trace);
            }

            // 8. Fallback to generic recipe parsing if StardewItemManager was not initialized
            var genericRecipeName = englishItemName;
            var genericRecipeSuffix = false;
            if (genericRecipeName.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase))
            {
                genericRecipeName = genericRecipeName.Substring(0, genericRecipeName.Length - 7).Trim();
                genericRecipeSuffix = true;
            }
            else if (genericRecipeName.EndsWith(" recipe", StringComparison.OrdinalIgnoreCase))
            {
                genericRecipeName = genericRecipeName.Substring(0, genericRecipeName.Length - 7).Trim();
                genericRecipeSuffix = true;
            }

            if (RecipeExists(genericRecipeName, false))
            {
                try
                {
                    var recipeObj = new CraftingRecipe(genericRecipeName, false);
                    if (!string.IsNullOrWhiteSpace(recipeObj.DisplayName))
                    {
                        return genericRecipeSuffix
                            ? ModEntry.Translation.Get("hints.recipe_format", new { name = recipeObj.DisplayName }).ToString()
                            : recipeObj.DisplayName;
                    }
                }
                catch { }
            }

            if (RecipeExists(genericRecipeName, true))
            {
                try
                {
                    var recipeObj = new CraftingRecipe(genericRecipeName, true);
                    if (!string.IsNullOrWhiteSpace(recipeObj.DisplayName))
                    {
                        return genericRecipeSuffix
                            ? ModEntry.Translation.Get("hints.recipe_format", new { name = recipeObj.DisplayName }).ToString()
                            : recipeObj.DisplayName;
                    }
                }
                catch { }
            }

            return englishItemName;
        }

        private static bool TryResolveProgressiveItemName(string englishItemName, out string localizedItemName)
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
            var coreTokens = tokens.Where(t => !t.Equals("Progressive", StringComparison.OrdinalIgnoreCase)).ToArray();
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
                $"progressive.{coreSanitized}"
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
            localizedItemName = ModEntry.Translation.Get("hints.progressive_format", new { name = localizedCore }).ToString();
            return true;
        }

        private static bool TryResolveTravelingMerchantItemName(string englishItemName, out string localizedItemName)
        {
            localizedItemName = englishItemName;

            const string dayPrefix = "Traveling Merchant: ";
            if (englishItemName.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishItemName.Substring(dayPrefix.Length).Trim();
                var localizedDay = GetLocalizedWeekday(englishDay);
                localizedItemName = ModEntry.Translation.Get("item.traveling_merchant_day_format", new { day = localizedDay }).ToString();
                return true;
            }

            if (englishItemName.Equals("Traveling Merchant Stock Size", StringComparison.OrdinalIgnoreCase))
            {
                localizedItemName = ModEntry.Translation.Get("item.traveling_merchant_stock_size").ToString();
                return true;
            }

            if (englishItemName.Equals("Traveling Merchant Discount", StringComparison.OrdinalIgnoreCase))
            {
                localizedItemName = ModEntry.Translation.Get("item.traveling_merchant_discount").ToString();
                return true;
            }

            if (englishItemName.Equals("Traveling Merchant Metal Detector", StringComparison.OrdinalIgnoreCase))
            {
                localizedItemName = ModEntry.Translation.Get("item.traveling_merchant_metal_detector").ToString();
                return true;
            }

            return false;
        }

        private static bool RecipeExists(string recipeName, bool isCooking)
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

        private static string GetVanillaBundleTranslation(string englishBundleName)
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_vanillaBundlesMap == null || _vanillaBundlesLang != currentLang)
            {
                lock (_bundlesLock)
                {
                    if (_vanillaBundlesMap == null || _vanillaBundlesLang != currentLang)
                    {
                        _vanillaBundlesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _vanillaBundlesLang = currentLang;
                        try
                        {
                            using (var contentManager = new LocalizedContentManager(Game1.game1.Content.ServiceProvider, Game1.game1.Content.RootDirectory))
                            {
                                var bundlesData = contentManager.Load<Dictionary<string, string>>("Data\\Bundles");
                                if (bundlesData != null)
                                {
                                    foreach (var pair in bundlesData)
                                    {
                                        var value = pair.Value;
                                        if (!string.IsNullOrWhiteSpace(value))
                                        {
                                            var parts = value.Split('/');
                                            if (parts.Length > 5)
                                            {
                                                var englishName = parts[0]?.Trim();
                                                var locName = parts[5]?.Trim();
                                                if (!string.IsNullOrWhiteSpace(englishName) && !string.IsNullOrWhiteSpace(locName))
                                                {
                                                    _vanillaBundlesMap[englishName] = locName;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log($"Failed to load vanilla bundles from clean content manager: {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }

            if (_vanillaBundlesMap.TryGetValue(englishBundleName, out var localizedName))
            {
                return localizedName;
            }

            return englishBundleName;
        }

        public static string GetLocalizedBundleName(string englishBundleName)
        {
            if (string.IsNullOrWhiteSpace(englishBundleName)) return englishBundleName;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_localizedBundleNamesCache.TryGetValue(englishBundleName, out var cached))
                {
                    return cached;
                }
            }

            string result;

            // 1. Check if we have a custom translation in i18n files first (highly useful for custom Archipelago bundles)
            var sanitized = englishBundleName.Replace(" ", "").Replace("'", "").ToLower();
            var key = $"bundle.{sanitized}";
            if (ModEntry.Translation.ContainsKey(key))
            {
                result = ModEntry.Translation.Get(key).ToString();
            }
            else
            {
                // 2. Query our dynamic vanilla bundles map
                var vanillaResolved = GetVanillaBundleTranslation(englishBundleName);
                if (vanillaResolved != englishBundleName)
                {
                    result = vanillaResolved;
                }
                else
                {
                    // 3. Query our dynamic building/tool/item lookup
                    var resolved = ResolveLocalizedItemName(englishBundleName);
                    result = resolved;
                }
            }

            lock (_cachesLock)
            {
                _localizedBundleNamesCache[englishBundleName] = result;
            }

            return result;
        }

        public static string TranslateHintMessage(string stardewFullMessage)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    stardewFullMessage,
                    @"^\[Hint\]:\s*(.*?)\s+'s\s+(.*?)\s+is at\s+(.*?)\s+in\s+(.*?)\s+'s\s+World\s*\.?\s*(?:\((.*?)\))?\s*\.?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
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

                    return ModEntry.Translation.Get("hints.format", new { item, receiver, location, finder, status }).ToString();
                }
            }
            catch (Exception)
            {
            }

            return stardewFullMessage;
        }

        public static string GetLocalizedBuildingName(string englishBuildingName)
        {
            if (Game1.buildingData != null && Game1.buildingData.TryGetValue(englishBuildingName, out var buildingData))
            {
                if (buildingData != null && !string.IsNullOrWhiteSpace(buildingData.Name))
                {
                    var localizedName = TokenParser.ParseText(buildingData.Name);
                    if (!string.IsNullOrWhiteSpace(localizedName))
                    {
                        return localizedName;
                    }
                }
            }
            return englishBuildingName;
        }

        public static void TranslateRewardName(ref string rewardName)
        {
            if (string.IsNullOrWhiteSpace(rewardName)) return;

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

            var scamMatch = System.Text.RegularExpressions.Regex.Match(rewardName, @"Guaranteed return of ([\d,]+) to ([\d,]+)");
            if (scamMatch.Success)
            {
                rewardName = ModEntry.Translation.Get("bundle.reward_scam_format", new { min = scamMatch.Groups[1].Value, max = scamMatch.Groups[2].Value }).ToString();
                return;
            }

            var cleanRewardName = rewardName;
            if (cleanRewardName.StartsWith("Reward:", StringComparison.OrdinalIgnoreCase))
            {
                cleanRewardName = cleanRewardName.Substring(7).Trim();
            }

            var match = System.Text.RegularExpressions.Regex.Match(cleanRewardName, @"^(.*)'s (.*)$");
            if (match.Success)
            {
                var player = match.Groups[1].Value;
                var item = match.Groups[2].Value;
                var localizedItem = GetLocalizedItemName(item);
                rewardName = ModEntry.Translation.Get("bundle.reward_format", new { player, item = localizedItem }).ToString();
            }
        }

        public static string GetLocalizedLocationName(string englishLocationName)
        {
            if (string.IsNullOrWhiteSpace(englishLocationName)) return englishLocationName;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedLocationNamesCache.TryGetValue(englishLocationName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishLocationName;

            // 1. Check if we have a custom translation in i18n files first (highly useful for specific custom locations)
            var sanitized = englishLocationName.Replace(" ", "_").Replace("'", "").ToLower();
            var key = $"location.{sanitized}";
            if (TryResolveLocalizedQuestLocation(englishLocationName, out var localizedQuestLocation))
            {
                result = localizedQuestLocation;
            }
            else if (TryResolveTravelingMerchantLocation(englishLocationName, out var localizedTravelingMerchantLocation))
            {
                result = localizedTravelingMerchantLocation;
            }
            else if (ModEntry.Translation.ContainsKey(key))
            {
                result = ModEntry.Translation.Get(key).ToString();
            }
            // 1.5. Parse TV channel locations dynamically, e.g. "The Queen of Sauce" -> "TV: A Rainha do Molho"
            else if (_tvChannelGameStringKeys.TryGetValue(englishLocationName, out var tvContentPath))
            {
                try
                {
                    var tvName = Game1.content.LoadString(tvContentPath);
                    if (!string.IsNullOrWhiteSpace(tvName))
                    {
                        result = ModEntry.Translation.Get("hints.tv_channel_format", new { name = tvName }).ToString();
                    }
                }
                catch
                {
                }
            }
            // 2. Parse tool upgrades format dynamically, e.g. "Copper Axe Upgrade" -> "Melhoria: Machado de Cobre"
            else if (englishLocationName.EndsWith(" Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                var cleanToolName = englishLocationName.Substring(0, englishLocationName.Length - 8).Trim();
                // Query tool data using registry (without spaces)
                var toolId = cleanToolName.Replace(" ", "");
                if (toolId.StartsWith("Iron", StringComparison.OrdinalIgnoreCase))
                {
                    toolId = "Steel" + toolId.Substring(4);
                }
                var toolData = ItemRegistry.GetData($"(T){toolId}");
                if (toolData != null && !string.IsNullOrWhiteSpace(toolData.DisplayName))
                {
                    result = ModEntry.Translation.Get("hints.upgrade_format", new { name = toolData.DisplayName }).ToString();
                }
            }
            // 2.5. Parse shop purchase format dynamically, e.g. "Purchase Training Rod" -> "Comprar: Vara de Treino"
            else if (englishLocationName.StartsWith("Purchase ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanItemName = englishLocationName.Substring(9).Trim();
                var localizedItem = ResolveLocalizedItemName(cleanItemName);
                result = ModEntry.Translation.Get("hints.purchase_format", new { name = localizedItem }).ToString();
            }
            // 3. Parse building blueprints format dynamically, e.g. "Coop Blueprint" -> "Projeto do Galinheiro"
            else if (englishLocationName.EndsWith(" Blueprint", StringComparison.OrdinalIgnoreCase))
            {
                var cleanBuildingName = englishLocationName.Substring(0, englishLocationName.Length - 10).Trim();
                var localizedBuilding = ResolveLocalizedItemName(cleanBuildingName);
                // Always wrap blueprint locations to avoid leaving the "Blueprint" suffix in English.
                result = ModEntry.Translation.Get("hints.blueprint_format", new { name = localizedBuilding }).ToString();
            }
            // 3.5. Parse Community Center rooms dynamically, e.g. "Complete Pantry" -> "Completar: Despensa"
            else if (englishLocationName.StartsWith("Complete ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanAreaName = englishLocationName.Substring(9).Trim();
                var localizedArea = GetLocalizedAreaName(cleanAreaName);
                result = ModEntry.Translation.Get("hints.complete_area_format", new { name = localizedArea }).ToString();
            }
            // 3.7. Detect bundle location names, e.g. "Orchard Bundle" -> "Pacote: Jardim"
            // Wraps with hints.bundle_format so players know it's a bundle, not a generic location.
            else if (englishLocationName.EndsWith(" Bundle", StringComparison.OrdinalIgnoreCase))
            {
                string bundleBaseName = englishLocationName;

                var localizedBundle = GetLocalizedBundleName(englishLocationName);
                if (localizedBundle != englishLocationName)
                {
                    bundleBaseName = localizedBundle;
                }
                else
                {
                    // Try just the prefix (e.g. "Orchard" -> "Pomar")
                    var bundlePrefix = englishLocationName.Substring(0, englishLocationName.Length - 7).Trim();
                    var localizedPrefix = GetLocalizedBundleName(bundlePrefix);
                    if (localizedPrefix != bundlePrefix)
                        bundleBaseName = localizedPrefix;
                }

                // Always wrap with the bundle format so it reads "Pacote: Jardim" instead of just "Jardim"
                result = ModEntry.Translation.Get("hints.bundle_format", new { name = bundleBaseName }).ToString();
            }
            else
            {
                // 4. Fallback to generic item display name resolution
                var resolved = ResolveLocalizedItemName(englishLocationName);
                result = resolved;
            }

            lock (_cachesLock)
            {
                _resolvedLocationNamesCache[englishLocationName] = result;
            }

            return result;
        }

        private static bool TryResolveTravelingMerchantLocation(string englishLocationName, out string localizedLocation)
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
                localizedLocation = ModEntry.Translation.Get("location.traveling_merchant_item_format", new { day = localizedDay, item = itemLabel }).ToString();
                return true;
            }

            const string dayPrefix = "Traveling Merchant: ";
            if (englishLocationName.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var englishDay = englishLocationName.Substring(dayPrefix.Length).Trim();
                var localizedDay = GetLocalizedWeekday(englishDay);
                localizedLocation = ModEntry.Translation.Get("location.traveling_merchant_day_format", new { day = localizedDay }).ToString();
                return true;
            }

            if (englishLocationName.Equals("Traveling Merchant Stock Size", StringComparison.OrdinalIgnoreCase))
            {
                localizedLocation = ModEntry.Translation.Get("location.traveling_merchant_stock_size").ToString();
                return true;
            }

            if (englishLocationName.Equals("Traveling Merchant Discount", StringComparison.OrdinalIgnoreCase))
            {
                localizedLocation = ModEntry.Translation.Get("location.traveling_merchant_discount").ToString();
                return true;
            }

            if (englishLocationName.Equals("Traveling Merchant Metal Detector", StringComparison.OrdinalIgnoreCase))
            {
                localizedLocation = ModEntry.Translation.Get("location.traveling_merchant_metal_detector").ToString();
                return true;
            }

            return false;
        }

        private static string GetLocalizedWeekday(string englishWeekday)
        {
            var weekdayKey = englishWeekday.Trim().ToLowerInvariant() switch
            {
                "sunday" => "location.weekday.sunday",
                "monday" => "location.weekday.monday",
                "tuesday" => "location.weekday.tuesday",
                "wednesday" => "location.weekday.wednesday",
                "thursday" => "location.weekday.thursday",
                "friday" => "location.weekday.friday",
                "saturday" => "location.weekday.saturday",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(weekdayKey) && ModEntry.Translation.ContainsKey(weekdayKey))
            {
                return ModEntry.Translation.Get(weekdayKey).ToString();
            }

            return englishWeekday;
        }

        private static bool TryResolveLocalizedQuestLocation(string englishLocationName, out string localizedLocation)
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

            if (_questCodeByEnglishTitle == null || !_questCodeByEnglishTitle.TryGetValue(englishQuestTitle, out var locationCode))
            {
                return false;
            }

            // Quest location codes in the AP table are 7175xx where xx maps to quest id.
            var questId = locationCode - 717500;
            if (questId <= 0)
            {
                return false;
            }

            try
            {
                var quest = StardewValley.Quests.Quest.getQuestFromId(questId.ToString());
                if (quest != null && !string.IsNullOrWhiteSpace(quest.questTitle))
                {
                    localizedLocation = $"Quest: {quest.questTitle}";
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Quest localization fallback failed for '{englishLocationName}': {ex.Message}", LogLevel.Trace);
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

                _questCodeByEnglishTitle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    var apAssemblyPath = typeof(StardewArchipelago.ModEntry).Assembly.Location;
                    var apBaseDir = Path.GetDirectoryName(apAssemblyPath);
                    if (string.IsNullOrWhiteSpace(apBaseDir))
                    {
                        return false;
                    }

                    var locationTablePath = Path.Combine(apBaseDir, "IdTables", "stardew_valley_location_table.json");
                    if (!File.Exists(locationTablePath))
                    {
                        ModEntry.Instance.Monitor.Log($"Quest lookup table not found: {locationTablePath}", LogLevel.Trace);
                        return false;
                    }

                    using var stream = File.OpenRead(locationTablePath);
                    using var document = JsonDocument.Parse(stream);
                    if (!document.RootElement.TryGetProperty("locations", out var locationsElement) || locationsElement.ValueKind != JsonValueKind.Object)
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

                        if (!locationEntry.Value.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.Number)
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
                    ModEntry.Instance.Monitor.Log($"Failed to build quest lookup map: {ex.Message}", LogLevel.Trace);
                    return false;
                }
            }
        }

        public static string TranslateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return description;

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
            if (string.IsNullOrWhiteSpace(line)) return line;

            var cleanLine = line.Trim();

            // 1. Match "[Player]'s Power: [PowerName]"
            var powerMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(.*)'s\s+Power:\s+(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (powerMatch.Success)
            {
                var player = powerMatch.Groups[1].Value.Trim();
                var power = powerMatch.Groups[2].Value.Trim();
                var localizedPower = GetLocalizedItemName(power);
                return ModEntry.Translation.Get("hints.player_power_format", new { player, power = localizedPower }).ToString();
            }

            // 2. Match "[Player]'s [ItemName]"
            var itemMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(.*)'s\s+(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (itemMatch.Success)
            {
                var player = itemMatch.Groups[1].Value.Trim();
                var item = itemMatch.Groups[2].Value.Trim();
                var localizedItem = GetLocalizedItemName(item);
                return ModEntry.Translation.Get("hints.player_item_format", new { player, item = localizedItem }).ToString();
            }

            // 3. Match "[ItemName] para [Player]" (Portuguese format generated by the library)
            var ptMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(.*?)\s+para\s+(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                    return ModEntry.Translation.Get("hints.player_power_format", new { player, power = localizedPower }).ToString();
                }
                else if (item.StartsWith(powerPrefixPt, StringComparison.OrdinalIgnoreCase))
                {
                    var power = item.Substring(powerPrefixPt.Length).Trim();
                    var localizedPower = GetLocalizedItemName(power);
                    return ModEntry.Translation.Get("hints.player_power_format", new { player, power = localizedPower }).ToString();
                }

                var localizedItem = GetLocalizedItemName(item);
                return ModEntry.Translation.Get("hints.player_item_format", new { player, item = localizedItem }).ToString();
            }

            // 4. Match "[Quantity] [ItemName]" (Requirements list, e.g. "250 Stone" -> "250 Pedra")
            var reqMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(\d+)\s+(.*)$");
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

            return line;
        }

        public static void DumpMapToFile()
        {
            var outputPath = System.IO.Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "mapped_items.txt");
            var lines = new List<string>();

            // Reflection debug print for Translation class
            try
            {
                lines.Add("=== Translation Properties ===");
                foreach (var prop in typeof(Translation).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                {
                    lines.Add($"Prop: {prop.Name} ({prop.PropertyType})");
                }
                foreach (var field in typeof(Translation).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                {
                    lines.Add($"Field: {field.Name} ({field.FieldType})");
                }
                lines.Add("=============================");
            }
            catch (Exception ex)
            {
                lines.Add($"Error inspecting Translation: {ex.Message}");
            }

            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance != null)
                {
                    var itemManagerField = typeof(StardewArchipelago.ModEntry).GetField("_stardewItemManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (itemManagerField != null)
                    {
                        var stardewItemManager = itemManagerField.GetValue(saInstance) as StardewArchipelago.Stardew.StardewItemManager;
                        if (stardewItemManager != null)
                        {
                            var allItems = stardewItemManager.GetAllItems();
                            foreach (var item in allItems.OrderBy(i => i.Name))
                            {
                                var localizedName = ResolveLocalizedItemName(item.Name);
                                var langCode = LocalizedContentManager.CurrentLanguageCode.ToString();
                                var category = Patcher.MailPatcher.GetItemCategory(item.Name);
                                lines.Add($"EN: '{item.Name}' -> {langCode}: '{localizedName}' [ID: {item.GetQualifiedId()}] [Category: {category}]");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add($"Error dumping map: {ex.Message}");
            }

            System.IO.File.WriteAllLines(outputPath, lines);
            ModEntry.Instance.Monitor.Log($"Dumped dynamic translations to: {outputPath}", LogLevel.Info);
        }

        private static bool IsPrime(int number)
        {
            if (number <= 1) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;
            var boundary = (int)Math.Floor(Math.Sqrt(number));
            for (int i = 3; i <= boundary; i += 2)
            {
                if (number % i == 0) return false;
            }
            return true;
        }

        private static int GetNextPrime(int min)
        {
            for (int i = min; i < int.MaxValue; i++)
            {
                if (IsPrime(i)) return i;
            }
            return min;
        }

        public static double GetMemoryUsageBytes(out int cacheEntries, out int indexEntries)
        {
            long bytes = 0;
            cacheEntries = 0;
            indexEntries = 0;

            long CalculateExactStringSize(string? s)
            {
                if (s == null) return 0;
                // Syncblock (8) + Type Handle (8) + Length (4) + Null terminator (2) + chars * 2, padded to 8-byte boundary
                long byteSize = 8 + 8 + 4 + 2 + (s.Length * 2);
                long padding = (8 - (byteSize % 8)) % 8;
                return byteSize + padding;
            }

            long CalculateExactDictSize(Dictionary<string, string>? dict, ref int count)
            {
                if (dict == null) return 0;
                long size = 0;
                lock (dict)
                {
                    int itemsCount = dict.Count;
                    count += itemsCount;

                    // Dictionary object overhead (approx 48 bytes in .NET 6 64-bit)
                    size += 48;

                    // Buckets array size (int[]): array object overhead (24 bytes) + bucket count * 4
                    int buckets = GetNextPrime(itemsCount);
                    size += 24 + (buckets * 4);

                    // Entries array size: array object overhead (24 bytes) + capacity * Entry struct size (24 bytes)
                    size += 24 + (itemsCount * 24);

                    foreach (var pair in dict)
                    {
                        size += CalculateExactStringSize(pair.Key);
                        size += CalculateExactStringSize(pair.Value);
                    }
                }
                return size;
            }

            int cachesCount = 0;
            lock (_cachesLock)
            {
                bytes += CalculateExactDictSize(_resolvedItemNamesCache, ref cachesCount);
                bytes += CalculateExactDictSize(_resolvedLocationNamesCache, ref cachesCount);
                bytes += CalculateExactDictSize(_translatedDescriptionsCache, ref cachesCount);
                bytes += CalculateExactDictSize(_localizedBundleNamesCache, ref cachesCount);
            }
            cacheEntries = cachesCount;

            int indexesCount = 0;
            bytes += CalculateExactDictSize(_vanillaBundlesMap, ref indexesCount);
            bytes += CalculateExactDictSize(_vanillaObjectsNameMap, ref indexesCount);
            bytes += CalculateExactDictSize(_vanillaPowersNameMap, ref indexesCount);
            indexEntries = indexesCount;

            // Loaded SMAPI assembly metadata, loaded translations array reference, and DLL overhead
            bytes += 85 * 1024;

            return bytes;
        }

        public static void PrepopulateCaches()
        {
            try
            {
                EnsureCachesValid();

                // Pre-populate all items from StardewItemManager to prevent any in-game hover delays
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance != null)
                {
                    _itemManagerField ??= typeof(StardewArchipelago.ModEntry).GetField("_stardewItemManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (_itemManagerField != null)
                    {
                        var stardewItemManager = _itemManagerField.GetValue(saInstance) as StardewArchipelago.Stardew.StardewItemManager;
                        if (stardewItemManager != null)
                        {
                            var allItems = stardewItemManager.GetAllItems();
                            if (allItems != null)
                            {
                                int count = 0;
                                lock (_cachesLock)
                                {
                                    foreach (var item in allItems)
                                    {
                                        if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                                        {
                                            if (!_resolvedItemNamesCache.ContainsKey(item.Name))
                                            {
                                                _resolvedItemNamesCache[item.Name] = ResolveLocalizedItemName(item.Name);
                                                count++;
                                            }
                                        }
                                    }
                                }

                                // Pre-cache tool upgrades
                                var tools = new[] { "Axe", "Pickaxe", "Hoe", "Watering Can", "Trash Can" };
                                var tiers = new[] { "Copper", "Iron", "Gold", "Iridium" };
                                foreach (var tool in tools)
                                {
                                    foreach (var tier in tiers)
                                    {
                                        GetLocalizedLocationName($"{tier} {tool} Upgrade");
                                    }
                                }

                                // Pre-cache Community Center complete locations
                                var ccLocs = new[] { "Complete Pantry", "Complete Crafts Room", "Complete Fish Tank", "Complete Boiler Room", "Complete Vault", "Complete Bulletin Board", "The Missing Bundle", "Complete Community Center" };
                                foreach (var ccLoc in ccLocs)
                                {
                                    GetLocalizedLocationName(ccLoc);
                                }

                                // Pre-cache Robin blueprints
                                if (Game1.buildingData != null)
                                {
                                    foreach (var buildingKey in Game1.buildingData.Keys)
                                    {
                                        GetLocalizedLocationName($"{buildingKey} Blueprint");
                                    }
                                }
                                GetLocalizedLocationName("Kitchen Blueprint");

                                ModEntry.Instance.Monitor.Log($"Pre-populated {count} items and all shop locations in translation cache!", LogLevel.Info);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error pre-populating translation caches: {ex.Message}", LogLevel.Trace);
            }
        }

        private static string GetLocalizedAreaName(string englishAreaName)
        {
            if (string.IsNullOrWhiteSpace(englishAreaName)) return englishAreaName;

            var clean = englishAreaName.Replace(" ", "");

            // 1. Try strings from Locations (covers Pantry, CraftsRoom, FishTank, BoilerRoom, Vault, BulletinBoard)
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

            // 1.5. Try strings from UI as fallback
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

            // 2. Try generic location strings (covers Community Center, etc.)
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

            // 3. Check if we have a direct custom item/location translation in JSON files as fallback
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
    }
}
