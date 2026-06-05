using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    /// <summary>
    /// Patches <c>ConversationFriendshipInjections.GrantConversationFriendship_TalkEvents_Postfix</c>
    /// to translate the hard-coded English George TV-Remote dialogues into the active locale.
    /// </summary>
    public static class DialoguePatcher
    {
        // English originals – used for recognition so we don't accidentally touch other dialogues.
        private const string DonatedOriginal =
            "Remember that complicated remote you gave me? I never could quite get used to it. "
            + "It was always triggering random things in the rooms of the house... "
            + "I brought it to the old Community Center.";

        private const string GivenOriginal =
            "I got this fancy new remote, but I can't figure it out. "
            + "It's always triggering random things in the rooms of the house. "
            + "Youngsters like you like these things, you can have it.";

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
                        "[DialoguePatcher] StardewArchipelago assembly not found – skipping.",
                        StardewModdingAPI.LogLevel.Warn
                    );
                    return;
                }

                var targetType = stardewAssembly.GetType(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.ConversationFriendshipInjections"
                );

                if (targetType == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[DialoguePatcher] ConversationFriendshipInjections type not found – skipping.",
                        StardewModdingAPI.LogLevel.Warn
                    );
                    return;
                }

                var targetMethod = targetType.GetMethod(
                    "GrantConversationFriendship_TalkEvents_Postfix",
                    BindingFlags.Public | BindingFlags.Static
                );

                if (targetMethod == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "[DialoguePatcher] GrantConversationFriendship_TalkEvents_Postfix method not found – skipping.",
                        StardewModdingAPI.LogLevel.Warn
                    );
                    return;
                }

                var postfix = new HarmonyMethod(
                    typeof(DialoguePatcher),
                    nameof(GrantConversationFriendship_Postfix)
                );

                harmony.Patch(targetMethod, postfix: postfix);

                ModEntry.Instance.Monitor.Log(
                    "[DialoguePatcher] Successfully patched ConversationFriendshipInjections!",
                    StardewModdingAPI.LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[DialoguePatcher] Failed to patch: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        /// <summary>
        /// Runs after <c>GrantConversationFriendship_TalkEvents_Postfix</c>.
        /// If the NPC is George and has a pending dialogue containing one of the two
        /// known English TV-Remote strings, replaces the text with the localised version.
        /// </summary>
        public static void GrantConversationFriendship_Postfix(NPC __instance)
        {
            try
            {
                if (__instance == null || __instance.Name != "George")
                    return;

                var currentDialogue = __instance.CurrentDialogue;
                if (currentDialogue == null || currentDialogue.Count == 0)
                    return;

                // Peek at the top dialogue entry.
                var top = currentDialogue.Peek();
                if (top == null)
                    return;

                // Try to get the dialogue text via the public getter or backing field.
                var dialogueText = GetDialogueText(top);
                if (dialogueText == null)
                    return;

                string? localized = null;

                if (dialogueText.Contains("complicated remote", StringComparison.OrdinalIgnoreCase))
                {
                    localized = ModEntry
                        .Translation.Get("dialogue.george.tv_remote_donated")
                        .ToString();
                }
                else if (
                    dialogueText.Contains("fancy new remote", StringComparison.OrdinalIgnoreCase)
                )
                {
                    localized = ModEntry
                        .Translation.Get("dialogue.george.tv_remote_given")
                        .ToString();
                }

                if (localized != null && localized != dialogueText)
                {
                    // Replace the whole dialogue stack with the localized version.
                    currentDialogue.Clear();
                    currentDialogue.Push(new Dialogue(__instance, null, localized));
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[DialoguePatcher] Error in GrantConversationFriendship_Postfix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        /// <summary>
        /// Extracts the raw text from a <see cref="Dialogue"/> object.
        /// Tries the public <c>dialogueText</c> property first, then falls back to reflection.
        /// </summary>
        private static string? GetDialogueText(Dialogue dialogue)
        {
            try
            {
                // The Dialogue class exposes the current text via getCurrentDialogue().
                var currentText = dialogue.getCurrentDialogue();
                if (!string.IsNullOrEmpty(currentText))
                    return currentText;
            }
            catch
            { /* ignore */
            }

            // Fallback: read private _dialogueParts or dialogues field via reflection.
            try
            {
                var field =
                    typeof(Dialogue).GetField(
                        "dialogues",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(Dialogue).GetField(
                        "_dialogueParts",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                if (field != null)
                {
                    var parts = field.GetValue(dialogue);
                    if (parts is System.Collections.Generic.List<string> list && list.Count > 0)
                        return list[0];
                }
            }
            catch
            { /* ignore */
            }

            return null;
        }
    }
}
