using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal class UiInfo2AltPatcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var originalCompute = AccessTools.Method(
                "UIInfoSuite2Alt.UIElements.ShowCalendarAndBillboardOnGameMenuButton:ComputeBoundsAndDrawIcons",
                new Type[] { typeof(SpriteBatch), typeof(IClickableMenu) }
            );
            if (originalCompute != null)
            {
                harmony.Patch(
                    original: originalCompute,
                    transpiler: this.GetHarmonyMethod(nameof(computeBoundsAndDrawIconsTranspiler))
                );
            }
            else
            {
                monitor.Log("Could not find UIInfoSuite2Alt ComputeBoundsAndDrawIcons method; skipping offset patch.", LogLevel.Warn);
            }

            var originalDraw = AccessTools.Method(
                "UIInfoSuite2Alt.UIElements.ShowCalendarAndBillboardOnGameMenuButton:DrawBillboard"
            );
            if (originalDraw != null)
            {
                harmony.Patch(
                    original: originalDraw,
                    postfix: this.GetHarmonyMethod(nameof(drawBillboardPostfix))
                );
            }
            else
            {
                monitor.Log("Could not find UIInfoSuite2Alt DrawBillboard method; skipping postfix patch.", LogLevel.Warn);
            }
        }

        private static FieldInfo? _showBillboardButtonField;

        public static void drawBillboardPostfix(object __instance)
        {
            if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.GetCurrentPage() is InventoryPage page)
            {
                _showBillboardButtonField ??= __instance.GetType().GetField("_showBillboardButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_showBillboardButtonField != null)
                {
                    var perScreenVal = _showBillboardButtonField.GetValue(__instance);
                    if (perScreenVal != null)
                    {
                        var valueProp = perScreenVal.GetType().GetProperty("Value");
                        if (valueProp != null)
                        {
                            if (valueProp.GetValue(perScreenVal) is ClickableTextureComponent button)
                            {
                                if (button.myID <= 0)
                                {
                                    button.myID = 77770; // Calendar/Billboard ID
                                }
                                if (page.allClickableComponents != null && !page.allClickableComponents.Contains(button))
                                {
                                    page.allClickableComponents.Add(button);
                                }
                                if (gameMenu.allClickableComponents != null && !gameMenu.allClickableComponents.Contains(button))
                                {
                                    gameMenu.allClickableComponents.Add(button);
                                }
                            }
                        }
                    }
                }

                if (gameMenu.allClickableComponents != null)
                {
                    InventoryMenuPatcher.WireGamepadNavigation(page, gameMenu.allClickableComponents);
                }
            }
        }

        public static IEnumerable<CodeInstruction> computeBoundsAndDrawIconsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo fieldInfo && fieldInfo.Name == "_hasFullInventoryView")
                {
                    codes[i].opcode = OpCodes.Pop;
                    codes[i].operand = null;
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4_0));
                    Log.Debug("Patching UIInfoSuite2Alt to bypass built-in _hasFullInventoryView offset adjustment");
                }

                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo f1 && f1.Name == "yPositionOnScreen")
                {
                    for (int j = i + 1; j < i + 10; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldfld && codes[j].operand is FieldInfo f2 && f2.Name == "height" && codes[j + 1].opcode == OpCodes.Add)
                        {
                            for (int k = j + 2; k < j + 10; k++)
                            {
                                if (codes[k].opcode == OpCodes.Sub)
                                {
                                    codes.Insert(k + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InventoryMenuPatcher), nameof(InventoryMenuPatcher.GetExtraHeight))));
                                    codes.Insert(k + 2, new CodeInstruction(OpCodes.Add));
                                    Log.Debug("Patching UIInfoSuite2Alt to add extra height to baseY");
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return codes;
        }
    }
}
