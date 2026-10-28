using System.Diagnostics;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2MenuManager.API.Interface;
using CS2MenuManager.API.Menu;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2MenuManager.API.Class;

/// <summary>
/// Manages the active menus for players.
/// </summary>
public static class MenuManager
{
    private static readonly Dictionary<ulong, (IMenuInstance Instance, Timer? Timer)> ActiveMenus = [];

    // Per-plugin hook state. Keyed weakly so an unloaded plugin's BasePlugin (and its
    // AssemblyLoadContext) is not pinned by this shared static after CounterStrikeSharp
    // has already torn its listeners/commands down.
    private static readonly ConditionalWeakTable<BasePlugin, PluginHooks> Hooks = new();
    private static readonly object InitLock = new();

    // Snapshot buffer for the per-tick dispatch. Reused so ticking allocates nothing;
    // a snapshot is needed because a ticked instance may close itself (or open a
    // different menu) and mutate ActiveMenus mid-iteration.
    private static readonly List<IMenuInstance> TickBuffer = [];

    /// <summary>
    /// Any game-thread callback owned by this library that runs longer than this is logged
    /// with its context, so regressions show up in the server log instead of only as
    /// anonymous "slow server frame" warnings.
    /// </summary>
    internal const double SlowCallbackThresholdMs = 5.0;

    private sealed class PluginHooks
    {
        public bool KeyCommandsRegistered;
    }

    /// <summary>
    /// Installs the library's per-plugin hooks exactly once per <see cref="BasePlugin"/>.
    /// </summary>
    /// <remarks>
    /// Everything that used to be registered per menu instance now lives here and is
    /// registered once for the plugin's lifetime: the <c>OnTick</c> listener that drives
    /// WASD / center-HTML menus, the disconnect handler, and (lazily, see
    /// <see cref="EnsureKeyCommands"/>) the <c>css_0</c>..<c>css_9</c> commands.
    /// <para>
    /// Per-instance registration was the cause of the "slow server frame" stalls: each
    /// menu open paid for a fresh native listener + event hook + FunctionReference stub,
    /// and numbered menus additionally re-registered ten engine <c>ConCommand</c>s
    /// (CounterStrikeSharp unregisters a command from the engine once its last definition
    /// is removed, which the previous instance's close had just done). All of that ran
    /// inside the <c>Server.NextFrame</c> callback that opens the menu.
    /// </para>
    /// </remarks>
    private static PluginHooks EnsureHooks(BasePlugin plugin)
    {
        if (Hooks.TryGetValue(plugin, out PluginHooks? hooks))
            return hooks;

        lock (InitLock)
        {
            if (Hooks.TryGetValue(plugin, out hooks))
                return hooks;

            hooks = new PluginHooks();

            plugin.RegisterListener<OnMapStart>(_ => CleanupAll());
            plugin.RegisterListener<OnTick>(() => TickPlugin(plugin));
            plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

            // Warm the stored menu-type preference as soon as the client authorizes, so that
            // opening a menu never has to wait on MySQL from the game thread.
            plugin.RegisterListener<OnClientAuthorized>((_, steamId) => MenuTypeManager.Prefetch(steamId.SteamId64));

            Hooks.Add(plugin, hooks);
            return hooks;
        }
    }

    /// <summary>
    /// Registers <c>css_0</c>..<c>css_9</c> once per plugin. Chat/console/center-HTML menus
    /// are driven by these; the handler routes to whichever menu the caller currently has
    /// open, so no per-instance commands are needed.
    /// </summary>
    private static void EnsureKeyCommands(BasePlugin plugin, PluginHooks hooks)
    {
        if (hooks.KeyCommandsRegistered)
            return;

        hooks.KeyCommandsRegistered = true;

        for (int i = 0; i <= 9; i++)
        {
            int key = i;
            plugin.AddCommand($"css_{i}", "CS2MenuManager menu key", (player, _) => OnKeyCommand(plugin, player, key));
        }
    }

    private static void OnKeyCommand(BasePlugin plugin, CCSPlayerController? player, int key)
    {
        if (player == null)
            return;

        // Only the plugin that owns the active menu may drive it: several plugins can
        // each have css_N registered and all of their handlers run for one key press.
        if (GetActiveMenu(player) is not BaseMenuInstance instance || instance.Menu.Plugin != plugin)
            return;

        // WASD menus are driven by movement keys, never by css_N (matches the old
        // behaviour, where they registered no key commands at all).
        if (!instance.UsesKeyCommands)
            return;

        instance.OnKeyPress(player, key);
    }

