using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Interface;
using CS2MenuManager.API.Menu;
using static CounterStrikeSharp.API.Core.Listeners;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2MenuManager.API.Class;

/// <summary>
/// Manages the active menus for players.
/// </summary>
public static class MenuManager
{
    private static readonly Dictionary<ulong, (IMenuInstance Instance, Timer? Timer)> ActiveMenus = [];
    private static bool _hooksInstalled;
    private static readonly object InitLock = new();

    private static void EnsureHooks(BasePlugin plugin)
    {
        if (_hooksInstalled)
            return;

        lock (InitLock)
        {
            if (_hooksInstalled)
                return;

            plugin.RegisterListener<OnMapStart>(_ => CleanupAll());
            plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            _hooksInstalled = true;
        }
    }

    private static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not { } player)
            return HookResult.Continue;

        ulong steamId = player.SteamID;
        CleanupPlayer(steamId);
        return HookResult.Continue;
    }

    internal static void CleanupPlayer(ulong steamId)
    {
        if (ActiveMenus.TryGetValue(steamId, out (IMenuInstance Instance, Timer? Timer) value))
        {
            value.Timer?.Kill();
            ActiveMenus.Remove(steamId);
        }

        MenuTypeManager.CleanupPlayer(steamId);
        WasdMenuInstance.CleanupPlayerState(steamId);
    }

    internal static void CleanupAll()
    {
        foreach ((IMenuInstance _, Timer? Timer) in ActiveMenus.Values)
            Timer?.Kill();

        ActiveMenus.Clear();
        MenuTypeManager.CleanupAll();
        WasdMenuInstance.CleanupAllStates();
    }

    /// <summary>
    /// A static readonly dictionary that maps menu type names to their corresponding <see cref="Type"/> objects.
    /// </summary>
    public static readonly Dictionary<string, Type> MenuTypesList = new()
    {
        { "ConsoleMenu", typeof(ConsoleMenu) },
        { "ChatMenu", typeof(ChatMenu) },
        { "WasdMenu", typeof(WasdMenu) },
        { "CenterHtmlMenu", typeof(CenterHtmlMenu) }
    };

    /// <summary>
    /// Gets the active menu for the specified player.
    /// </summary>
    /// <param name="player">The player controller.</param>
    /// <returns>The active menu instance, or null if no menu is active.</returns>
    public static IMenuInstance? GetActiveMenu(CCSPlayerController player)
    {
        return ActiveMenus.TryGetValue(player.SteamID, out (IMenuInstance Instance, Timer? Timer) value) ? value.Instance : null;
    }

    /// <summary>
    /// Closes the active menu for the specified player.
    /// </summary>
    /// <param name="player">The player controller.</param>
    public static void CloseActiveMenu(CCSPlayerController player)
    {
        if (!ActiveMenus.TryGetValue(player.SteamID, out (IMenuInstance Instance, Timer? Timer) value))
            return;

        value.Instance.Close(true);
        value.Timer?.Kill();
        value.Timer = null;
        ActiveMenus.Remove(player.SteamID);
    }

    internal static void DisposeActiveMenu(CCSPlayerController player)
    {
        if (!ActiveMenus.TryGetValue(player.SteamID, out (IMenuInstance Instance, Timer? Timer) value))
            return;

        value.Timer?.Kill();
        value.Timer = null;
        ActiveMenus.Remove(player.SteamID);
    }

    /// <summary>
    /// Opens a menu for the specified player.
    /// </summary>
    /// <typeparam name="TMenu">The type of the menu.</typeparam>
    /// <param name="player">The player controller.</param>
    /// <param name="menu">The menu to open.</param>
    /// <param name="firstItem">The index of the first item to display.</param>
    /// <param name="createInstance">The function to create a menu instance.</param>
    public static void OpenMenu<TMenu>(CCSPlayerController player, TMenu menu, int? firstItem, Func<CCSPlayerController, TMenu, IMenuInstance> createInstance)
        where TMenu : IMenu
    {
        if (menu.ItemOptions.Count == 0)
        {
            player.PrintToChat(player.Localizer("OptionListEmpty"));
            return;
        }

        EnsureHooks(menu.Plugin);
        CloseActiveMenu(player);

        Server.NextFrame(() =>
        {
            IMenuInstance instance = createInstance.Invoke(player, menu);

            if (instance is BaseMenuInstance baseMenuInstance)
            {
                baseMenuInstance.RegisterOnKeyPress();
                baseMenuInstance.RegisterPlayerDisconnectEvent();

                if (firstItem.HasValue)
                {
                    int item = Math.Clamp(firstItem.Value, 0, menu.ItemOptions.Count - 1);

                    // Walk forward only. '!= item' hangs if a restored CurrentChoiceIndex
                    // (e.g. WasdMenu RestoreMenuState) is already >= item.
                    while (baseMenuInstance.CurrentChoiceIndex < item)
                    {
                        baseMenuInstance.CurrentChoiceIndex++;

                        if (baseMenuInstance.CurrentChoiceIndex >= baseMenuInstance.CurrentOffset + baseMenuInstance.NumPerPage)
                            baseMenuInstance.NextPage();
                    }

                    baseMenuInstance.CurrentChoiceIndex -= baseMenuInstance.CurrentOffset;
                }
            }

            Timer? timer = menu.MenuTime > 0 ?
                menu.Plugin.AddTimer(menu.MenuTime, () => CloseActiveMenu(player)) :
                null;

            ActiveMenus[player.SteamID] = (instance, timer);

            instance.Display();
        });
    }

    /// <summary>
    /// Handles key press events for the active menu of the specified player.
    /// </summary>
    /// <param name="player">The player controller.</param>
    /// <param name="key">The key that was pressed.</param>
    public static void OnKeyPress(CCSPlayerController player, int key)
    {
        GetActiveMenu(player)?.OnKeyPress(player, key);
    }

    /// <summary>
    /// Creates a menu instance of a specified type
    /// </summary>
    /// <typeparam name="T">Type of menu to create (must implement BaseMenu)</typeparam>
    /// <param name="title">The title of the menu.</param>
    /// <param name="plugin">The plugin associated with the menu.</param>
    /// <returns>New menu instance of a requested type</returns>
    public static T CreateMenu<T>(string title, BasePlugin plugin) where T : BaseMenu
    {
        return (T)MenuByType(typeof(T), title, plugin);
    }

    /// <summary>
    /// Creates a menu instance based on Type parameter
    /// </summary>
    /// <param name="menuType">Type of menu to create</param>
    /// <param name="title">The title of the menu.</param>
    /// <param name="plugin">The plugin associated with the menu.</param>
    /// <returns>New menu instance of a requested type</returns>
    public static BaseMenu MenuByType(Type menuType, string title, BasePlugin plugin)
    {
        return menuType switch
        {
            { } t when t == typeof(PlayerMenu) => new PlayerMenu(title, plugin),
            { } t when t == typeof(ChatMenu) => new ChatMenu(title, plugin),
            { } t when t == typeof(ConsoleMenu) => new ConsoleMenu(title, plugin),
            { } t when t == typeof(CenterHtmlMenu) => new CenterHtmlMenu(title, plugin),
            { } t when t == typeof(WasdMenu) => new WasdMenu(title, plugin),
            _ => throw new ArgumentException($"Unsupported menu type: {menuType.FullName}", nameof(menuType))
        };
    }

    /// <summary>
    /// Creates a menu instance based on (string)Type parameter.
    /// </summary>
    /// <param name="menuType">Type of menu to create</param>
    /// <param name="title">The title of the menu.</param>
    /// <param name="plugin">The plugin associated with the menu.</param>
    /// <returns>New menu instance of a requested type</returns>
    public static BaseMenu MenuByType(string menuType, string title, BasePlugin plugin)
    {
        return menuType switch
        {
            "PlayerMenu" => new PlayerMenu(title, plugin),
            "ChatMenu" => new ChatMenu(title, plugin),
            "ConsoleMenu" => new ConsoleMenu(title, plugin),
            "CenterHtmlMenu" => new CenterHtmlMenu(title, plugin),
            "WasdMenu" => new WasdMenu(title, plugin),
            _ => throw new ArgumentException($"Unsupported menu type", nameof(menuType))
        };
    }

    /// <summary>
    /// Reloads config variables
    /// </summary>
    public static void ReloadConfig()
    {
        ConfigManager.LoadConfig();
    }
}