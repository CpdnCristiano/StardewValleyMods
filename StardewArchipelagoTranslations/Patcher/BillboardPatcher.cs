using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class BillboardPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var performHoverActionMethod = AccessTools.Method(
                    typeof(Billboard),
                    nameof(Billboard.performHoverAction)
                );
                if (performHoverActionMethod != null)
                {
                    var postfix = new HarmonyMethod(
                        typeof(BillboardPatcher),
                        nameof(PerformHoverAction_Postfix)
                    );
                    postfix.priority = Priority.Last;
                    harmony.Patch(performHoverActionMethod, postfix: postfix);
                }

                // Patch Billboard constructor for controller support initialization
                var billboardConstructor = AccessTools.Constructor(
                    typeof(Billboard),
                    new[] { typeof(bool) }
                );
                if (billboardConstructor != null)
                {
                    var constructorPostfix = new HarmonyMethod(
                        typeof(BillboardPatcher),
                        nameof(BillboardConstructor_Postfix)
                    );
                    constructorPostfix.priority = Priority.Last;
                    harmony.Patch(billboardConstructor, postfix: constructorPostfix);
                }

                // Patch BillboardInjections.DrawRerollButton
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Tooltips.BillboardInjections"
                );
                if (targetType != null)
                {
                    var drawRerollButtonMethod = AccessTools.Method(targetType, "DrawRerollButton");
                    if (drawRerollButtonMethod != null)
                    {
                        harmony.Patch(
                            drawRerollButtonMethod,
                            prefix: new HarmonyMethod(
                                typeof(BillboardPatcher),
                                nameof(DrawRerollButton_Prefix)
                            )
                        );
                    }

                    var clickRerollButtonMethod = AccessTools.Method(
                        targetType,
                        "ReceiveLeftClick_ClickRerollButton_Postfix"
                    );
                    if (clickRerollButtonMethod != null)
                    {
                        harmony.Patch(
                            clickRerollButtonMethod,
                            prefix: new HarmonyMethod(
                                typeof(BillboardPatcher),
                                nameof(ReceiveLeftClick_ClickRerollButton_Prefix)
                            )
                        );
                    }
                }

                ModEntry.Instance.Monitor.Log(
                    "Successfully patched Billboard & BillboardInjections for calendar, reroll translations, controller support, and chat notifications!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch Billboard: {ex}", LogLevel.Error);
            }
        }

        public static void ReceiveLeftClick_ClickRerollButton_Prefix(
            Billboard __instance,
            int x,
            int y,
            bool playSound
        )
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Tooltips.BillboardInjections"
                );
                if (targetType == null)
                    return;

                var rerollButtonField = AccessTools.Field(targetType, "_rerollButton");
                var rerollButton = rerollButtonField?.GetValue(null) as ClickableComponent;

                if (
                    rerollButton != null
                    && rerollButton.visible
                    && rerollButton.containsPoint(x, y)
                )
                {
                    SendRerollChatMessage();
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ReceiveLeftClick_ClickRerollButton_Prefix: {ex}",
                    LogLevel.Error
                );
            }
        }

        private static void SendRerollChatMessage()
        {
            try
            {
                var chatMessageTranslation = ModEntry.Translation.Get(
                    "billboard.reroll.chatMessage"
                );
                if (chatMessageTranslation.HasValue())
                {
                    string template = chatMessageTranslation.ToString();
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        string message = template.Replace("{{player}}", Game1.player.Name);

                        // Send the message to the Archipelago server chat via ModEntry._archipelago
                        var modEntryType = AccessTools.TypeByName("StardewArchipelago.ModEntry");
                        var instanceField = AccessTools.Field(modEntryType, "Instance");
                        var instance = instanceField?.GetValue(null);
                        if (instance != null)
                        {
                            var archipelagoField = AccessTools.Field(modEntryType, "_archipelago");
                            var archipelagoClient = archipelagoField?.GetValue(instance);
                            if (archipelagoClient != null)
                            {
                                var sendMessageMethod = AccessTools.Method(
                                    archipelagoClient.GetType(),
                                    "SendMessage",
                                    new[] { typeof(string) }
                                );
                                sendMessageMethod?.Invoke(
                                    archipelagoClient,
                                    new object[] { message }
                                );
                                ModEntry.Instance.Monitor.Log(
                                    $"Sent reroll chat message to server: {message}",
                                    LogLevel.Info
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error sending reroll chat message: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void EnsureControllerSupport(Billboard billboard)
        {
            try
            {
                var dailyQuestBoard = ModEntry
                    .Instance.Helper.Reflection.GetField<bool>(billboard, "dailyQuestBoard")
                    .GetValue();
                if (!dailyQuestBoard)
                    return;

                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Tooltips.BillboardInjections"
                );
                if (targetType == null)
                    return;

                var rerollButtonField = AccessTools.Field(targetType, "_rerollButton");
                var rerollButton = rerollButtonField?.GetValue(null) as ClickableComponent;

                if (rerollButton != null)
                {
                    if (billboard.allClickableComponents == null)
                    {
                        billboard.allClickableComponents = new List<ClickableComponent>();
                    }

                    if (!billboard.allClickableComponents.Contains(rerollButton))
                    {
                        billboard.allClickableComponents.Add(rerollButton);
                    }

                    var acceptQuestButton = billboard.acceptQuestButton;
                    var closeButton = billboard.upperRightCloseButton;

                    // Setup neighbors dynamically
                    rerollButton.upNeighborID = -1;
                    rerollButton.leftNeighborID = -1;

                    if (acceptQuestButton != null)
                    {
                        rerollButton.downNeighborID = acceptQuestButton.myID;
                        if (acceptQuestButton.upNeighborID != rerollButton.myID)
                        {
                            acceptQuestButton.upNeighborID = rerollButton.myID;
                        }
                    }

                    if (closeButton != null)
                    {
                        rerollButton.rightNeighborID = closeButton.myID;
                        if (closeButton.leftNeighborID != rerollButton.myID)
                        {
                            closeButton.leftNeighborID = rerollButton.myID;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in EnsureControllerSupport: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void BillboardConstructor_Postfix(Billboard __instance, bool dailyQuest)
        {
            if (dailyQuest)
            {
                EnsureControllerSupport(__instance);
            }
        }

        public static bool DrawRerollButton_Prefix(Billboard billboard, SpriteBatch spriteBatch)
        {
            try
            {
                EnsureControllerSupport(billboard);

                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Tooltips.BillboardInjections"
                );
                if (targetType == null)
                    return true;

                var rerollButtonField = AccessTools.Field(targetType, "_rerollButton");
                var rerollButton = rerollButtonField?.GetValue(null) as ClickableComponent;

                if (rerollButton != null && rerollButton.visible)
                {
                    var rerollText = ModEntry.Translation.Get("billboard.reroll").ToString();

                    // Recalculate width dynamic to fit localized text
                    var newWidth = (int)Game1.dialogueFont.MeasureString(rerollText).X + 24;
                    if (rerollButton.bounds.Width != newWidth)
                    {
                        rerollButton.bounds.Width = newWidth;
                        rerollButton.bounds.X =
                            billboard.xPositionOnScreen + billboard.width * 2 / 4 - (newWidth / 2);
                    }

                    IClickableMenu.drawTextureBox(
                        spriteBatch,
                        Game1.mouseCursors,
                        new Rectangle(403, 373, 9, 9),
                        rerollButton.bounds.X,
                        rerollButton.bounds.Y,
                        rerollButton.bounds.Width,
                        rerollButton.bounds.Height,
                        rerollButton.scale > 1.0 ? Color.LightPink : Color.White,
                        4f * rerollButton.scale
                    );
                    Utility.drawTextWithShadow(
                        spriteBatch,
                        rerollText,
                        Game1.dialogueFont,
                        new Vector2(
                            rerollButton.bounds.X + 12,
                            rerollButton.bounds.Y
                                + (LocalizedContentManager.CurrentLanguageLatin ? 16 : 12)
                        ),
                        Game1.textColor
                    );
                }
                return false; // skip original
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DrawRerollButton_Prefix: {ex}",
                    LogLevel.Error
                );
                return true; // fallback
            }
        }

        public static void PerformHoverAction_Postfix(
            Billboard __instance,
            ref string ___hoverText,
            int x,
            int y
        )
        {
            try
            {
                EnsureControllerSupport(__instance);

                // Try to translate the Reroll button hover tooltip (as a joke/easter egg)
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Tooltips.BillboardInjections"
                );
                if (targetType != null)
                {
                    var rerollButtonField = AccessTools.Field(targetType, "_rerollButton");
                    var rerollButton = rerollButtonField?.GetValue(null) as ClickableComponent;
                    if (
                        rerollButton != null
                        && rerollButton.visible
                        && rerollButton.bounds.Contains(x, y)
                    )
                    {
                        ___hoverText = ModEntry
                            .Translation.Get("billboard.reroll.tooltip")
                            .ToString();
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(___hoverText))
                    return;

                var lines = ___hoverText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var translatedLines = new List<string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("- "))
                    {
                        var rawLocation = line.Substring(2).Trim();
                        // Restore "♡" to "<3" to perform translation lookup
                        var lookupLocation = rawLocation.Replace("♡", "<3");

                        var translatedLocation = TranslationHelper.GetLocalizedLocationName(
                            lookupLocation
                        );

                        // Convert "<3" back to "♡" in translated name
                        translatedLocation = translatedLocation.Replace("<3", "♡");

                        translatedLines.Add("- " + translatedLocation);
                    }
                    else
                    {
                        translatedLines.Add(line);
                    }
                }

                ___hoverText = string.Join(Environment.NewLine, translatedLines);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in BillboardPatcher.PerformHoverAction_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