    /// <summary>
    /// Single per-plugin tick that drives every active menu instance owned by that plugin.
    /// Replaces one native <c>OnTick</c> listener per open menu.
    /// </summary>
    private static void TickPlugin(BasePlugin plugin)
    {
        if (ActiveMenus.Count == 0)
            return;

        long start = Stopwatch.GetTimestamp();

        TickBuffer.Clear();
        foreach ((IMenuInstance Instance, Timer? _) in ActiveMenus.Values)
            TickBuffer.Add(Instance);

        int ticked = 0;
        foreach (IMenuInstance menu in TickBuffer)
        {
            if (menu is not BaseMenuInstance instance || instance.Menu.Plugin != plugin)
                continue;

            ticked++;
            instance.OnTickInternal();
        }

        TickBuffer.Clear();

        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (elapsedMs > SlowCallbackThresholdMs)
        {
            plugin.Logger.LogWarning(
                "[CS2MenuManager] OnTick took {ElapsedMs:F1} ms (ticked={Ticked}, activeMenus={Active}, players={Players})",
                elapsedMs, ticked, ActiveMenus.Count, CountHumanPlayers());
        }
    }

    private static int CountHumanPlayers()
    {
        int count = 0;
        foreach (CCSPlayerController p in Utilities.GetPlayers())
        {
            if (!p.IsBot)
                count++;
        }

        return count;
    }

    private static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not { } player)
            return HookResult.Continue;

        ulong steamId = player.SteamID;

        // Close (not just drop) so the instance runs its own teardown — WASD unfreeze,
        // state save, center-HTML clear — exactly as the old per-instance disconnect hook did.
        if (ActiveMenus.TryGetValue(steamId, out (IMenuInstance Instance, Timer? Timer) value))
        {
            value.Timer?.Kill();
            value.Instance.Close(false);
        }

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
        // Map change: the old player entities are gone, so instances are dropped rather than
        // Close()d (Close reads the controller). Nothing leaks by dropping — instances no
        // longer own native listeners/commands/hooks; those are per-plugin (EnsureHooks).
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
        CloseActiveMenu(player, exitSound: true);
    }

    private static void CloseActiveMenu(CCSPlayerController player, bool exitSound)
    {
        if (!ActiveMenus.TryGetValue(player.SteamID, out (IMenuInstance Instance, Timer? Timer) value))
            return;

        value.Instance.Close(exitSound);
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
    /// <param name="time">The auto-close duration in seconds; 0 disables the timer.</param>
    public static void OpenMenu<TMenu>(CCSPlayerController player, TMenu menu, int? firstItem, Func<CCSPlayerController, TMenu, IMenuInstance> createInstance, int time)
        where TMenu : IMenu
    {
        if (menu.ItemOptions.Count == 0)
        {
            player.PrintToChat(player.Localizer("OptionListEmpty"));
            return;
        }

        PluginHooks hooks = EnsureHooks(menu.Plugin);
        CloseActiveMenu(player);

        // Back-compat: honour the deprecated menu-level MenuTime only when no explicit duration was passed.
#pragma warning disable CS0618
        if (time == 0 && menu.MenuTime > 0)
            time = menu.MenuTime;
#pragma warning restore CS0618

        Server.NextFrame(() =>
        {
            long start = Stopwatch.GetTimestamp();

            if (!player.IsValid)
                return;

            // Two Display() calls for the same player in one frame both pass the synchronous
            // CloseActiveMenu above; without this the second callback would overwrite the
            // first instance in ActiveMenus and orphan it. Re-check now that we are actually
            // about to install a new instance.
            CloseActiveMenu(player, exitSound: false);

            IMenuInstance instance = createInstance.Invoke(player, menu);

            if (instance is BaseMenuInstance baseMenuInstance)
            {
                baseMenuInstance.MenuTime = time;

                if (baseMenuInstance.UsesKeyCommands)
                    EnsureKeyCommands(menu.Plugin, hooks);

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

            Timer? timer = time > 0 ?
                menu.Plugin.AddTimer(time, () => CloseActiveMenu(player)) :
                null;

            ActiveMenus[player.SteamID] = (instance, timer);

            instance.Display();

            double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (elapsedMs > SlowCallbackThresholdMs)
            {
                menu.Plugin.Logger.LogWarning(
                    "[CS2MenuManager] OpenMenu NextFrame took {ElapsedMs:F1} ms (menu={MenuType}, items={Items}, activeMenus={Active}, players={Players})",
                    elapsedMs, menu.GetType().Name, menu.ItemOptions.Count, ActiveMenus.Count, CountHumanPlayers());
            }
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
