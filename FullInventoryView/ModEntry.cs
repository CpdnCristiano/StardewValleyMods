using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView;

public class ModEntry : Mod
{
    public static ModEntry Instance { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Log.Init(Monitor);
        var patches = new List<IPatcher> { new InventoryMenuPatcher(), new CustomBackpackFrameworkCompatPatcher() };
        if (Helper.ModRegistry.IsLoaded("Annosz.UiInfoSuite2"))
        {
            patches.Add(new UiInfo2Patcher());
        }
        if (Helper.ModRegistry.IsLoaded("DazUki.UIInfoSuite2Alt"))
        {
            patches.Add(new UiInfo2AltPatcher());
        }
        HarmonyPatcher.Apply(this, patches.ToArray());
        helper.Events.Display.MenuChanged += this.OnMenuChanged;

        helper.ConsoleCommands.Add(
            "fit_size",
            "Altera o tamanho máximo do inventário do jogador. Exemplo: fit_size 60",
            this.SetInventorySize
        );
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (e.NewMenu is null)
            return;

        string typeChain = DescribeTypeChain(e.NewMenu.GetType());
        string interfaces = DescribeInterfaces(e.NewMenu.GetType());
        Log.Debug(
            $"[MenuProbe] changed old={e.OldMenu?.GetType().FullName ?? "<null>"}#{e.OldMenu?.GetHashCode().ToString() ?? "0"} -> new={e.NewMenu.GetType().FullName}#{e.NewMenu.GetHashCode()} inheritance={typeChain} interfaces={interfaces}"
        );
        InventoryMenuPatcher.NotifyMenuChanged(e.OldMenu, e.NewMenu);
    }

    private static string DescribeTypeChain(Type type)
    {
        var chain = new List<string>();
        Type? current = type;
        while (current != null)
        {
            chain.Add(current.FullName ?? current.Name);
            current = current.BaseType;
        }

        return string.Join(" -> ", chain);
    }

    private static string DescribeInterfaces(Type type)
    {
        var names = type.GetInterfaces().Select(p => p.FullName ?? p.Name).OrderBy(p => p).ToList();

        return names.Count > 0 ? string.Join(", ", names) : "<none>";
    }

    private void SetInventorySize(string command, string[] args)
    {
        if (StardewValley.Game1.player == null)
        {
            Log.Error("Nenhum save carregado! Carregue um save antes de rodar o comando.");
            return;
        }

        int requestedSize = 60; // tamanho padrão
        if (args.Length > 0 && int.TryParse(args[0], out int parsedSize))
        {
            requestedSize = parsedSize;
        }

        requestedSize = Math.Max(InventoryGridMetrics.DefaultColumnCount, requestedSize);
        int droppedItems = ResizePlayerInventoryToNativeSize(requestedSize);
        StardewValley.Game1.player.maxItems.Value = requestedSize;

        int rows = InventoryGridMetrics.GetRequiredRows(requestedSize, InventoryGridMetrics.DefaultColumnCount);
        if (droppedItems > 0)
        {
            Log.Warn($"Inventário reduzido para {requestedSize} slots; {droppedItems} stack(s) fora do novo limite foram jogados no chão.");
        }
        Log.Info($"Inventário alterado com sucesso para {requestedSize} slots ({rows} linhas)! Items.Count={StardewValley.Game1.player.Items.Count}, maxItems={StardewValley.Game1.player.maxItems.Value}");
    }

    private static int ResizePlayerInventoryToNativeSize(int requestedSize)
    {
        var player = StardewValley.Game1.player;
        var inventory = player.Items;
        int droppedItems = 0;

        if (inventory.Count > requestedSize)
        {
            for (int i = requestedSize; i < inventory.Count; i++)
            {
                Item? item = inventory[i];
                if (item == null)
                    continue;

                inventory[i] = null!;
                droppedItems++;
                try
                {
                    Vector2 origin = player.getStandingPosition();
                    Game1.createItemDebris(item, origin, player.FacingDirection, player.currentLocation);
                }
                catch
                {
                    // Fallback: if debris creation fails during a weird menu/location state,
                    // put it at the player's feet through the current location API instead of deleting it silently.
                    player.currentLocation?.debris.Add(new Debris(item, player.Position));
                }
            }
        }

        while (inventory.Count < requestedSize)
            inventory.Add(null!);
        while (inventory.Count > requestedSize)
            inventory.RemoveAt(inventory.Count - 1);

        return droppedItems;
    }
}
