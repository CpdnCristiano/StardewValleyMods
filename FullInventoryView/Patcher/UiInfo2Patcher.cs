using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMods.Common.Log;
using CpdnCristiano.StardewValleyMods.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMods.FullInventoryView.Patcher
{
    internal class UiInfo2Patcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var original = AccessTools.Method(
                "UIInfoSuite2.UIElements.ShowCalendarAndBillboardOnGameMenuButton:DrawBillboard"
            );
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
                                typeof(InventoryMenuPatcher),
                                nameof(InventoryMenuPatcher.GetExtraHeight)
                            )
                        )
                    );
                    codes.Insert(i + 5, new CodeInstruction(OpCodes.Add));
                    Log.Debug("Patching UIInfoSuite2 to add extra height to the billboard");
                    break;
                }
            }
            return codes;
        }
    }
}
