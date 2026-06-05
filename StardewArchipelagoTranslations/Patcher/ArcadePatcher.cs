#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8625
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ArcadePatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (saAssembly == null)
                    return;

                // 1. Patch JotPKInjections.PlayerPressedNewGame
                var jotpkInjectionsType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.Arcade.JotPKInjections"
                );
                if (jotpkInjectionsType != null)
                {
                    var playerPressedNewGameMethod = AccessTools.Method(
                        jotpkInjectionsType,
                        "PlayerPressedNewGame"
                    );
                    if (playerPressedNewGameMethod != null)
                    {
                        harmony.Patch(
                            playerPressedNewGameMethod,
                            prefix: new HarmonyMethod(
                                typeof(ArcadePatcher),
                                nameof(PlayerPressedNewGame_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched JotPKInjections.PlayerPressedNewGame for stage selection translations!",
                            LogLevel.Info
                        );
                    }

                    var usePowerupMethod = AccessTools.Method(
                        jotpkInjectionsType,
                        "UsePowerup_PrairieKingBossBeaten_Prefix"
                    );
                    if (usePowerupMethod != null)
                    {
                        harmony.Patch(
                            usePowerupMethod,
                            prefix: new HarmonyMethod(
                                typeof(ArcadePatcher),
                                nameof(UsePowerup_Prefix)
                            )
                        );
                    }
                }

                // 2. Patch JunimoKartInjections.SendJunimoKartLevelsBeatChecks
                var jkInjectionsType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.Arcade.JunimoKartInjections"
                );
                if (jkInjectionsType != null)
                {
                    var sendJkChecksMethod = AccessTools.Method(
                        jkInjectionsType,
                        "SendJunimoKartLevelsBeatChecks"
                    );
                    if (sendJkChecksMethod != null)
                    {
                        harmony.Patch(
                            sendJkChecksMethod,
                            prefix: new HarmonyMethod(
                                typeof(ArcadePatcher),
                                nameof(SendJunimoKartLevelsBeatChecks_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched JunimoKartInjections for beat checks translations!",
                            LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch Arcade: {ex}", LogLevel.Error);
            }
        }

        public static bool PlayerPressedNewGame_Prefix()
        {
            try
            {
                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (saAssembly == null)
                    return true;

                var jotpkInjectionsType = saAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.Arcade.JotPKInjections"
                );
                var stateField = jotpkInjectionsType?.GetField(
                    "_state",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var state = stateField?.GetValue(null);
                if (state == null)
                    return true;

                var maxLevelProp = state.GetType().GetProperty("MaxJotPKLevelBeaten");
                int maxLevel = (int)maxLevelProp.GetValue(state);

                var canStartAhead = maxLevel >= 5;
                if (!canStartAhead)
                {
                    Game1.player.jotpkProgress.Value = null;
                    Game1.currentMinigame = new StardewValley.Minigames.AbigailGame();
                    return false;
                }

                var answerChoices = new List<Response>();
                answerChoices.Add(
                    new Response(
                        "Stage1",
                        ModEntry.Translation.Get("config.arcade.stage_1").ToString()
                    )
                );
                if (maxLevel >= 5)
                {
                    answerChoices.Add(
                        new Response(
                            "Stage2",
                            ModEntry.Translation.Get("config.arcade.stage_2").ToString()
                        )
                    );
                }
                if (maxLevel >= 9)
                {
                    answerChoices.Add(
                        new Response(
                            "Stage3",
                            ModEntry.Translation.Get("config.arcade.stage_3").ToString()
                        )
                    );
                }

                Game1.player.currentLocation.createQuestionDialogue(
                    ModEntry.Translation.Get("config.arcade.start_from").ToString(),
                    answerChoices.ToArray(),
                    "CowboyGame"
                );
                return false; // skip original
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in PlayerPressedNewGame_Prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        public static bool UsePowerup_Prefix(ref bool __result, object __instance, int which)
        {
            try
            {
                var minigame = Game1.currentMinigame as StardewValley.Minigames.AbigailGame;
                if (minigame == null)
                    return true;

                if (minigame.activePowerups.ContainsKey(which) || which > -1)
                {
                    return true;
                }

                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                var jotpkInjectionsType = saAssembly?.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.Arcade.JotPKInjections"
                );
                var locationCheckerField = jotpkInjectionsType?.GetField(
                    "_locationChecker",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var locationChecker = locationCheckerField?.GetValue(null);
                var stateField = jotpkInjectionsType?.GetField(
                    "_state",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var state = stateField?.GetValue(null);

                if (locationChecker != null && state != null)
                {
                    var addCheckedLocationMethod = locationChecker
                        .GetType()
                        .GetMethod("AddCheckedLocation", new[] { typeof(string) });
                    var maxLevelProp = state.GetType().GetProperty("MaxJotPKLevelBeaten");

                    if (which == -3)
                    {
                        addCheckedLocationMethod?.Invoke(
                            locationChecker,
                            new object[] { "Journey of the Prairie King Victory" }
                        );
                        Game1.chatBox?.addMessage(
                            ModEntry.Translation.Get("config.arcade.release_jotpk").ToString(),
                            Color.Green
                        );
                    }
                    else
                    {
                        string whichCowboyWasBeaten;
                        int currentMax = (int)maxLevelProp.GetValue(state);
                        if (which == -1)
                        {
                            whichCowboyWasBeaten = "JotPK: Cowboy 1";
                            maxLevelProp.SetValue(state, Math.Max(currentMax, 5));
                        }
                        else
                        {
                            whichCowboyWasBeaten = "JotPK: Cowboy 2";
                            maxLevelProp.SetValue(state, Math.Max(currentMax, 9));
                        }
                        addCheckedLocationMethod?.Invoke(
                            locationChecker,
                            new object[] { whichCowboyWasBeaten }
                        );
                    }

                    __result = true; // MethodPrefix.RUN_ORIGINAL_METHOD
                    return false; // Skip original English prefix
                }
            }
            catch { }
            return true;
        }

        public static bool SendJunimoKartLevelsBeatChecks_Prefix(object __instance)
        {
            try
            {
                var minecart = __instance as StardewValley.Minigames.MineCart;
                if (minecart == null)
                    return true;

                var saAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                var jkInjectionsType = saAssembly?.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.Arcade.JunimoKartInjections"
                );
                if (jkInjectionsType == null)
                    return true;

                var helperField = jkInjectionsType.GetField(
                    "_helper",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var helper = helperField?.GetValue(null) as IModHelper;
                var locationCheckerField = jkInjectionsType.GetField(
                    "_locationChecker",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var locationChecker = locationCheckerField?.GetValue(null);

                if (helper == null || locationChecker == null)
                    return true;

                var gamemode = helper.Reflection.GetField<int>(minecart, "gameMode").GetValue();
                var levelsBeat = helper.Reflection.GetField<int>(minecart, "levelsBeat").GetValue();
                var levelsFinishedThisRun = helper
                    .Reflection.GetField<List<int>>(minecart, "levelThemesFinishedThisRun")
                    .GetValue();

                if (gamemode != 3 || levelsBeat < 1)
                {
                    return false; // skip original since it won't do anything either
                }

                var levelLocationsField = jkInjectionsType.GetField(
                    "JK_LEVEL_LOCATIONS",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var jkLevelLocations =
                    levelLocationsField?.GetValue(null) as Dictionary<int, string>;
                if (jkLevelLocations == null)
                    return true;

                var isLocationMissingMethod = locationChecker
                    .GetType()
                    .GetMethod("IsLocationMissing", new[] { typeof(string) });
                var addCheckedLocationMethod = locationChecker
                    .GetType()
                    .GetMethod("AddCheckedLocation", new[] { typeof(string) });

                foreach (var levelFinished in levelsFinishedThisRun)
                {
                    if (jkLevelLocations.TryGetValue(levelFinished, out var location))
                    {
                        if (location == "Junimo Kart: Sunset Speedway (Victory)")
                        {
                            bool isMissing = (bool)
                                isLocationMissingMethod.Invoke(
                                    locationChecker,
                                    new object[] { "Junimo Kart: Sunset Speedway (Victory)" }
                                );
                            if (isMissing)
                            {
                                Game1.chatBox?.addMessage(
                                    ModEntry.Translation.Get("config.arcade.release_jk").ToString(),
                                    Color.Green
                                );
                            }
                        }
                        addCheckedLocationMethod.Invoke(locationChecker, new object[] { location });
                    }
                }

                return false; // skip original to avoid duplicate English message and duplicate logic
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in SendJunimoKartLevelsBeatChecks_Prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }
    }
}
