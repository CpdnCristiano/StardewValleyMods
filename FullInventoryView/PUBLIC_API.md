# Full Inventory View API draft

This is the first small public API surface. It does **not** expose `GridViewport` directly.

```csharp
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
```

Example for another mod:

```csharp
var api = helper.ModRegistry.GetApi<IFullInventoryViewApi>("CpdnCristiano.FullInventoryView");
api?.RegisterSideButton(
    ownerMenu: this.Menu,
    button: this.MyButton,
    sourceId: "Author.ModId.MyButton",
    displayName: "My Button",
    activate: () => this.DoSomething()
);
```

The registry lets Full Inventory View place and wire the button with the same side-button layout system used by vanilla and compatibility buttons.
