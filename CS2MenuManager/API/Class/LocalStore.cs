using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API;

namespace CS2MenuManager.API.Class;

/// <summary>
/// File-backed fallback store for per-player menu type preferences, used when MySQL is not
/// configured. Persists a SteamID -> menu type name map as JSON next to the plugin config so
/// preferences survive reconnects, map changes and server restarts without a database.
/// </summary>
internal static class LocalStore
{
    private static readonly string StoreFilePath;
    private static readonly ConcurrentDictionary<ulong, string> Prefs = new();
    private static readonly object WriteLock = new();

    static LocalStore()
    {
        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        string dir = Path.Combine(
            Server.GameDirectory,
            "csgo",
            "addons",
            "counterstrikesharp",
            "shared",
            assemblyName
        );
        StoreFilePath = Path.Combine(dir, "menu_prefs.json");

        Load();
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(StoreFilePath))
                return;

            string json = File.ReadAllText(StoreFilePath);
            Dictionary<ulong, string>? data = JsonSerializer.Deserialize<Dictionary<ulong, string>>(json);
            if (data == null)
                return;

            foreach (KeyValuePair<ulong, string> kvp in data)
                Prefs[kvp.Key] = kvp.Value;
        }
        catch
        {
            // Corrupt/unreadable store must not crash the plugin; start empty.
        }
    }

    /// <summary>
    /// Returns the stored menu type name for the given SteamID, or null if none is set.
    /// </summary>
    public static string? Select(ulong steamId)
        => Prefs.TryGetValue(steamId, out string? menu) ? menu : null;

    /// <summary>
    /// Stores the menu type name for the given SteamID and flushes the store to disk.
    /// </summary>
    public static void Insert(ulong steamId, string menu)
    {
        Prefs[steamId] = menu;
        Save();
    }

    private static void Save()
    {
        lock (WriteLock)
        {
            try
            {
                string json = JsonSerializer.Serialize(Prefs, new JsonSerializerOptions { WriteIndented = true });
                string? dir = Path.GetDirectoryName(StoreFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string tmp = StoreFilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, StoreFilePath, overwrite: true);
            }
            catch
            {
                // Best-effort persistence; a failed write should not break menu switching.
            }
        }
    }
}
