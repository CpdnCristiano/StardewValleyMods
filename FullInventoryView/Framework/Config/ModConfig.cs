namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Config;

public sealed class ModConfig
{
    /// <summary>
    /// Whether FIV should add a controller-navigation proxy over RemoteFridgeStorage's native fridge toggle.
    /// FIV must not move or toggle the native RemoteFridgeStorage button directly.
    /// </summary>
    public bool EnableRemoteFridgeStorageCompatPatch { get; set; } = true;

    /// <summary>
    /// Whether FIV should shift Better Game Menu's Inventory tab upward to match the vanilla GameMenu
    /// vertical offset when the player's inventory is expanded.
    /// </summary>
    public bool EnableBetterGameMenuInventoryPagePositionPatch { get; set; } = true;

    /// <summary>
    /// Whether pressing up/down on the top/bottom visible row should scroll in the main InventoryPage.
    /// This only affects the player's main inventory page. Chest/shop menus keep edge-scroll enabled because
    /// they don't have the vanilla equipment/hat/ring navigation below the inventory grid.
    /// </summary>
    public bool EnableInventoryPageGamepadEdgeScroll { get; set; } = false;
}
