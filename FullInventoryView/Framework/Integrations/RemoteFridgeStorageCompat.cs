using System.Reflection;
using System.Runtime.CompilerServices;
using StardewModdingAPI;
using CpdnCristiano.StardewValleyMod.FullInventoryView;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Integrations;

internal static class RemoteFridgeStorageCompat
{
    private const string ChestControllerTypeName = "RemoteFridgeStorage.controller.ChestController";
    private const string SourceId = "RemoteFridgeStorage.FridgeToggleButton";
    private const int ProxyButtonId = 174500;

    public static Action<IClickableMenu, string>? RequestLayout { get; set; }

    private sealed class MenuState
    {
        public ClickableComponent? ProxyButton;
        public Rectangle? LastNativeBounds;
        public object? LastController;
    }

    private static readonly ConditionalWeakTable<IClickableMenu, MenuState> States = new();

    public static void Apply(Harmony harmony)
    {
        Type? chestControllerType = AccessTools.TypeByName(ChestControllerTypeName);
        if (chestControllerType == null)
            return;

        MethodInfo? updateButtonPosition = AccessTools.Method(chestControllerType, "UpdateButtonPosition");
        if (updateButtonPosition == null)
        {
            Log.Warn("[FIV/Compat] RemoteFridgeStorage detected, but ChestController.UpdateButtonPosition was not found.");
            return;
        }

        harmony.Patch(
            original: updateButtonPosition,
            postfix: new HarmonyMethod(typeof(RemoteFridgeStorageCompat), nameof(UpdateButtonPositionPostfix), new[] { typeof(object) })
            {
                priority = Priority.Last,
            }
        );

        Log.Debug("[FIV/Compat] patched RemoteFridgeStorage ChestController.UpdateButtonPosition postfix for gamepad proxy registration");
    }

