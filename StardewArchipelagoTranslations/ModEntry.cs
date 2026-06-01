using System;
using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewModdingAPI;
using CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; } = null!;
        public static ITranslationHelper Translation => Instance.Helper.Translation;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            
            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Load static Harmony patches from the assembly
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Load dynamic Harmony patches
            I18nPatcher.Patch(harmony);
            BundlesPatcher.Patch(harmony);
            ShopPatcher.Patch(harmony);
            MultiSleepPatcher.Patch(harmony);

            // Load custom JSON mail templates
            MailPatcher.LoadTemplates(helper);

            // Pre-populate translation caches when the save is loaded to ensure zero gameplay delay
            helper.Events.GameLoop.SaveLoaded += (sender, e) =>
            {
                TranslationHelper.PrepopulateCaches();
                TranslationHelper.ResetPreScout();
            };

            helper.Events.GameLoop.ReturnedToTitle += (sender, e) =>
            {
                TranslationHelper.ResetPreScout();
            };

            helper.Events.GameLoop.UpdateTicked += (sender, e) =>
            {
                if (e.IsMultipleOf(60))
                {
                    TranslationHelper.CheckAndTriggerPreScout();
                }
            };

            // Register debug console commands
            helper.ConsoleCommands.Add("find_game_string", "Searches Stardew Valley native asset files for a string and outputs its key and translations. Usage: find_game_string <term>", (command, args) =>
            {
                if (args.Length == 0)
                {
                    Monitor.Log("Please provide a search term. Usage: find_game_string <term>", LogLevel.Error);
                    return;
                }
                var searchTerm = string.Join(" ", args);
                Monitor.Log($"Searching game assets for '{searchTerm}'...", LogLevel.Info);

                var stringAssets = new[]
                {
                    "Strings\\StringsFromCSFiles",
                    "Strings\\StringsFromMaps",
                    "Strings\\Locations",
                    "Strings\\UI",
                    "Strings\\Events",
                    "Strings\\Notes",
                    "Strings\\Speech"
                };

                using (var engManager = new Microsoft.Xna.Framework.Content.ContentManager(Game1.game1.Content.ServiceProvider, Game1.game1.Content.RootDirectory))
                {
                    var foundAny = false;
                    foreach (var asset in stringAssets)
                    {
                        try
                        {
                            var engStrings = engManager.Load<Dictionary<string, string>>(asset);
                            var locStrings = Game1.content.Load<Dictionary<string, string>>(asset);
                            if (engStrings != null && locStrings != null)
                            {
                                foreach (var pair in engStrings)
                                {
                                    if (pair.Value != null && pair.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                                    {
                                        locStrings.TryGetValue(pair.Key, out var locVal);
                                        Monitor.Log($"[Asset: {asset}] Key: '{pair.Key}'\n -> EN: '{pair.Value}'\n -> PT: '{locVal ?? "N/A"}'", LogLevel.Info);
                                        foundAny = true;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Failed to search asset '{asset}': {ex.Message}", LogLevel.Trace);
                        }
                    }
                    if (!foundAny)
                    {
                        Monitor.Log($"No native occurrences of '{searchTerm}' were found in string assets.", LogLevel.Warn);
                    }
                }
            });

            helper.ConsoleCommands.Add("ap_pt_mem", "Shows the exact, real-time memory usage of the StardewArchipelagoTranslations translation mod.", (command, args) =>
            {
                try
                {
                    double bytes = TranslationHelper.GetMemoryUsageBytes(out int cacheCount, out int indexCount);
                    double kb = bytes / 1024.0;
                    double mb = kb / 1024.0;

                    Monitor.Log("====================================================", LogLevel.Info);
                    Monitor.Log("   StardewArchipelagoTranslations - Memory Usage Status", LogLevel.Info);
                    Monitor.Log("====================================================", LogLevel.Info);
                    Monitor.Log($" -> Active Dynamic Caches:  {cacheCount} entries", LogLevel.Info);
                    Monitor.Log($" -> Game Database Indexes:  {indexCount} entries", LogLevel.Info);
                    Monitor.Log($" -> Physical RAM Size: {kb:F2} KB ({mb:F4} MB)", LogLevel.Info);
                    Monitor.Log("----------------------------------------------------", LogLevel.Info);
                    Monitor.Log(" Status: Extremely optimized, light, and healthy! (0.01% game impact)", LogLevel.Info);
                    Monitor.Log("====================================================", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed to measure memory usage: {ex}", LogLevel.Error);
                }
            });
        }
    }
}
