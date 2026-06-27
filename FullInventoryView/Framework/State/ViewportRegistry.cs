using System.Runtime.CompilerServices;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.State;

/// <summary>
/// Owns the relationship between vanilla <see cref="InventoryMenu"/> instances and their managed
/// <see cref="GridViewport"/> state.
/// </summary>
internal static class ViewportRegistry
{
    private static readonly ConditionalWeakTable<InventoryMenu, GridViewport> Viewports = new();

    public static GridViewport GetOrCreate(InventoryMenu menu)
    {
        return Viewports.GetValue(menu, static m => new GridViewport(m));
    }

    public static bool TryGet(InventoryMenu menu, out GridViewport? viewport)
    {
        return Viewports.TryGetValue(menu, out viewport);
    }

    public static GridViewport? Get(InventoryMenu menu)
    {
        return TryGet(menu, out var viewport) ? viewport : null;
    }

    public static bool IsManaged(InventoryMenu menu)
    {
        return Viewports.TryGetValue(menu, out _);
    }
}
