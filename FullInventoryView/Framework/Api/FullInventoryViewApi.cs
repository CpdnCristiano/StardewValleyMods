using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Api;

public sealed class FullInventoryViewApi : IFullInventoryViewApi
{
    public int ApiVersion => 1;

    public void RegisterSideButton(
        IClickableMenu ownerMenu,
        ClickableComponent button,
        string sourceId,
        string displayName,
        Action? activate = null
    )
    {
        ExternalSideButtonRegistry.Register(
            ownerMenu: ownerMenu,
            owner: ownerMenu,
            button: button,
            sourceId: sourceId,
            displayName: displayName,
            activate: activate
        );

        InventoryMenuPatcher.NotifyExternalButtonRegistered(ownerMenu, sourceId);
    }

    public void UnregisterSideButton(IClickableMenu ownerMenu, string sourceId)
    {
        ExternalSideButtonRegistry.RemoveSource(ownerMenu, sourceId);
        InventoryMenuPatcher.NotifyExternalButtonRegistered(ownerMenu, sourceId);
    }
}
