using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Interface;
using static CS2MenuManager.API.Class.ConfigManager;
using static CS2MenuManager.API.Class.Database;
using static CS2MenuManager.API.Class.MenuManager;

namespace CS2MenuManager.API.Class;

/// <summary>
/// Provides centralized management of menu types for players in a CS2 plugin.
/// </summary>
/// <remarks>
/// This static class handles the registration, retrieval, and switching between different
/// menu implementations for individual players. It supports both in-memory and database-backed
/// storage of player preferences.
/// </remarks>
public static class MenuTypeManager
{
    private static readonly Dictionary<ulong, Type> MenuTypes = [];

    // SteamIDs with a preference lookup in flight, so a burst of menu opens does not queue one
    // query per open.
    private static readonly HashSet<ulong> PendingLookups = [];

    internal static void CleanupPlayer(ulong steamId)
    {
        MenuTypes.Remove(steamId);
        PendingLookups.Remove(steamId);
    }

    internal static void CleanupAll()
    {
        MenuTypes.Clear();
        PendingLookups.Clear();
    }

    /// <summary>
    /// Warms the menu-type cache for a player from the database, off the game thread.
    /// Called when a client authorizes, so the preference is in memory long before the player
    /// can open a menu. No-op unless MySQL is configured — the local store is already in memory.
    /// </summary>
    internal static void Prefetch(ulong steamId)
    {
        if (!IsMYSQLSet || steamId == 0)
            return;

        if (MenuTypes.ContainsKey(steamId) || !PendingLookups.Add(steamId))
            return;

        Task.Run(async () =>
        {
            string? type = null;
            try
            {
                type = await SelectMenu(steamId);
            }
            catch
            {
                // Fall through: the player keeps the default menu for this session.
            }

            // MenuTypes is game-thread state; do not write it from the worker.
            Server.NextFrame(() =>
            {
                PendingLookups.Remove(steamId);

                if (string.IsNullOrEmpty(type))
                    return;

                MenuTypes[steamId] = MenuTypesList.TryGetValue(type, out Type? resolved) ? resolved : GetDefaultMenu();
            });
        });
    }

    /// <summary>
    /// Gets the default menu type to use when no specific preference is set.
    /// </summary>
    /// <returns>The <see cref="Type"/> representing the default menu implementation.</returns>
    public static Type GetDefaultMenu()
    {
        return MenuTypesList[Config.DefaultMenuType];
    }

    /// <summary>
    /// Retrieves the preferred menu type for a specific player.
    /// </summary>
    /// <param name="player">The player controller to look up.</param>
    /// <returns>
    /// The player's preferred menu type if known; otherwise the default menu type.
    /// </returns>
    /// <remarks>
    /// This never blocks. The MySQL path used to sit on
    /// <c>SelectMenu(...).GetAwaiter().GetResult()</c>, which stalled the game thread on a
    /// database round trip every time a player opened a menu — a server-wide freeze visible to
    /// everyone as a lag spike. The preference is loaded once by <see cref="Prefetch"/> when the
    /// client authorizes; if it has not landed yet (or the query failed) the default menu is
    /// returned and the lookup is kicked off in the background for next time. The local store is
    /// already in memory, so that path is unchanged.
    /// </remarks>
    public static Type? GetPlayerMenuType(CCSPlayerController player)
    {
        if (MenuTypes.TryGetValue(player.SteamID, out Type? menuType))
            return menuType;

        if (IsMYSQLSet)
        {
            Prefetch(player.SteamID);
            return GetDefaultMenu();
        }

        string? type = LocalStore.Select(player.SteamID);

        if (string.IsNullOrEmpty(type))
            return GetDefaultMenu();

        MenuTypes[player.SteamID] = MenuTypesList.TryGetValue(type, out menuType) ? menuType : GetDefaultMenu();

        return menuType;
    }

    /// <summary>
    /// Sets the preferred menu type for a specific player.
    /// </summary>
    /// <param name="player">The player controller to update.</param>
    /// <param name="menuType">The menu types to set as preferred.</param>
    /// <returns>
    /// The menu type that was set or the default menu type if the specified type wasn't valid.
    /// Will update the database if MySQL is configured.
    /// </returns>
    public static Type SetPlayerMenuType(CCSPlayerController player, Type menuType)
    {
        MenuTypes[player.SteamID] = menuType;

        string menuTypeStr = MenuTypesList.FirstOrDefault(x => x.Value == menuType).Key;

        if (menuTypeStr == null)
            return GetDefaultMenu();

        if (IsMYSQLSet)
            Task.Run(async () => await InsertMenu(player.SteamID, menuTypeStr));
        else
            LocalStore.Insert(player.SteamID, menuTypeStr);

        return menuType;
    }

    /// <summary>
    /// Creates a menu selection interface for choosing between available menu types.
    /// </summary>
    /// <typeparam name="T">The type of menu to create.</typeparam>
    /// <param name="player">The player controller this menu is for.</param>
    /// <param name="plugin">The plugin instance that owns this menu.</param>
    /// <param name="prevMenu">The previous menu to return to after selection.</param>
    public static void MenuTypeMenu<T>(CCSPlayerController player, BasePlugin plugin, IMenu? prevMenu) where T : BaseMenu
    {
        T menu = CreateMenu<T>(player.Localizer("SelectMenuType"), plugin);
        AddMenuTypeMenuItems(menu, prevMenu);
    }

    /// <summary>
    /// Creates a menu selection interface for choosing between available menu types using a specific menu implementation.
    /// </summary>
    /// <param name="type">The type of menu to instantiate.</param>
    /// <param name="player">The player controller this menu is for.</param>
    /// <param name="plugin">The plugin instance that owns this menu.</param>
    /// <param name="prevMenu">The previous menu to return to after selection.</param>
    /// <returns>The created menu instance.</returns>
    public static BaseMenu MenuTypeMenuByType(Type type, CCSPlayerController player, BasePlugin plugin, IMenu? prevMenu)
    {
        BaseMenu menu = MenuByType(type, player.Localizer("SelectMenuType"), plugin);
        AddMenuTypeMenuItems(menu, prevMenu);
        return menu;
    }

    /// <summary>
    /// Creates a menu selection interface for choosing between available menu types using a specific menu type name.
    /// </summary>
    /// <param name="type">The name of the menu type to instantiate.</param>
    /// <param name="player">The player controller this menu is for.</param>
    /// <param name="plugin">The plugin instance that owns this menu.</param>
    /// <param name="prevMenu">The previous menu to return to after selection.</param>
    /// <returns>The created menu instance.</returns>
    public static BaseMenu MenuTypeMenuByType(string type, CCSPlayerController player, BasePlugin plugin, IMenu? prevMenu)
    {
        BaseMenu menu = MenuByType(type, player.Localizer("SelectMenuType"), plugin);
        AddMenuTypeMenuItems(menu, prevMenu);
        return menu;
    }

    /// <summary>
    /// Adds menu type selection items to an existing menu instance.
    /// </summary>
    /// <param name="menu">The menu to add items to.</param>
    /// <param name="prevMenu">The previous menu to return to after selection.</param>
    public static void AddMenuTypeMenuItems(BaseMenu menu, IMenu? prevMenu)
    {
        foreach (KeyValuePair<string, Type> menuType in MenuTypesList)
        {
            menu.AddItem(menuType.Key, (p, _) =>
            {
                SetPlayerMenuType(p, menuType.Value);
                prevMenu?.Display(p, GetActiveMenu(p)?.MenuTime ?? 0);
            });
        }
    }
}
