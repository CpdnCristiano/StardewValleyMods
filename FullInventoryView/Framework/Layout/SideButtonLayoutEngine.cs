using System;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;

/// <summary>
/// Facade for side-button placement and cache operations. Keeping the public operations here
/// prevents Harmony patches from depending directly on every detail of <see cref="GridViewport"/>.
/// </summary>
internal static class SideButtonLayoutEngine
{
    public static void Layout(GridViewport viewport, GridViewport.SideButtonLayoutContext context)
    {
        viewport.LayoutSideButtons(context);
    }

    public static bool HasUncachedBottomCandidates(GridViewport viewport, GridViewport.SideButtonLayoutContext context)
    {
        return viewport.HasUncachedBottomSideButtonCandidates(context);
    }

    public static bool ApplyCachedTargets(
        GridViewport viewport,
        IClickableMenu menu,
        string reason,
        Func<ClickableComponent, bool>? canApply = null
    )
    {
        return viewport.ApplyCachedSideButtonTargets(menu, reason, canApply);
    }
}
