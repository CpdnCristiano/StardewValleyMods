using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal class UiInfo2Patcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var original = AccessTools.Method(
                "UIInfoSuite2.UIElements.ShowCalendarAndBillboardOnGameMenuButton:DrawBillboard"
            );
            if (original is null)
            {
                monitor.Log("Could not find UIInfoSuite2 DrawBillboard method; skipping patch.", LogLevel.Warn);
                return;
            }
            harmony.Patch(
                original: original,
                transpiler: this.GetHarmonyMethod(nameof(drawBillboardTranspiler)),
                postfix: this.GetHarmonyMethod(nameof(drawBillboardPostfix))
            );
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

        public static IEnumerable<CodeInstruction> drawBillboardTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count - 5; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo f1 && f1.Name == "yPositionOnScreen")
                {
                    for (int j = i + 1; j < i + 6; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldfld && codes[j].operand is FieldInfo f2 && f2.Name == "height" && codes[j + 1].opcode == OpCodes.Add)
                        {
                            codes.Insert(j + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InventoryMenuPatcher), nameof(InventoryMenuPatcher.GetExtraHeight))));
                            codes.Insert(j + 3, new CodeInstruction(OpCodes.Add));
                            Log.Debug("Patching UIInfoSuite2 to add extra height to the billboard");
                            return codes;
                        }
                    }
                }
            }
            return codes;
        }
    }
}
