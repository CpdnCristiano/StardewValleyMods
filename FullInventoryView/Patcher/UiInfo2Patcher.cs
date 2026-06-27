using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using HarmonyLib;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    /// <summary>
    /// UI Info Suite 2 compatibility is intentionally height-only.
    /// Its buttons are already positioned by the mod; FIV only teaches it that the
    /// game menu is taller when the backpack has extra visible rows.
    /// </summary>
    internal class UiInfo2Patcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var original = AccessTools.Method(
                "UIInfoSuite2.UIElements.ShowCalendarAndBillboardOnGameMenuButton:DrawBillboard"
            );
            if (original is null)
            {
                Log.Warn("Could not find UIInfoSuite2 DrawBillboard method; skipping height patch.");
                return;
            }
            harmony.Patch(
                original: original,
                transpiler: this.GetHarmonyMethod(nameof(drawBillboardTranspiler))
            );
        }

        public static IEnumerable<CodeInstruction> drawBillboardTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (
                    codes[i].opcode == OpCodes.Ldfld
                    && ((FieldInfo)codes[i].operand).Name == "yPositionOnScreen"
                    && codes[i + 1].opcode == OpCodes.Call
                    && codes[i + 2].opcode == OpCodes.Ldfld
                    && ((FieldInfo)codes[i + 2].operand).Name == "height"
                    && codes[i + 3].opcode == OpCodes.Add
                )
                {
                    codes.Insert(
                        i + 4,
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(
                                typeof(InventoryGridMetrics),
                                nameof(InventoryGridMetrics.GetExtraHeight)
                            )
                        )
                    );
                    codes.Insert(i + 5, new CodeInstruction(OpCodes.Add));
                    break;
                }
            }
            return codes;
        }
    }
}
