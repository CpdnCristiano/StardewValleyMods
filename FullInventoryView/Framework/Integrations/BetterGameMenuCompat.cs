using System.Collections.Generic;
using System.Reflection;
using CpdnCristiano.StardewValleyMod.FullInventoryView;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using HarmonyLib;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Integrations;

internal static class BetterGameMenuCompat
{
    private const string BetterGameMenuTypeName = "Leclair.Stardew.BetterGameMenu.Menus.BetterGameMenuImpl";
    private const string InventoryTabName = "Inventory";

    public static void Apply(Harmony harmony)
    {
        Type? menuType = AccessTools.TypeByName(BetterGameMenuTypeName);
        if (menuType == null)
            return;

        MethodInfo? resizeMenu = AccessTools.Method(menuType, "ResizeMenu", new[] { typeof(string) });
        if (resizeMenu == null)
        {
            Log.Warn("[FIV/Compat] Better Game Menu detected, but BetterGameMenuImpl.ResizeMenu(string?) was not found.");
            return;
        }

        harmony.Patch(
            original: resizeMenu,
            postfix: new HarmonyMethod(typeof(BetterGameMenuCompat), nameof(ResizeMenuPostfix), new[] { typeof(object), typeof(string) })
            {
                priority = Priority.Last,
            }
        );

        Log.Debug("[FIV/Compat] patched Better Game Menu ResizeMenu postfix for InventoryPage vertical offset");
    }

    public static void ResizeMenuPostfix(object __instance, string? tab)
    {
        if (!ModEntry.Config.EnableBetterGameMenuInventoryPagePositionPatch)
            return;

        if (!string.Equals(tab, InventoryTabName, StringComparison.Ordinal))
            return;

        if (__instance is not IClickableMenu menu)
            return;

        if (!InventoryGridMetrics.PlayerHasExpandedInventory())
            return;

        int extraHeight = InventoryGridMetrics.GetExtraHeight();
        if (extraHeight <= 0)
            return;

        int offset = extraHeight / 2;

        // Better Game Menu positions its own frame, tabs, close button, and then creates
        // the current page from this wrapper menu's x/y/width/height. Moving only the
        // InventoryPage leaves BGM's tabs and close button at the old coordinates. Treat
        // the wrapper itself like vanilla GameMenu: make the whole menu taller and move
        // the whole frame upward, then ask BGM to recalculate its tab/close components.
        menu.height += extraHeight;
        menu.yPositionOnScreen -= offset;

        InvokeNoArg(__instance, "RepositionTabs");
        menu.initializeUpperRightCloseButton();
        ResizeCachedInventoryPageIfPresent(__instance, menu, tab);

        Log.Debug($"[FIV/Compat] Better Game Menu inventory wrapper resized: extraHeight={extraHeight}, offset={offset}, menu={menu.GetType().Name}#{menu.GetHashCode()}, y={menu.yPositionOnScreen}, height={menu.height}");
    }

    private static void InvokeNoArg(object instance, string methodName)
    {
        MethodInfo? method = AccessTools.Method(instance.GetType(), methodName);
        method?.Invoke(instance, null);
    }

    private static void ResizeCachedInventoryPageIfPresent(object instance, IClickableMenu wrapper, string tab)
    {
        if (!TryReadInstanceField(instance, "TabPages", out IDictionary<string, IClickableMenu>? pages) || pages == null)
            return;

        if (!pages.TryGetValue(tab, out IClickableMenu? page) || page is not InventoryPage)
            return;

        page.xPositionOnScreen = wrapper.xPositionOnScreen;
        page.yPositionOnScreen = wrapper.yPositionOnScreen;
        page.width = wrapper.width;
        page.height = wrapper.height;
        page.populateClickableComponentList();
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
                object? raw = field.GetValue(instance);
                if (raw is T typed)
                {
                    value = typed;
                    return true;
                }

                return raw == null;
            }

            type = type.BaseType;
        }

        return false;
    }
}
