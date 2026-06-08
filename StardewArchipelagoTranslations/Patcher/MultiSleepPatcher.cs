using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class MultiSleepPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                // 1. Find all overloads of GameLocation.createQuestionDialogue dynamically and patch them
                var methods = typeof(GameLocation).GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                );
                int patchedCount = 0;
                foreach (var method in methods)
                {
                    if (method.Name == "createQuestionDialogue")
                    {
                        harmony.Patch(
                            original: method,
                            prefix: new HarmonyMethod(
                                typeof(MultiSleepPatcher),
                                nameof(CreateQuestionDialogue_Prefix)
                            )
                        );
                        patchedCount++;
                    }
                }
                ModEntry.Instance.Monitor.Log(
                    $"MultiSleepPatcher: Patched {patchedCount} overloads of createQuestionDialogue",
                    LogLevel.Info
                );

                // 2. Patch MultiSleepSelectionMenu constructor
                var selectionMenuConstructor = AccessTools.Constructor(
                    typeof(StardewArchipelago.GameModifications.MultiSleep.MultiSleepSelectionMenu),
                    new Type[]
                    {
                        typeof(string),
                        typeof(NumberSelectionMenu.behaviorOnNumberSelect),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                    }
                );
                if (selectionMenuConstructor != null)
                {
                    harmony.Patch(
                        original: selectionMenuConstructor,
                        prefix: new HarmonyMethod(
                            typeof(MultiSleepPatcher),
                            nameof(MultiSleepSelectionMenu_Constructor_Prefix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "MultiSleepPatcher: Patched MultiSleepSelectionMenu constructor",
                        LogLevel.Info
                    );
                }

                // 3. Patch Game1.drawObjectDialogue
                var drawObjectDialogueMethod = AccessTools.Method(
                    typeof(Game1),
                    nameof(Game1.drawObjectDialogue),
                    new Type[] { typeof(string) }
                );
                if (drawObjectDialogueMethod != null)
                {
                    harmony.Patch(
                        original: drawObjectDialogueMethod,
                        prefix: new HarmonyMethod(
                            typeof(MultiSleepPatcher),
                            nameof(DrawObjectDialogue_Prefix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "MultiSleepPatcher: Patched drawObjectDialogue",
                        LogLevel.Info
                    );
                }

            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error while applying MultiSleepPatcher patches: {ex.Message}",
                    LogLevel.Error
                );
            }
        }

        // Prefix for createQuestionDialogue using object[] __args to support any signature/overloads robustly
        public static bool CreateQuestionDialogue_Prefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 3)
                    return true;

                var question = __args[0] as string;
                var answerChoices = __args[1] as Response[];
                var dialogKey = __args[2] as string;

                if (dialogKey == "Sleep")
                {
                    if (answerChoices != null)
                    {
                        foreach (var response in answerChoices)
                        {
                            if (
                                response.responseKey == "Many"
                                && response.responseText == "Sleep for multiple days"
                            )
                            {
                                response.responseText = ModEntry.Translation.Get(
                                    "sleep.option.many"
                                );
                            }
                            else if (
                                response.responseKey == "Until"
                                && response.responseText == "Sleep until..."
                            )
                            {
                                response.responseText = ModEntry.Translation.Get(
                                    "sleep.option.until"
                                );
                            }
                        }
                    }
                }
                else if (dialogKey == "SleepUntil")
                {
                    __args[0] = ModEntry.Translation.Get("sleep.until.message").ToString();
                    if (answerChoices != null)
                    {
                        foreach (var response in answerChoices)
                        {
                            switch (response.responseKey)
                            {
                                case "Rain":
                                    if (response.responseText == "Rain")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.rain"
                                        );
                                    break;
                                case "Storm":
                                    if (response.responseText == "Storm")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.storm"
                                        );
                                    break;
                                case "Great Luck":
                                    if (response.responseText == "Great Luck")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.greatLuck"
                                        );
                                    break;
                                case "Festival":
                                    if (response.responseText == "Festival")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.festival"
                                        );
                                    break;
                                case "Birthday":
                                    if (response.responseText == "Birthday")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.birthday"
                                        );
                                    break;
                                case "Traveling Cart":
                                    if (response.responseText == "Traveling Cart")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.travelingCart"
                                        );
                                    break;
                                case "Bookseller":
                                    if (response.responseText == "Bookseller")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.bookseller"
                                        );
                                    break;
                                case "Any Crop Ready":
                                    if (response.responseText == "Any Crop Ready")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.anyCropReady"
                                        );
                                    break;
                                case "All Crops Ready":
                                    if (response.responseText == "All Crops Ready")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.allCropsReady"
                                        );
                                    break;
                                case "End of Month":
                                case "End of month":
                                    if (
                                        response.responseText == "End of month"
                                        || response.responseText == "End of Month"
                                    )
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.endOfMonth"
                                        );
                                    break;
                                case "Hibernate":
                                    if (response.responseText == "Hibernate")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.hibernate"
                                        );
                                    break;
                                case "Cancel":
                                    if (response.responseText == "Nevermind")
                                        response.responseText = ModEntry.Translation.Get(
                                            "sleep.until.cancel"
                                        );
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in MultiSleepPatcher.CreateQuestionDialogue_Prefix: {ex.Message}",
                    LogLevel.Trace
                );
            }
            return true;
        }

        // Prefix to translate the selection menu message
        public static bool MultiSleepSelectionMenu_Constructor_Prefix(ref string message)
        {
            try
            {
                if (
                    message
                    == "How many days do you wish to sleep for?\n(Warning: Sleeping saves the game, this action cannot be undone)"
                )
                {
                    message = ModEntry.Translation.Get("sleep.many.message");
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in MultiSleepPatcher.MultiSleepSelectionMenu_Constructor_Prefix: {ex.Message}",
                    LogLevel.Trace
                );
            }
            return true;
        }

        // Prefix to translate the cannot afford message
        public static bool DrawObjectDialogue_Prefix(ref string dialogue)
        {
            try
            {
                if (
                    dialogue != null
                    && dialogue.StartsWith("Cannot afford to continue multisleeping. Cost:")
                )
                {
                    var match = Regex.Match(
                        dialogue,
                        @"Cannot afford to continue multisleeping\. Cost: (\d+)g/day"
                    );
                    if (match.Success)
                    {
                        dialogue = ModEntry.Translation.Get(
                            "sleep.cannot_afford",
                            new { cost = match.Groups[1].Value }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in MultiSleepPatcher.DrawObjectDialogue_Prefix: {ex.Message}",
                    LogLevel.Trace
                );
            }
            return true;
        }
    }
}
