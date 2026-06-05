using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class WizardBookPatcher
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
                        "[WizardBookPatcher] StardewArchipelago assembly not found – skipping.",
                        LogLevel.Warn
                    );
                    return;
                }

                var targetType = stardewAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.WizardBookInjections"
                );

                if (targetType == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[WizardBookPatcher] WizardBookInjections type not found – skipping.",
                        LogLevel.Warn
                    );
                    return;
                }

                // Patch GameLocation.createQuestionDialogue prefix to intercept the magic book prompt
                var createQuestionDialogueMethod = AccessTools.Method(
                    typeof(GameLocation),
                    nameof(GameLocation.createQuestionDialogue),
                    new[] { typeof(string), typeof(Response[]), typeof(string) }
                );

                if (createQuestionDialogueMethod != null)
                {
                    harmony.Patch(
                        createQuestionDialogueMethod,
                        prefix: new HarmonyMethod(
                            typeof(WizardBookPatcher),
                            nameof(CreateQuestionDialogue_Prefix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "[WizardBookPatcher] Successfully patched GameLocation.createQuestionDialogue for wizard book!",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[WizardBookPatcher] Failed to patch: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static bool CreateQuestionDialogue_Prefix(
            ref string question,
            ref Response[] answerChoices,
            string dialogKey
        )
        {
            try
            {
                if (dialogKey == "magicbook")
                {
                    question = ModEntry.Translation.Get("wizard_book.dialogue.question").ToString();
                    for (int i = 0; i < answerChoices.Length; i++)
                    {
                        var responseKey = answerChoices[i].responseKey;
                        if (responseKey == "Shop")
                        {
                            answerChoices[i].responseText = ModEntry
                                .Translation.Get("wizard_book.dialogue.shop")
                                .ToString();
                        }
                        else if (responseKey == "Construct")
                        {
                            answerChoices[i].responseText = ModEntry
                                .Translation.Get("wizard_book.dialogue.construct")
                                .ToString();
                        }
                        else if (responseKey == "Leave")
                        {
                            answerChoices[i].responseText = ModEntry
                                .Translation.Get("wizard_book.dialogue.leave")
                                .ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in CreateQuestionDialogue_Prefix for Wizard Book: {ex}",
                    LogLevel.Error
                );
            }
            return true;
        }
    }
}
