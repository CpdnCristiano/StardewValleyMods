using System.Reflection;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Reflection
{
    internal static class MenuComponentFinder
    {
        public static ClickableComponent? FindFieldContaining(object obj, string substring)
        {
            if (obj == null)
                return null;

            Type? type = obj.GetType();
            while (type != null)
            {
                foreach (
                    var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                )
                {
                    if (
                        typeof(ClickableComponent).IsAssignableFrom(field.FieldType)
                        && field.Name.Contains(substring, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        try
                        {
                            return field.GetValue(obj) as ClickableComponent;
                        }
                        catch { }
                    }
                }
                type = type.BaseType;
            }

            return null;
        }

        public static DiscreteColorPicker? FindColorPicker(IClickableMenu menu)
        {
            if (menu == null)
                return null;

            Type? type = menu.GetType();
            while (type != null)
            {
                foreach (
                    var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                )
                {
                    if (typeof(DiscreteColorPicker).IsAssignableFrom(field.FieldType))
                    {
                        try
                        {
                            return field.GetValue(menu) as DiscreteColorPicker;
                        }
                        catch { }
                    }
                }
                type = type.BaseType;
            }

            return null;
        }

        public static ClickableTextureComponent? FindColorPickerToggleButton(
            DiscreteColorPicker picker
        )
        {
            if (picker == null)
                return null;

            foreach (
                var field in typeof(DiscreteColorPicker).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
            )
            {
                if (
                    typeof(ClickableTextureComponent).IsAssignableFrom(field.FieldType)
                    && (
                        field.Name.Contains("toggle", StringComparison.OrdinalIgnoreCase)
                        || field.Name.Contains("button", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    try
                    {
                        var value = field.GetValue(picker) as ClickableTextureComponent;
                        if (value != null)
                            return value;
                    }
                    catch { }
                }
            }

            return null;
        }

        public static bool IsMouseTargetingInventoryArea(
            InventoryMenu menu,
            GridViewport? grid,
            int mouseX,
            int mouseY
        )
        {
            if (menu.inventory != null)
            {
                foreach (var slot in menu.inventory)
                {
                    if (slot?.containsPoint(mouseX, mouseY) == true)
                        return true;
                }
            }

            if (grid != null)
            {
                if (grid.UpArrow.containsPoint(mouseX, mouseY))
                    return true;
                if (grid.DownArrow.containsPoint(mouseX, mouseY))
                    return true;
                if (grid.ShowAuxScrollBar)
                {
                    if (grid.ScrollBarThumb.containsPoint(mouseX, mouseY))
                        return true;
                    if (grid.ScrollBarRunner.Contains(mouseX, mouseY))
                        return true;
                }
            }

            return false;
        }

        public static bool IsGamepadTargetingInventoryArea(
            IClickableMenu parentMenu,
            InventoryMenu inventoryMenu,
            GridViewport grid
        )
        {
            var snapped = parentMenu.currentlySnappedComponent;
            if (snapped == null)
                return false;

            if (inventoryMenu.inventory != null && inventoryMenu.inventory.Contains(snapped))
                return true;
            if (ReferenceEquals(snapped, grid.UpArrow) || ReferenceEquals(snapped, grid.DownArrow))
                return true;
            if (grid.ShowAuxScrollBar && ReferenceEquals(snapped, grid.ScrollBarThumb))
                return true;

            return false;
        }

        public static List<InventoryMenu> FindInventoryMenus(IClickableMenu menu)
        {
            var list = new List<InventoryMenu>();
            if (menu == null)
                return list;

            foreach (
                var field in menu.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            )
            {
                if (typeof(InventoryMenu).IsAssignableFrom(field.FieldType))
                {
                    var value = field.GetValue(menu) as InventoryMenu;
                    if (value != null)
                        list.Add(value);
                }
            }

            foreach (
                var prop in menu.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            )
            {
                if (typeof(InventoryMenu).IsAssignableFrom(prop.PropertyType))
                {
                    try
                    {
                        var value = prop.GetValue(menu) as InventoryMenu;
                        if (value != null)
                            list.Add(value);
                    }
                    catch { }
                }
            }

            return list;
        }
    }
}
