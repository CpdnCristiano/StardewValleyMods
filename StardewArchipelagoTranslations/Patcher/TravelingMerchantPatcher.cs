#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8625
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using xTile.Dimensions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class TravelingMerchantPatcher
    {
        private static bool _patched;

        public static void Patch(Harmony harmony)
        {
            try
            {
                if (_patched)
                {
                    return;
                }

                var stardewAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");

                if (stardewAssembly == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[TravelingMerchantPatcher] StardewArchipelago assembly not found – skipping.",
                        LogLevel.Trace
                    );
                    return;
                }

                var targetType = stardewAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.TravelingMerchantInjections"
                );

                if (targetType == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[TravelingMerchantPatcher] TravelingMerchantInjections type not found – skipping.",
                        LogLevel.Trace
                    );
                    return;
                }

                // 1. Patch SetTravelingMerchantFlair
                var flairMethod = targetType.GetMethod(
                    "SetTravelingMerchantFlair",
                    BindingFlags.Public | BindingFlags.Static
                );
                if (flairMethod != null)
                {
                    harmony.Patch(
                        flairMethod,
                        postfix: new HarmonyMethod(
                            typeof(TravelingMerchantPatcher),
                            nameof(SetTravelingMerchantFlair_Postfix)
                        )
                    );
                }
                var setupFlairMethod = targetType.GetMethod(
                    "SetUpShopOwner_TravelingMerchantApFlair_Postfix",
                    BindingFlags.Public | BindingFlags.Static
                );
                if (setupFlairMethod != null)
                {
                    harmony.Patch(
                        setupFlairMethod,
                        postfix: new HarmonyMethod(
                            typeof(TravelingMerchantPatcher),
                            nameof(SetUpShopOwnerTravelingMerchantFlair_Postfix)
                        )
                    );
                }

                // 2. Patch BeachNightMarket.checkAction
                var nightMarketMethod = AccessTools.Method(
                    typeof(BeachNightMarket),
                    nameof(BeachNightMarket.checkAction)
                );
                if (nightMarketMethod != null)
                {
                    harmony.Patch(
                        nightMarketMethod,
                        prefix: new HarmonyMethod(
                            typeof(TravelingMerchantPatcher),
                            nameof(NightMarketCheckAction_Prefix)
                        )
                    );
                }

                // 3. Patch DesertFestival.checkAction
                var desertFestivalMethod = AccessTools.Method(
                    typeof(DesertFestival),
                    nameof(DesertFestival.checkAction)
                );
                if (desertFestivalMethod != null)
                {
                    harmony.Patch(
                        desertFestivalMethod,
                        prefix: new HarmonyMethod(
                            typeof(TravelingMerchantPatcher),
                            nameof(DesertFestivalCheckAction_Prefix)
                        )
                    );
                }

                ModEntry.Instance.Monitor.Log(
                    "[TravelingMerchantPatcher] Successfully patched TravelingMerchantInjections!",
                    LogLevel.Info
                );
                _patched = true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[TravelingMerchantPatcher] Failed to patch: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void SetUpShopOwnerTravelingMerchantFlair_Postfix(ShopMenu __instance)
        {
            TranslateTravelingMerchantFlair(__instance);
        }

        private static bool CallIsTravelingMerchantDay(int dayOfMonth)
        {
            try
            {
                var stardewAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                var targetType = stardewAssembly?.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.TravelingMerchantInjections"
                );
                var isTravelingDayMethod = targetType?.GetMethod(
                    "IsTravelingMerchantDay",
                    new[] { typeof(int) }
                );
                if (isTravelingDayMethod != null)
                {
                    return (bool)isTravelingDayMethod.Invoke(null, new object[] { dayOfMonth });
                }
            }
            catch { }
            return false;
        }

        public static bool NightMarketCheckAction_Prefix(
            BeachNightMarket __instance,
            Location tileLocation,
            xTile.Dimensions.Rectangle viewport,
            Farmer who,
            ref bool __result
        )
        {
            try
            {
                var tileIndex = __instance.getTileIndexAt(tileLocation, "Buildings");
                if (tileIndex != 399)
                {
                    return true;
                }

                if (Game1.timeOfDay < 1700)
                {
                    return true;
                }

                bool isTravelingMerchantDay = CallIsTravelingMerchantDay(Game1.dayOfMonth);
                if (!isTravelingMerchantDay)
                {
                    Game1.drawObjectDialogue(
                        ModEntry.Translation.Get("traveling_merchant.not_here").ToString()
                    );
                    __result = true;
                    return false; // Skip original and S.A.'s prefix
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in NightMarketCheckAction_Prefix: {ex}",
                    LogLevel.Error
                );
            }
            return true;
        }

        public static bool DesertFestivalCheckAction_Prefix(
            DesertFestival __instance,
            Location tileLocation,
            xTile.Dimensions.Rectangle viewport,
            Farmer who,
            ref bool __result
        )
        {
            try
            {
                var tileIndex = __instance.getTileIndexAt(tileLocation, "Buildings");
                if (tileIndex != 796 && tileIndex != 797)
                {
                    return true;
                }

                bool isTravelingMerchantDay = CallIsTravelingMerchantDay(Game1.dayOfMonth);
                if (!isTravelingMerchantDay)
                {
                    Game1.drawObjectDialogue(
                        ModEntry.Translation.Get("traveling_merchant.not_here").ToString()
                    );
                    __result = true;
                    return false; // Skip original and S.A.'s prefix
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DesertFestivalCheckAction_Prefix: {ex}",
                    LogLevel.Error
                );
            }
            return true;
        }

        public static void SetTravelingMerchantFlair_Postfix(ShopMenu travelingMerchantShopMenu)
        {
            TranslateTravelingMerchantFlair(travelingMerchantShopMenu);
        }

        private static void TranslateTravelingMerchantFlair(ShopMenu travelingMerchantShopMenu)
        {
            try
            {
                if (
                    travelingMerchantShopMenu == null
                    || string.IsNullOrEmpty(travelingMerchantShopMenu.potraitPersonDialogue)
                )
                    return;

                var text = travelingMerchantShopMenu.potraitPersonDialogue;

                var translatedFixedFlair = false;

                if (ContainsFlexibleText(text, "I'm sorry I don't have much to offer. Maybe do something else in the meantime?"))
                {
                    text = ReplaceFlexibleText(
                        text,
                        "I'm sorry I don't have much to offer. Maybe do something else in the meantime?",
                        ModEntry.Translation.Get("traveling_merchant.flair.low_stock").ToString()
                    );
                    translatedFixedFlair = true;
                }
                else if (ContainsFlexibleText(text, "I got lots of good stuff for sale!"))
                {
                    text = ReplaceFlexibleText(
                        text,
                        "I got lots of good stuff for sale!",
                        ModEntry.Translation.Get("traveling_merchant.flair.default").ToString()
                    );
                    translatedFixedFlair = true;
                }
                else if (ContainsFlexibleText(text, "Sweety, will you please buy something? I have a family to feed"))
                {
                    text = ReplaceFlexibleText(
                        text,
                        "Sweety, will you please buy something? I have a family to feed",
                        ModEntry.Translation.Get("traveling_merchant.flair.feed_family").ToString()
                    );
                    translatedFixedFlair = true;
                }

                var recMatch = Regex.Match(
                    text,
                    @"(.+?)\s+recommended\s+that\s+I\s+visit\s+(.+?)\s+on\s+(.+?)s\.",
                    RegexOptions.IgnoreCase
                );
                if (!translatedFixedFlair && recMatch.Success)
                {
                    var playerName = recMatch.Groups[1].Value;
                    var locationName = recMatch.Groups[2].Value;
                    var day = recMatch.Groups[3].Value;

                    var localizedLocation = locationName;
                    if (locationName.Equals("Cindersap Forest", StringComparison.OrdinalIgnoreCase))
                        localizedLocation = ModEntry
                            .Translation.Get("location.cindersap_forest")
                            .ToString();
                    else if (
                        locationName.Equals(
                            "the Beach Night Market",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        localizedLocation = ModEntry
                            .Translation.Get("location.beach_night_market")
                            .ToString();

                    var localizedDay = WeekdayResolver.GetLocalizedWeekday(day);

                    var localizedRec = ModEntry
                        .Translation.Get(
                            "traveling_merchant.flair.recommendation",
                            new
                            {
                                player = playerName,
                                location = localizedLocation,
                                day = localizedDay,
                            }
                        )
                        .ToString();

                    text = text.Replace(recMatch.Value, localizedRec);
                }

                var museumMatch = Regex.Match(
                    text,
                    @"I\s+found\s+something\s+interesting,\s+and\s+I\s+hear\s+(.+?)\s+wants\s+someone\s+to\s+donate\s+it\s+to\s+the\s+Museum\.\.\.",
                    RegexOptions.IgnoreCase
                );
                if (!translatedFixedFlair && museumMatch.Success)
                {
                    var playerName = museumMatch.Groups[1].Value;
                    var localizedMuseum = ModEntry
                        .Translation.Get(
                            "traveling_merchant.flair.museum_hint",
                            new { player = playerName }
                        )
                        .ToString();

                    text = text.Replace(museumMatch.Value, localizedMuseum);
                }

                var stockMatch = Regex.Match(text, @"Stock:\s*(\d+)%", RegexOptions.IgnoreCase);
                if (stockMatch.Success)
                {
                    var percent = stockMatch.Groups[1].Value;
                    var localizedStock = ModEntry
                        .Translation.Get(
                            "traveling_merchant.stock_format",
                            new { percent = percent }
                        )
                        .ToString();
                    text = text.Replace(stockMatch.Value, localizedStock);
                }

                travelingMerchantShopMenu.potraitPersonDialogue = Game1.parseText(
                    text,
                    Game1.dialogueFont,
                    304
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in SetTravelingMerchantFlair_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        private static bool ContainsFlexibleText(string text, string phrase)
        {
            return Regex.IsMatch(text, ToFlexiblePattern(phrase), RegexOptions.IgnoreCase);
        }

        private static string ReplaceFlexibleText(string text, string phrase, string replacement)
        {
            return Regex.Replace(text, ToFlexiblePattern(phrase), replacement, RegexOptions.IgnoreCase);
        }

        private static string ToFlexiblePattern(string phrase)
        {
            return Regex.Replace(Regex.Escape(phrase), @"\\\s+", @"\s+");
        }
    }
}
