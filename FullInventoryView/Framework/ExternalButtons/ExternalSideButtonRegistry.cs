using System.Runtime.CompilerServices;
using CpdnCristiano.StardewValleyMod.Common.Log;
using Microsoft.Xna.Framework.Input;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;

internal static class ExternalSideButtonRegistry
{
    private sealed class MenuExternalButtonState
    {
        public List<ExternalSideButton> Buttons { get; } = new();
    }

    private static readonly ConditionalWeakTable<IClickableMenu, MenuExternalButtonState> RegisteredButtons = new();

    public static void Register(
        IClickableMenu ownerMenu,
        object owner,
        ClickableComponent button,
        string sourceId,
        string displayName,
        Action? activate,
        bool allowLayout = true
    )
    {
        var state = RegisteredButtons.GetValue(ownerMenu, _ => new MenuExternalButtonState());
        state.Buttons.RemoveAll(p => p.SourceId == sourceId || ReferenceEquals(p.Button, button));
        state.Buttons.Add(new ExternalSideButton(ownerMenu, owner, button, sourceId, displayName, activate, allowLayout));
    }

    public static void RemoveSource(IClickableMenu ownerMenu, string sourceId)
    {
        if (RegisteredButtons.TryGetValue(ownerMenu, out var state))
            state.Buttons.RemoveAll(p => p.SourceId == sourceId);
    }

    public static IReadOnlyList<ExternalSideButton> Get(IClickableMenu ownerMenu)
    {
        return RegisteredButtons.TryGetValue(ownerMenu, out var state)
            ? state.Buttons
            : Array.Empty<ExternalSideButton>();
    }

    public static bool IsLayoutDisabled(IClickableMenu ownerMenu, ClickableComponent button)
    {
        return RegisteredButtons.TryGetValue(ownerMenu, out var state)
            && state.Buttons.Any(entry => ReferenceEquals(entry.Button, button) && !entry.AllowLayout);
    }

    public static IReadOnlyList<ExternalSideButton> GetFixedPositionButtons(IClickableMenu ownerMenu)
    {
        if (!RegisteredButtons.TryGetValue(ownerMenu, out var state))
            return Array.Empty<ExternalSideButton>();

        return state.Buttons.Where(entry => !entry.AllowLayout).ToList();
    }

    public static bool TryActivateSnapped(IClickableMenu ownerMenu, Buttons button, Buttons activationButton = Buttons.A)
    {
        if (button != activationButton || ownerMenu.currentlySnappedComponent == null)
            return false;

        foreach (var entry in Get(ownerMenu))
        {
            if (entry.Activate == null || !ReferenceEquals(ownerMenu.currentlySnappedComponent, entry.Button))
                continue;

            try
            {
                entry.Activate();
                Log.Debug(
                    $"[FIV/ExternalButton] activated by gamepad: source={entry.SourceId}, display={entry.DisplayName}, menu={ownerMenu.GetType().Name}#{RuntimeHelpers.GetHashCode(ownerMenu)}, button={Describe(entry.Button)}"
                );
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"[FIV/ExternalButton] failed to activate {entry.SourceId}: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private static string Describe(ClickableComponent? component)
    {
        if (component == null)
            return "<null>";

        string name = string.IsNullOrWhiteSpace(component.name) ? "<no-name>" : component.name;
        var bounds = component.bounds;
        return $"id={component.myID}, name={name}, x={bounds.X}, y={bounds.Y}, w={bounds.Width}, h={bounds.Height}";
    }
}