    public static void UpdateButtonPositionPostfix(object __instance)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu menu)
            return;

        if (!ModEntry.Config.EnableRemoteFridgeStorageCompatPatch)
        {
            RemoveProxy(menu);
            return;
        }

        if (!TryReadInstanceField(__instance, "_openChest", out Chest? openChest) || openChest == null)
        {
            RemoveProxy(menu);
            return;
        }

        if (!TryReadInstanceField(__instance, "_fridgeSelected", out ClickableTextureComponent? selectedButton) || selectedButton == null)
            return;
        if (!TryReadInstanceField(__instance, "_fridgeDeselected", out ClickableTextureComponent? deselectedButton) || deselectedButton == null)
            return;

        bool selected = TryReadInstanceField(__instance, "_chests", out HashSet<Chest>? chests)
            && chests != null
            && chests.Contains(openChest);

        ClickableTextureComponent nativeButton = selected ? selectedButton : deselectedButton;
        Rectangle nativeBounds = nativeButton.bounds;
        if (nativeBounds.Width <= 0 || nativeBounds.Height <= 0)
            return;

        MenuState state = States.GetValue(menu, _ => new MenuState());
        bool created = false;
        if (state.ProxyButton == null)
        {
            state.ProxyButton = new ClickableComponent(nativeBounds, "remote-fridge-storage")
            {
                myID = ProxyButtonId,
            };
            created = true;
        }

        bool nativeMoved = !state.LastNativeBounds.HasValue || state.LastNativeBounds.Value != nativeBounds;
        if (created || nativeMoved || !ReferenceEquals(state.LastController, __instance))
        {
            state.ProxyButton.bounds = nativeBounds;
            state.LastNativeBounds = nativeBounds;
            state.LastController = __instance;
        }

        menu.allClickableComponents ??= new List<ClickableComponent>();
        if (!menu.allClickableComponents.Contains(state.ProxyButton))
            menu.allClickableComponents.Add(state.ProxyButton);

        // RemoteFridgeStorage's real selected/deselected buttons are private and manually drawn.
        // Keep the native button exactly where RemoteFridgeStorage put it; this proxy only overlays
        // those bounds so FIV can snap to it with a controller.
        ExternalSideButtonRegistry.Register(
            ownerMenu: menu,
            owner: __instance,
            button: state.ProxyButton,
            sourceId: SourceId,
            displayName: "RemoteFridgeStorage Fridge Toggle",
            activate: () => ActivateRemoteFridgeNativeHandleClick(__instance),
            allowLayout: false
        );

        if (created || nativeMoved)
        {
            Log.Debug(
                $"[FIV/Compat] registered RemoteFridgeStorage fixed-position fridge toggle proxy: menu={menu.GetType().Name}#{RuntimeHelpers.GetHashCode(menu)}, proxy={Describe(state.ProxyButton)}, native={Describe(nativeButton)}, created={created}, nativeMoved={nativeMoved}"
            );
            RequestLayout?.Invoke(menu, "RemoteFridgeStorage.UpdateButtonPosition");
        }
    }

    private static void RemoveProxy(ItemGrabMenu menu)
    {
        ExternalSideButtonRegistry.RemoveSource(menu, SourceId);

        if (States.TryGetValue(menu, out MenuState? state) && state.ProxyButton != null)
        {
            menu.allClickableComponents?.Remove(state.ProxyButton);
            state.ProxyButton = null;
            state.LastNativeBounds = null;
            state.LastController = null;
        }
    }

    private static void ActivateRemoteFridgeNativeHandleClick(object controller)
    {
        if (!TryReadInstanceField(controller, "_fridgeSelected", out ClickableTextureComponent? selectedButton) || selectedButton == null)
            return;

        Rectangle nativeBounds = selectedButton.bounds;
        if (nativeBounds.Width <= 0 || nativeBounds.Height <= 0)
            return;

        MethodInfo? handleClick = FindInstanceMethod(controller.GetType(), "HandleClick");
        if (handleClick == null)
            throw new MissingMethodException(controller.GetType().FullName, "HandleClick");

        Vector2 uiPoint = new(nativeBounds.Center.X, nativeBounds.Center.Y);
        Vector2 screenPixels = ConvertUiPointToScreenPixels(uiPoint);
        handleClick.Invoke(controller, new object[] { new ProxyCursorPosition(screenPixels) });
    }

    private static Vector2 ConvertUiPointToScreenPixels(Vector2 uiPoint)
    {
        MethodInfo? inverseScaleMethod = AccessTools.Method(typeof(Utility), "ModifyCoordinatesFromUIScale", new[] { typeof(Vector2) });
        if (inverseScaleMethod != null)
        {
            try
            {
                if (inverseScaleMethod.Invoke(null, new object[] { uiPoint }) is Vector2 rawPoint)
                    return rawPoint;
            }
            catch
            {
                // Fall through to the uiScale fallback below.
            }
        }

        float uiScale = Game1.options?.uiScale ?? 1f;
        return uiScale != 0f ? uiPoint * uiScale : uiPoint;
    }

    private sealed class ProxyCursorPosition : ICursorPosition
    {
        public ProxyCursorPosition(Vector2 screenPixels)
        {
            ScreenPixels = screenPixels;
            AbsolutePixels = new Vector2(screenPixels.X + Game1.viewport.X, screenPixels.Y + Game1.viewport.Y);
            Tile = new Vector2(AbsolutePixels.X / Game1.tileSize, AbsolutePixels.Y / Game1.tileSize);
            GrabTile = Tile;
        }

        public Vector2 AbsolutePixels { get; }
        public Vector2 ScreenPixels { get; }
        public Vector2 Tile { get; }
        public Vector2 GrabTile { get; }

        public Vector2 GetScaledAbsolutePixels()
        {
            Vector2 scaledScreen = GetScaledScreenPixels();
            return new Vector2(scaledScreen.X + Game1.viewport.X, scaledScreen.Y + Game1.viewport.Y);
        }

        public Vector2 GetScaledScreenPixels()
        {
            return Utility.ModifyCoordinatesForUIScale(ScreenPixels);
        }

        public bool Equals(ICursorPosition? other)
        {
            return other != null
                && AbsolutePixels.Equals(other.AbsolutePixels)
                && ScreenPixels.Equals(other.ScreenPixels)
                && Tile.Equals(other.Tile)
                && GrabTile.Equals(other.GrabTile);
        }

        public override bool Equals(object? obj)
        {
            return obj is ICursorPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AbsolutePixels, ScreenPixels, Tile, GrabTile);
        }
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
