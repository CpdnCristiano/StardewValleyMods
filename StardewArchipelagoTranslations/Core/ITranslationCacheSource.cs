namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal interface ITranslationCacheSource
    {
        string Name { get; }
        void WarmUp();
        void Clear();
    }
}
