using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Api;

public interface IFullInventoryViewApi
{
    int ApiVersion { get; }

    void RegisterSideButton(
        IClickableMenu ownerMenu,
        ClickableComponent button,
        string sourceId,
        string displayName,
        Action? activate = null
    );

    void UnregisterSideButton(IClickableMenu ownerMenu, string sourceId);
}
