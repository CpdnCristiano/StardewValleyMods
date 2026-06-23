using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView;

public class ModEntry : Mod
{
    public static ModEntry Instance { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Log.Init(Monitor);
        var patches = new List<IPatcher> { new InventoryMenuPatcher() };
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

        helper.ConsoleCommands.Add("fit_size", "Altera o tamanho máximo do inventário do jogador. Exemplo: fit_size 60", this.SetInventorySize);
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (e.NewMenu is null)
            return;

        string typeChain = DescribeTypeChain(e.NewMenu.GetType());
        string interfaces = DescribeInterfaces(e.NewMenu.GetType());
        Log.Debug($"[MenuProbe] opened={e.NewMenu.GetType().FullName} inheritance={typeChain} interfaces={interfaces}");
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
        var names = type
            .GetInterfaces()
            .Select(p => p.FullName ?? p.Name)
            .OrderBy(p => p)
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : "<none>";
    }

    private void SetInventorySize(string command, string[] args)
    {
        if (StardewValley.Game1.player == null)
        {
            this.Monitor.Log("Nenhum save carregado! Carregue um save antes de rodar o comando.", LogLevel.Error);
            return;
        }

        int size = 60; // tamanho padrão
        if (args.Length > 0 && int.TryParse(args[0], out int parsedSize))
        {
            size = parsedSize;
        }

        StardewValley.Game1.player.maxItems.Value = size;
        this.Monitor.Log($"Inventário alterado com sucesso para {size} slots ({size / 12} linhas)!", LogLevel.Info);
    }
}
