using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;

/// <summary>
/// Central place for directional gamepad graph operations.
/// </summary>
internal static class NavigationGraphBuilder
{
    public static void WireSideColumns(
        List<ClickableComponent> slotColumn,
        List<ClickableComponent> sideButtons,
        int startingDynamicId,
        bool buttonsAreOnLeft,
        bool wrapOutwardToDrop = false
    )
    {
        GridViewportLayoutHelpers.WireSideColumnsNavigation(
            slotColumn,
            sideButtons,
            startingDynamicId,
            buttonsAreOnLeft,
            wrapOutwardToDrop
        );
    }
}
