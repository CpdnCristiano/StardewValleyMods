#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8625
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class CarpenterPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CarpenterInjections"
                );
                if (targetType != null)
                {
                    var offerFreeUpgradeMethod = AccessTools.Method(
                        targetType,
                        "HouseUpgradeOffer_OfferFreeUpgrade_Prefix"
                    );
                    if (offerFreeUpgradeMethod != null)
                    {
                        harmony.Patch(
                            offerFreeUpgradeMethod,
                            prefix: new HarmonyMethod(
                                typeof(CarpenterPatcher),
                                nameof(HouseUpgradeOffer_OfferFreeUpgrade_Prefix_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched CarpenterInjections.HouseUpgradeOffer_OfferFreeUpgrade_Prefix for localized free upgrade dialogues!",
                            LogLevel.Info
                        );
                    }

                    var offerCheaperUpgradeMethod = AccessTools.Method(
                        targetType,
                        "HouseUpgradeOffer_OfferCheaperUpgrade_Prefix"
                    );
                    if (offerCheaperUpgradeMethod != null)
                    {
                        harmony.Patch(
                            offerCheaperUpgradeMethod,
                            prefix: new HarmonyMethod(
                                typeof(CarpenterPatcher),
                                nameof(HouseUpgradeOffer_OfferCheaperUpgrade_Prefix_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched CarpenterInjections.HouseUpgradeOffer_OfferCheaperUpgrade_Prefix for localized cheaper upgrade dialogues!",
                            LogLevel.Info
                        );
                    }

                    var acceptCheaperMethod = AccessTools.Method(
                        targetType,
                        "HouseUpgradeAccept_CheaperInAP_Prefix"
                    );
                    if (acceptCheaperMethod != null)
                    {
                        harmony.Patch(
                            acceptCheaperMethod,
                            prefix: new HarmonyMethod(
                                typeof(CarpenterPatcher),
                                nameof(HouseUpgradeAccept_CheaperInAP_Prefix_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched CarpenterInjections.HouseUpgradeAccept_CheaperInAP_Prefix for corrected ingredient amounts in error dialogues!",
                            LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch CarpenterInjections: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static bool HouseUpgradeOffer_OfferFreeUpgrade_Prefix_Prefix(
            ref bool __result,
            object[] __args
        )
        {
            try
            {
                var __instance = __args[0] as GameLocation;
                if (__instance == null)
                    return true;

                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (saAssembly == null)
                    return true; // Let original run if S.A. is not loaded

                var targetType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CarpenterInjections"
                );
                if (targetType == null)
                    return true;

                // Get _archipelago field from CarpenterInjections
                var archipelagoField = targetType.GetField(
                    "_archipelago",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (archipelagoField == null)
                    return true;

                var archipelago = archipelagoField.GetValue(null);
                if (archipelago == null)
                    return true;

                // Call archipelago.GetReceivedItemCount(BUILDING_PROGRESSIVE_HOUSE)
                var progressiveHouseConst =
                    targetType
                        .GetField(
                            "BUILDING_PROGRESSIVE_HOUSE",
                            BindingFlags.Static | BindingFlags.Public
                        )
                        ?.GetValue(null) as string;
                if (string.IsNullOrEmpty(progressiveHouseConst))
                    progressiveHouseConst = "Progressive House";

                var getReceivedItemCountMethod = archipelago
                    .GetType()
                    .GetMethod("GetReceivedItemCount", new[] { typeof(string) });
                if (getReceivedItemCountMethod == null)
                    return true;

                var receivedHouseUpgrades = (int)
                    getReceivedItemCountMethod.Invoke(
                        archipelago,
                        new object[] { progressiveHouseConst }
                    );
                if (Game1.player.HouseUpgradeLevel >= receivedHouseUpgrades)
                {
                    __result = false; // DONT_RUN_ORIGINAL_METHOD
                    return false;
                }

                // Call archipelago.GetAllReceivedItems()
                var getAllReceivedItemsMethod = archipelago
                    .GetType()
                    .GetMethod("GetAllReceivedItems");
                if (getAllReceivedItemsMethod == null)
                    return true;

                var receivedItems =
                    getAllReceivedItemsMethod.Invoke(archipelago, null)
                    as System.Collections.IEnumerable;
                if (receivedItems == null)
                    return true;

                var houseUpgradeReceivedFromAPList = new System.Collections.Generic.List<dynamic>();
                foreach (var item in receivedItems)
                {
                    var itemName = item.GetType().GetProperty("ItemName")?.GetValue(item) as string;
                    if (itemName == progressiveHouseConst)
                    {
                        houseUpgradeReceivedFromAPList.Add(item);
                    }
                }
                var houseUpgradeReceivedFromAP = houseUpgradeReceivedFromAPList.ToArray();

                var apUpgradeDialogue = ModEntry.Translation.Get("carpenter.funded").ToString();
                var startNowQuestion =
                    "^" + ModEntry.Translation.Get("carpenter.start_now").ToString();

                switch (Game1.player.HouseUpgradeLevel)
                {
                    case 0:
                        var player0 =
                            houseUpgradeReceivedFromAP.Length > 0
                                ? houseUpgradeReceivedFromAP[0]?.PlayerName ?? "Server"
                                : "Server";
                        var paidKitchenStr = ModEntry
                            .Translation.Get("carpenter.paid_kitchen", new { player = player0 })
                            .ToString();
                        apUpgradeDialogue += $"^{paidKitchenStr}{startNowQuestion}";
                        __instance.createQuestionDialogue(
                            apUpgradeDialogue,
                            __instance.createYesNoResponses(),
                            "upgrade"
                        );
                        break;
                    case 1:
                        var player1 =
                            houseUpgradeReceivedFromAP.Length > 1
                                ? houseUpgradeReceivedFromAP[1]?.PlayerName ?? "Server"
                                : "Server";
                        var paidSecondFloorStr = ModEntry
                            .Translation.Get(
                                "carpenter.paid_second_floor",
                                new { player = player1 }
                            )
                            .ToString();
                        var bonusDialogue = GetChildRoomDialogue();
                        apUpgradeDialogue +=
                            $"^{paidSecondFloorStr} {bonusDialogue}{startNowQuestion}";
                        __instance.createQuestionDialogue(
                            apUpgradeDialogue,
                            __instance.createYesNoResponses(),
                            "upgrade"
                        );
                        break;
                    case 2:
                        var player2 =
                            houseUpgradeReceivedFromAP.Length > 2
                                ? houseUpgradeReceivedFromAP[2]?.PlayerName ?? "Server"
                                : "Server";
                        var paidCellarStr = ModEntry
                            .Translation.Get("carpenter.paid_cellar", new { player = player2 })
                            .ToString();
                        var cellarGoatStr = ModEntry
                            .Translation.Get("carpenter.cellar_goat_cheese")
                            .ToString();
                        apUpgradeDialogue += $"^{paidCellarStr}^{cellarGoatStr}{startNowQuestion}";
                        __instance.createQuestionDialogue(
                            apUpgradeDialogue,
                            __instance.createYesNoResponses(),
                            "upgrade"
                        );
                        break;
                }

                __result = false; // DONT_RUN_ORIGINAL_METHOD
                return false; // Skip original
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in HouseUpgradeOffer_OfferFreeUpgrade_Prefix_Prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        private static string GetChildRoomDialogue()
        {
            var player = Game1.player;
            if (!player.isMarriedOrRoommates() && !player.isEngaged())
            {
                var who = player
                    .friendshipData.Keys.Where(name => player.friendshipData[name].IsDating())
                    .MaxBy(name => player.friendshipData[name].Points);

                if (who == null)
                {
                    return ModEntry.Translation.Get("carpenter.town_start").ToString();
                }

                return ModEntry.Translation.Get("carpenter.pop_question", new { who }).ToString();
            }

            var friendshipNature = player.friendshipData[player.spouse];
            var spouse = Game1.getCharacterFromName(player.spouse);

            if (player.isEngaged())
            {
                return ModEntry
                    .Translation.Get("carpenter.new_lives", new { spouse = spouse.Name })
                    .ToString();
            }

            if (friendshipNature.RoommateMarriage)
            {
                return ModEntry.Translation.Get("carpenter.no_kid_beds").ToString();
            }
            else
            {
                var spouseIsMale = spouse.Gender == 0;
                if (player.IsMale == spouseIsMale)
                {
                    return ModEntry
                        .Translation.Get("carpenter.adopt", new { spouse = spouse.Name })
                        .ToString();
                }
                else
                {
                    if (player.IsMale)
                    {
                        return ModEntry
                            .Translation.Get(
                                "carpenter.expecting_spouse",
                                new { spouse = spouse.Name }
                            )
                            .ToString();
                    }
                    else
                    {
                        return ModEntry
                            .Translation.Get(
                                "carpenter.expecting_you",
                                new { spouse = spouse.Name }
                            )
                            .ToString();
                    }
                }
            }
        }

        public static bool HouseUpgradeOffer_OfferCheaperUpgrade_Prefix_Prefix(
            ref bool __result,
            object[] __args
        )
        {
            try
            {
                var __instance = __args[0] as GameLocation;
                if (__instance == null)
                    return true;

                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (saAssembly == null)
                    return true;

                var targetType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CarpenterInjections"
                );
                if (targetType == null)
                    return true;

                var archipelagoField = targetType.GetField(
                    "_archipelago",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (archipelagoField == null)
                    return true;

                var archipelago = archipelagoField.GetValue(null);
                if (archipelago == null)
                    return true;

                var slotData = archipelago.GetType().GetProperty("SlotData")?.GetValue(archipelago);
                if (slotData == null)
                    return true;

                var priceMultiplierProp = slotData.GetType().GetProperty("BuildingPriceMultiplier");
                if (priceMultiplierProp == null)
                    return true;

                var priceMultiplier = (double)priceMultiplierProp.GetValue(slotData);
                if (Math.Abs(priceMultiplier - 1.0) < 0.001)
                {
                    return true;
                }

                var text = "";
                switch (Game1.player.HouseUpgradeLevel)
                {
                    case 0:
                        text = Game1.parseText(
                            Game1.content.LoadString(
                                "Strings\\Locations:ScienceHouse_Carpenter_UpgradeHouse1"
                            )
                        );
                        var goldPrice = (int)Math.Round(10000 * priceMultiplier);
                        var woodPrice = Math.Max(1, (int)Math.Round(450 * priceMultiplier));
                        text = text.Replace("10,000", Utility.getNumberWithCommas(goldPrice))
                            .Replace("10.000", Utility.getNumberWithCommas(goldPrice))
                            .Replace("450", woodPrice.ToString());
                        break;
                    case 1:
                        var priceGold = (int)Math.Round(50000 * priceMultiplier);
                        var priceWood = Math.Max(1, (int)Math.Round(150 * priceMultiplier));
                        text = Game1.parseText(
                            Game1.content.LoadString(
                                "Strings\\Locations:ScienceHouse_Carpenter_UpgradeHouse2",
                                priceGold,
                                priceWood
                            )
                        );
                        break;
                    case 2:
                        text = Game1.parseText(
                            Game1.content.LoadString(
                                "Strings\\Locations:ScienceHouse_Carpenter_UpgradeHouse3"
                            )
                        );
                        var goldPrice3 = (int)Math.Round(100000 * priceMultiplier);
                        text = text.Replace("100,000", Utility.getNumberWithCommas(goldPrice3))
                            .Replace("100.000", Utility.getNumberWithCommas(goldPrice3));
                        break;
                }

                __instance.createQuestionDialogue(
                    text,
                    __instance.createYesNoResponses(),
                    "upgrade"
                );
                __result = false; // DONT_RUN_ORIGINAL_METHOD
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in HouseUpgradeOffer_OfferCheaperUpgrade_Prefix_Prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        public static bool HouseUpgradeAccept_CheaperInAP_Prefix_Prefix(
            ref bool __result,
            object[] __args
        )
        {
            try
            {
                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (saAssembly == null)
                    return true;

                var targetType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CarpenterInjections"
                );
                if (targetType == null)
                    return true;

                var archipelagoField = targetType.GetField(
                    "_archipelago",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (archipelagoField == null)
                    return true;

                var archipelago = archipelagoField.GetValue(null);
                if (archipelago == null)
                    return true;

                var slotData = archipelago.GetType().GetProperty("SlotData")?.GetValue(archipelago);
                if (slotData == null)
                    return true;

                var priceMultiplierProp = slotData.GetType().GetProperty("BuildingPriceMultiplier");
                if (priceMultiplierProp == null)
                    return true;

                var priceMultiplier = (double)priceMultiplierProp.GetValue(slotData);
                if (Math.Abs(priceMultiplier - 1.0) < 0.001)
                {
                    return true;
                }

                switch (Game1.player.HouseUpgradeLevel)
                {
                    case 0:
                        var price1 = (int)(10000 * priceMultiplier);
                        var woodAmount = Math.Max(1, (int)(450 * priceMultiplier));
                        if (
                            Game1.player.Money >= price1
                            && Game1.player.Items.ContainsId("388", woodAmount)
                        )
                        {
                            Game1.player.daysUntilHouseUpgrade.Value = 3;
                            Game1.player.Money -= price1;
                            Game1.player.Items.ReduceId("388", woodAmount);
                            Game1
                                .RequireCharacter("Robin")
                                .setNewDialogue("Data\\ExtraDialogue:Robin_HouseUpgrade_Accepted");
                            Game1.drawDialogue(Game1.getCharacterFromName("Robin"));
                            break;
                        }
                        if (Game1.player.Money < price1)
                        {
                            Game1.drawObjectDialogue(
                                Game1.content.LoadString("Strings\\UI:NotEnoughMoney3")
                            );
                            break;
                        }
                        Game1.drawObjectDialogue(
                            Game1.content.LoadString(
                                "Strings\\Locations:ScienceHouse_Carpenter_NotEnoughWood",
                                (object)woodAmount
                            )
                        );
                        break;
                    case 1:
                        var price2 = (int)(50000 * priceMultiplier);
                        var hardwoodAmount = Math.Max(1, (int)(150 * priceMultiplier));
                        if (
                            Game1.player.Money >= price2
                            && Game1.player.Items.ContainsId("709", hardwoodAmount)
                        )
                        {
                            Game1.player.daysUntilHouseUpgrade.Value = 3;
                            Game1.player.Money -= price2;
                            Game1.player.Items.ReduceId("709", hardwoodAmount);
                            Game1
                                .RequireCharacter("Robin")
                                .setNewDialogue("Data\\ExtraDialogue:Robin_HouseUpgrade_Accepted");
                            Game1.drawDialogue(Game1.getCharacterFromName("Robin"));
                            break;
                        }
                        if (Game1.player.Money < price2)
                        {
                            Game1.drawObjectDialogue(
                                Game1.content.LoadString("Strings\\UI:NotEnoughMoney3")
                            );
                            break;
                        }
                        Game1.drawObjectDialogue(
                            Game1.content.LoadString(
                                "Strings\\Locations:ScienceHouse_Carpenter_NotEnoughHardwood",
                                (object)hardwoodAmount
                            )
                        );
                        break;
                    case 2:
                        var price3 = (int)(100000 * priceMultiplier);
                        if (Game1.player.Money >= price3)
                        {
                            Game1.player.daysUntilHouseUpgrade.Value = 3;
                            Game1.player.Money -= price3;
                            Game1
                                .RequireCharacter("Robin")
                                .setNewDialogue("Data\\ExtraDialogue:Robin_HouseUpgrade_Accepted");
                            Game1.drawDialogue(Game1.getCharacterFromName("Robin"));
                            break;
                        }
                        Game1.drawObjectDialogue(
                            Game1.content.LoadString("Strings\\UI:NotEnoughMoney3")
                        );
                        break;
                }

                __result = false; // DONT_RUN_ORIGINAL_METHOD
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in HouseUpgradeAccept_CheaperInAP_Prefix_Prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }
    }
}
