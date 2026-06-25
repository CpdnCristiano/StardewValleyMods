using System.Reflection;
using HarmonyLib;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Reflection
{
    internal static class StardewMenuFields
    {
        public static readonly FieldInfo Inventory = AccessTools.Field(
            typeof(InventoryMenu),
            "inventory"
        )!;

        public static readonly FieldInfo ActualInventory = AccessTools.Field(
            typeof(InventoryMenu),
            "actualInventory"
        )!;

        public static readonly FieldInfo InventoryPageInventory = AccessTools.Field(
            typeof(InventoryPage),
            "inventory"
        )!;

        public static readonly FieldInfo InventoryPageOrganizeButton = AccessTools.Field(
            typeof(InventoryPage),
            "organizeButton"
        )!;

        public static readonly FieldInfo ItemGrabMenuColorPicker = AccessTools.Field(
            typeof(ItemGrabMenu),
            "colorPicker"
        )!;

        public static readonly FieldInfo DiscreteColorPickerToggleButton = AccessTools.Field(
            typeof(DiscreteColorPicker),
            "colorPickerToggleButton"
        )!;

        public static readonly FieldInfo ShopMenuScrollBarRunner = AccessTools.Field(
            typeof(ShopMenu),
            "scrollBarRunner"
        )!;
    }
}
