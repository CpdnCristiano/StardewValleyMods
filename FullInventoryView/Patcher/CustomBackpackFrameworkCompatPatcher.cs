using System.Reflection;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using static StardewValley.Menus.InventoryMenu;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    /// <summary>
    /// Disables only the inventory/menu layout patches from Custom Backpack Framework when both mods are installed.
    ///
    /// That mod can still provide backpack sizes, API, shops, and save data. We only remove Harmony patches
    /// that rebuild InventoryMenu components, draw its own scroll UI, or override gamepad/menu navigation,
    /// because those conflict with Full Inventory View's virtualized inventory layout.
    /// </summary>
    internal sealed class CustomBackpackFrameworkCompatPatcher : BasePatcher
    {
        private const string CustomBackpackFrameworkModId = "platinummyr.CustomBackpackFramework";
        private static bool HasTriedGameLaunched;

        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            // Try immediately. If load order changes, try once again after all mods finish launching.
            TryDisableConflictingPatches(harmony, "Entry");
            ModEntry.Instance.Helper.Events.GameLoop.GameLaunched += (_, _) =>
            {
                if (HasTriedGameLaunched)
                    return;

                HasTriedGameLaunched = true;
                TryDisableConflictingPatches(harmony, "GameLaunched");
            };
        }

        private static void TryDisableConflictingPatches(Harmony harmony, string phase)
        {
            if (!ModEntry.Instance.Helper.ModRegistry.IsLoaded(CustomBackpackFrameworkModId))
                return;

            Log.Info(
                $"[CustomBackpackCompat] {CustomBackpackFrameworkModId} detected during {phase}; disabling its InventoryMenu layout/navigation patches."
            );

            UnpatchByOwner(
                harmony,
                AccessTools.Constructor(
                    typeof(InventoryMenu),
                    new Type[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                        typeof(IList<Item>),
                        typeof(highlightThisItem),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                    }
                ),
                "InventoryMenu ctor"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "hover", typeof(int), typeof(int), typeof(Item)),
                "InventoryMenu.hover"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(ShopMenu), "performHoverAction", typeof(int), typeof(int)),
                "ShopMenu.performHoverAction"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(ShopMenu), "receiveScrollWheelAction", typeof(int)),
                "ShopMenu.receiveScrollWheelAction"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "getInventoryPositionOfClick", typeof(int), typeof(int)),
                "InventoryMenu.getInventoryPositionOfClick"
            );

            // CBF patches these by name only, and Stardew versions/mod loaders may expose different optional parameters.
            // Resolve by name with a typed first attempt, then fallback to any method with the same name.
            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "leftClick", typeof(int), typeof(int), typeof(Item), typeof(bool), typeof(bool)),
                "InventoryMenu.leftClick"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "rightClick", typeof(int), typeof(int), typeof(Item), typeof(bool), typeof(bool)),
                "InventoryMenu.rightClick"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "setUpForGamePadMode"),
                "InventoryMenu.setUpForGamePadMode"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(IClickableMenu), "applyMovementKey", typeof(int)),
                "IClickableMenu.applyMovementKey"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(ItemGrabMenu), "customSnapBehavior", typeof(int), typeof(int), typeof(int)),
                "ItemGrabMenu.customSnapBehavior"
            );

            UnpatchByOwner(
                harmony,
                ResolveMethod(typeof(InventoryMenu), "draw", typeof(SpriteBatch), typeof(int), typeof(int), typeof(int)),
                "InventoryMenu.draw"
            );
        }

        private static MethodInfo? ResolveMethod(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo? exact = parameterTypes.Length > 0
                ? AccessTools.Method(type, name, parameterTypes)
                : AccessTools.Method(type, name, Type.EmptyTypes);

            if (exact is not null)
                return exact;

            // Some Stardew methods have optional parameters that are not stable across versions.
            // Match CBF's original behavior: AccessTools.Method(type, name), but do it without throwing.
            MethodInfo? byName = AccessTools.GetDeclaredMethods(type)
                .FirstOrDefault(method => method.Name.Equals(name, StringComparison.Ordinal));

            if (byName is not null)
                return byName;

            // Last-resort case-insensitive search just to avoid noisy false warnings from casing drift.
            return AccessTools.GetDeclaredMethods(type)
                .FirstOrDefault(method => method.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static void UnpatchByOwner(Harmony harmony, MethodBase? original, string label)
        {
            if (original is null)
            {
                // Debug only: missing methods can happen across Stardew versions and shouldn't spam/warn unless needed.
                Log.Debug($"[CustomBackpackCompat] Target method not found, skipped: {label}");
                return;
            }

            try
            {
                harmony.Unpatch(original, HarmonyPatchType.All, CustomBackpackFrameworkModId);
                Log.Debug($"[CustomBackpackCompat] Removed conflicting patches from {label}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[CustomBackpackCompat] Failed to unpatch {label}: {ex.Message}");
            }
        }
    }
}
