using System.Reflection;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;
using HarmonyLib;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Integrations;

internal static class ChestsAnywhereSortButtonCompat
{
    private const string ChestOverlayTypeName = "Pathoschild.Stardew.ChestsAnywhere.Menus.Overlays.ChestOverlay";
    private const string SourceId = "Pathoschild.ChestsAnywhere.SortInventoryButton";

    public static Action<IClickableMenu, string>? RequestLayout { get; set; }

    public static void Apply(Harmony harmony)
    {
        Type? chestOverlayType = AccessTools.TypeByName(ChestOverlayTypeName);
        if (chestOverlayType == null)
            return;

        MethodInfo? reinitialize = AccessTools.Method(chestOverlayType, "ReinitializeComponents");
        if (reinitialize == null)
            return;

        harmony.Patch(
            original: reinitialize,
            postfix: new HarmonyMethod(typeof(ChestsAnywhereSortButtonCompat), nameof(ReinitializeComponentsPostfix), new[] { typeof(object) })
            {
                priority = Priority.Last,
            }
        );

        Log.Debug("[FIV/Compat] patched ChestsAnywhere ChestOverlay.ReinitializeComponents for SortInventoryButton registration");
    }

    public static void ReinitializeComponentsPostfix(object __instance)
    {
        TrackSortInventoryButton(__instance, "ChestsAnywhere.ReinitializeComponents");
    }

    private static void TrackSortInventoryButton(object overlay, string reason)
    {
        if (!TryReadInstanceField(overlay, "Menu", out IClickableMenu? menu) || menu == null)
            return;

        if (!TryReadInstanceField(overlay, "SortInventoryButton", out ClickableComponent? sortButton) || sortButton == null)
        {
            ExternalSideButtonRegistry.RemoveSource(menu, SourceId);
            return;
        }

        ExternalSideButtonRegistry.Register(
            ownerMenu: menu,
            owner: overlay,
            button: sortButton,
            sourceId: SourceId,
            displayName: "ChestsAnywhere SortInventoryButton",
            activate: () => InvokeSortInventory(overlay)
        );

        EnsureOnlyLiveSortButtonInMenu(menu, sortButton);

        Log.Debug($"[FIV/Compat] registered ChestsAnywhere SortInventoryButton: reason={reason}, menu={menu.GetType().Name}#{menu.GetHashCode()}, button={Describe(sortButton)}, owner={overlay.GetType().FullName}");
        RequestLayout?.Invoke(menu, reason);
    }

    private static void EnsureOnlyLiveSortButtonInMenu(IClickableMenu menu, ClickableComponent sortButton)
    {
        menu.allClickableComponents ??= new List<ClickableComponent>();

        int before = menu.allClickableComponents.Count;
        menu.allClickableComponents.RemoveAll(component =>
            component != null
            && !ReferenceEquals(component, sortButton)
            && string.Equals(component.name, "sort-inventory", StringComparison.OrdinalIgnoreCase)
        );

        if (!menu.allClickableComponents.Contains(sortButton))
            menu.allClickableComponents.Add(sortButton);

        int removed = before - menu.allClickableComponents.Count;
        if (removed > 0)
            Log.Debug($"[FIV/Compat] pruned stale ChestsAnywhere SortInventoryButton refs: removed={removed}, live={Describe(sortButton)}, components={menu.allClickableComponents.Count}");
    }

    private static void InvokeSortInventory(object overlay)
    {
        MethodInfo? sortInventoryMethod = FindInstanceMethod(overlay.GetType(), "SortInventory");
        if (sortInventoryMethod == null)
            throw new MissingMethodException(overlay.GetType().FullName, "SortInventory");

        sortInventoryMethod.Invoke(overlay, null);
    }

    private static MethodInfo? FindInstanceMethod(Type? type, string methodName)
    {
        while (type != null && type != typeof(object))
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            );
            if (method != null)
                return method;

            type = type.BaseType;
        }

        return null;
    }

    private static bool TryReadInstanceField<T>(object instance, string fieldName, out T? value)
    {
        value = default;
        Type? type = instance.GetType();
        while (type != null && type != typeof(object))
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                try
                {
                    object? raw = field.GetValue(instance);
                    if (raw is T typed)
                    {
                        value = typed;
                        return true;
                    }

                    return raw == null;
                }
                catch
                {
                    return false;
                }
            }

            type = type.BaseType;
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
