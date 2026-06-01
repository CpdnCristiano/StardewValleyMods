using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal class UiInfo2AltPatcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var original = AccessTools.Method(
                "UIInfoSuite2Alt.UIElements.ShowCalendarAndBillboardOnGameMenuButton:DrawBillboard"
            );
            if (original is null)
            {
                monitor.Log("Could not find UIInfoSuite2Alt DrawBillboard method; skipping patch.", LogLevel.Warn);
                return;
            }
            harmony.Patch(
                original: original,
                transpiler: this.GetHarmonyMethod(nameof(drawBillboardTranspiler))
            );
        }

        public static IEnumerable<CodeInstruction> drawBillboardTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo fieldInfo && fieldInfo.Name == "height" &&
                    codes[i + 1].opcode == OpCodes.Add &&
                    codes[i + 3].opcode == OpCodes.Sub)
                {
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InventoryMenuPatcher), nameof(InventoryMenuPatcher.GetBillboardOffset))));
                    codes.Insert(i + 5, new CodeInstruction(OpCodes.Add));

                    Log.Debug("Patching UIInfoSuite2Alt to add extra height to the billboard");
                    break;
                }
            }

            return codes;
        }
    }
}
