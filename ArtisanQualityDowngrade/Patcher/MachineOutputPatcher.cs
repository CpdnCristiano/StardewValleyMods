using System;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;

namespace CpdnCristiano.StardewValleyMod.ArtisanQualityDowngrade.Patcher
{
    internal class MachineOutputPatcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            var originalMethod = AccessTools.Method(typeof(MachineDataUtility), nameof(MachineDataUtility.GetOutputItem));
            if (originalMethod == null)
            {
                throw new InvalidOperationException("Can't find method MachineDataUtility.GetOutputItem");
            }

            harmony.Patch(
                original: originalMethod,
                postfix: this.GetHarmonyMethod(nameof(getOutputItem_Postfix))
            );
        }

        public static void getOutputItem_Postfix(Item __result, MachineItemOutput? outputData, Item inputItem)
        {
            // Filtra apenas produtos artesanais (Categoria -26) e ingredientes válidos
            if (__result is StardewValley.Object artisan
                && inputItem is StardewValley.Object ingredient
                && artisan.Category == StardewValley.Object.artisanGoodsCategory)
            {
                int inputQuality = GetQualityTier(ingredient.Quality);
                int baseQuality = outputData != null ? GetQualityTier(outputData.Quality) : 0;

                // Reduz em 1 nível a qualidade do ingrediente (mínimo de 0)
                int extraQuality = Math.Max(0, inputQuality - 1);

                // Soma a qualidade base da máquina com o bônus do ingrediente reduzido (máximo de 3/Irídio)
                int finalQuality = Math.Min(3, baseQuality + extraQuality);

                artisan.Quality = GetQualityFromTier(finalQuality);
            }
        }

        private static int GetQualityTier(int quality) => quality switch
        {
            4 => 3,
            _ => quality
        };

        private static int GetQualityFromTier(int tier) => tier switch
        {
            3 => 4,
            _ => tier
        };
    }
}
