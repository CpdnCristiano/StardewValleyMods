using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    /// <summary>
    /// UI Info Suite 2 Alternative compatibility is intentionally height-only.
    /// Its own button layout stays intact; FIV only includes the expanded menu height
    /// so the calendar/billboard button remains attached to the correct bottom edge.
    /// </summary>
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
                Log.Warn("Could not find UIInfoSuite2Alt ComputeBoundsAndDrawIcons method; skipping height patch.");
            }


        }


        public static IEnumerable<CodeInstruction> computeBoundsAndDrawIconsTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (
                    codes[i].opcode == OpCodes.Ldfld
                    && codes[i].operand is FieldInfo fieldInfo
                    && fieldInfo.Name == "_hasFullInventoryView"
                )
                {
                    codes[i].opcode = OpCodes.Pop;
                    codes[i].operand = null;
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4_0));
                }

                if (
                    codes[i].opcode == OpCodes.Ldfld
                    && codes[i].operand is FieldInfo f1
                    && f1.Name == "yPositionOnScreen"
                )
                {
                    for (int j = i + 1; j < i + 10; j++)
                    {
                        if (
                            codes[j].opcode == OpCodes.Ldfld
                            && codes[j].operand is FieldInfo f2
                            && f2.Name == "height"
                            && codes[j + 1].opcode == OpCodes.Add
                        )
                        {
                            for (int k = j + 2; k < j + 10; k++)
                            {
                                if (codes[k].opcode == OpCodes.Sub)
                                {
                                    codes.Insert(
                                        k + 1,
                                        new CodeInstruction(
                                            OpCodes.Call,
                                            AccessTools.Method(
                                                typeof(InventoryGridMetrics),
                                                nameof(InventoryGridMetrics.GetExtraHeight)
                                            )
                                        )
                                    );
                                    codes.Insert(k + 2, new CodeInstruction(OpCodes.Add));
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
