namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public interface IItemResolver
    {
        bool TryResolve(string englishName, out string? localizedName);
    }

    public interface ILocationResolver
    {
        bool TryResolve(string englishName, out string? localizedName);
    }

    public interface IBuildingResolver
    {
        bool TryResolve(string englishName, out string? localizedName);
    }
}
