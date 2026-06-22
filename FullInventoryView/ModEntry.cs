using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
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

        helper.ConsoleCommands.Add("fit_size", "Altera o tamanho máximo do inventário do jogador. Exemplo: fit_size 60", this.SetInventorySize);
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
