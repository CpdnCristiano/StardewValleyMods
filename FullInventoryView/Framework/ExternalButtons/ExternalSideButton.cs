using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;

internal sealed class ExternalSideButton
{
    public ExternalSideButton(
        IClickableMenu ownerMenu,
        object owner,
        ClickableComponent button,
        string sourceId,
        string displayName,
        Action? activate,
        bool allowLayout
    )
    {
        OwnerMenu = ownerMenu;
        Owner = owner;
        Button = button;
        SourceId = sourceId;
        DisplayName = displayName;
        Activate = activate;
        AllowLayout = allowLayout;
    }

    public IClickableMenu OwnerMenu { get; }
    public object Owner { get; }
    public ClickableComponent Button { get; }
    public string SourceId { get; }
    public string DisplayName { get; }
    public Action? Activate { get; }
    public bool AllowLayout { get; }
}
