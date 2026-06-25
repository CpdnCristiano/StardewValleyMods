using System.Collections.Generic;
using System.Linq;
using CpdnCristiano.StardewValleyMod.Common.Log;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Diagnostics
{
    internal static class LayoutDiagnostics
    {
        private static readonly Dictionary<string, string> LastMessages = new();

        public static void DebugChanged(string key, string message)
        {
            if (LastMessages.TryGetValue(key, out string? previous) && previous == message)
                return;

            LastMessages[key] = message;
            Log.Debug(message);
        }

        public static void Reset(string keyPrefix)
        {
            foreach (string key in LastMessages.Keys.Where(k => k.StartsWith(keyPrefix)).ToList())
                LastMessages.Remove(key);
        }
    }
}
