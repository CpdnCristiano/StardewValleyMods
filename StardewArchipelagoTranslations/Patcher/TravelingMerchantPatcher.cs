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
        public static void Patch(Harmony harmony)
        {
            try
            {
                var stardewAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");

                if (stardewAssembly == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[TravelingMerchantPatcher] StardewArchipelago assembly not found – skipping.",
                        LogLevel.Warn
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
                        LogLevel.Warn
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
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[TravelingMerchantPatcher] Failed to patch: {ex}",
                    LogLevel.Error
                );
            }
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
            try
            {
                if (
                    travelingMerchantShopMenu == null
                    || string.IsNullOrEmpty(travelingMerchantShopMenu.potraitPersonDialogue)
                )
                    return;

                var text = travelingMerchantShopMenu.potraitPersonDialogue;

                // 1. Lower stock
                if (
                    text.Contains(
                        "I'm sorry I don't have much to offer. Maybe do something else in the meantime?"
                    )
                )
                {
                    text = text.Replace(
                        "I'm sorry I don't have much to offer. Maybe do something else in the meantime?",
                        ModEntry.Translation.Get("traveling_merchant.flair.low_stock").ToString()
                    );
                }
                // 2. Default
                else if (text.Contains("I got lots of good stuff for sale!"))
                {
                    text = text.Replace(
                        "I got lots of good stuff for sale!",
                        ModEntry.Translation.Get("traveling_merchant.flair.default").ToString()
                    );
                }
                // 3. Feed family
                else if (
                    text.Contains("Sweety, will you please buy something? I have a family to feed")
                )
                {
                    text = text.Replace(
                        "Sweety, will you please buy something? I have a family to feed",
                        ModEntry.Translation.Get("traveling_merchant.flair.feed_family").ToString()
                    );
                }

                // 4. Recommendation: "{playerName} recommended that I visit {locationName} on {day}s."
                // Regex pattern: "(.+?) recommended that I visit (.+?) on (.+?)s\."
                var recMatch = Regex.Match(
                    text,
                    @"(.+?) recommended that I visit (.+?) on (.+?)s\."
                );
                if (recMatch.Success)
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

                // 5. Museum Hint: "I found something interesting, and I hear {playerName} wants someone to donate it to the Museum..."
                var museumMatch = Regex.Match(
                    text,
                    @"I found something interesting, and I hear (.+?) wants someone to donate it to the Museum\.\.\."
                );
                if (museumMatch.Success)
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

                // 6. Stock: "Stock: (\d+)%"
                var stockMatch = Regex.Match(text, @"Stock:\s*(\d+)%");
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
    }
}
