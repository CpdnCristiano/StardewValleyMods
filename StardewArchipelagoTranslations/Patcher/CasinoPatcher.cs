using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using xTile.Dimensions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class CasinoPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CasinoInjections"
                );
                if (type == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "CasinoInjections type not found, skipping casino patch.",
                        LogLevel.Debug
                    );
                    return;
                }

                var method = type.GetMethod(
                    "PerformAction_OfferStatueOfEndlessFortune_Prefix",
                    BindingFlags.Static | BindingFlags.Public
                );
                if (method != null)
                {
                    harmony.Patch(
                        original: method,
                        prefix: new HarmonyMethod(
                            typeof(CasinoPatcher),
                            nameof(PerformAction_OfferStatueOfEndlessFortune_Prefix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched CasinoInjections.PerformAction_OfferStatueOfEndlessFortune_Prefix.",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch CasinoInjections: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static bool PerformAction_OfferStatueOfEndlessFortune_Prefix(
            GameLocation __instance,
            string[] action,
            Farmer who,
            Location tileLocation,
            ref bool __result
        )
        {
            try
            {
                if (__instance.Name != "Club")
                {
                    return true; // RUN_ORIGINAL_METHOD (let CasinoInjections handle it)
                }

                if (
                    !ArgUtility.TryGet(
                        action,
                        0,
                        out var key1,
                        out var error,
                        name: "string actionType"
                    )
                    || key1 != "ClubSeller"
                )
                {
                    return true; // RUN_ORIGINAL_METHOD
                }

                // Obtain the Archipelago StardewLocationChecker and client via reflection to respect Archipelago settings
                var type = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.CasinoInjections"
                );
                if (type == null)
                {
                    return true;
                }

                var checkerField = type.GetField(
                    "_locationChecker",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var clientField = type.GetField(
                    "_archipelago",
                    BindingFlags.Static | BindingFlags.NonPublic
                );

                if (checkerField == null || clientField == null)
                {
                    return true; // Fallback to original
                }

                var checker = checkerField.GetValue(null);
                var client = clientField.GetValue(null);

                if (checker == null || client == null)
                {
                    return true;
                }

                var isLocationMissingMethod = checker
                    .GetType()
                    .GetMethod("IsLocationMissing", new[] { typeof(string) });
                var hasReceivedItemMethod = client
                    .GetType()
                    .GetMethod("HasReceivedItem", new[] { typeof(string) });
                var scoutLocationMethod = client
                    .GetType()
                    .GetMethod("ScoutStardewLocation", new[] { typeof(string), typeof(bool) });

                if (
                    isLocationMissingMethod == null
                    || hasReceivedItemMethod == null
                    || scoutLocationMethod == null
                )
                {
                    return true;
                }

                const string STATUE_LOCATION = "Purchase Statue Of Endless Fortune";
                const string STATUE_ITEM = "Statue of Endless Fortune";

                var isMissingVal = isLocationMissingMethod.Invoke(
                    checker,
                    new object[] { STATUE_LOCATION }
                );
                var hasReceivedVal = hasReceivedItemMethod.Invoke(
                    client,
                    new object[] { STATUE_ITEM }
                );

                if (isMissingVal == null || hasReceivedVal == null)
                {
                    return true;
                }

                var isMissing = (bool)isMissingVal;
                var hasReceived = (bool)hasReceivedVal;

                var statueName = STATUE_ITEM;

                // Localize the item name (either the scouted Archipelago check name or the localized Statue of Endless Fortune)
                if (isMissing)
                {
                    var scoutResult = scoutLocationMethod.Invoke(
                        client,
                        new object[] { STATUE_LOCATION, false }
                    );
                    if (scoutResult != null)
                    {
                        var itemNameProperty = scoutResult.GetType().GetProperty("ItemName");
                        var scoutedItemName = itemNameProperty?.GetValue(scoutResult) as string;
                        if (!string.IsNullOrEmpty(scoutedItemName))
                        {
                            statueName = TranslationHelper.GetLocalizedItemName(scoutedItemName);
                        }
                    }
                }
                else
                {
                    statueName = TranslationHelper.GetLocalizedItemName(STATUE_ITEM);
                }

                // Load and format the Portuguese (or active language) localized base game string
                var baseGameText = Game1.content.LoadString(
                    "Strings\\Locations:Club_ClubSeller",
                    statueName
                );

                var responses = new List<Response>();
                if (isMissing || hasReceived)
                {
                    responses.Add(
                        new Response(
                            "I'll",
                            Game1.content.LoadString("Strings\\Locations:Club_ClubSeller_Yes")
                        )
                    );
                }
                responses.Add(
                    new Response(
                        "No",
                        Game1.content.LoadString("Strings\\Locations:Club_ClubSeller_No")
                    )
                );

                __instance.createQuestionDialogue(baseGameText, responses.ToArray(), "ClubSeller");

                __result = true;
                return false; // DONT_RUN_ORIGINAL_METHOD (Skip the Archipelago mod's English prefix)
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in CasinoPatcher: {ex}", LogLevel.Error);
                return true; // Fallback to original
            }
        }
    }
}
