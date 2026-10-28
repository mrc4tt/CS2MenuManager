using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Interface;
using static CS2MenuManager.API.Class.Buttons;
using static CS2MenuManager.API.Class.ConfigManager;

namespace CS2MenuManager.API.Menu;

/// <summary>
/// Represents a WASD menu with customizable colors and options.
/// </summary>
/// <param name="title">The title of the menu.</param>
/// <param name="plugin">The plugin associated with the menu.</param>
public class WasdMenu(string title, BasePlugin plugin) : BaseMenu(title, plugin)
{
    /// <summary>
    /// Displays the menu to the specified player for a specified duration.
    /// </summary>
    /// <param name="player">The player to whom the menu is displayed.</param>
    /// <param name="time">The duration for which the menu is displayed.</param>
    public override void Display(CCSPlayerController player, int time)
    {
        MenuManager.OpenMenu(player, this, null, (p, m) => new WasdMenuInstance(p, m), time);
    }

    /// <summary>
    /// Displays the menu to the specified player for a specified duration, starting from the given item.
    /// </summary>
    /// <param name="player">The player to whom the menu is displayed.</param>
    /// <param name="firstItem">The index of the first item to display.</param>
    /// <param name="time">The duration for which the menu is displayed.</param>
    public override void DisplayAt(CCSPlayerController player, int firstItem, int time)
    {
        MenuManager.OpenMenu(player, this, firstItem, (p, m) => new WasdMenuInstance(p, m), time);
    }
}

/// <summary>
/// Represents an instance of a WASD menu with player-specific data.
/// </summary>
public class WasdMenuInstance : BaseMenuInstance
{
    private readonly Dictionary<string, Action> Buttons = [];

    /// <summary>
    /// Gets the number of items displayed per page.
    /// </summary>
    public override int NumPerPage => 5;

    /// <summary>
    /// Gets or sets the display string for the menu.
    /// </summary>
    public string DisplayString = "";

    private PlayerButtons OldButton;
    private readonly float OldVelocityModifier;

    private const string ScrollbarTrack = "┃";
    private const string ScrollbarThumb = "█";
    private const float LookScrollThreshold = 5.0f;
    private float _baselinePitch;
    private bool _pitchInitialized;
    private int _nextLookTick;

    private string _scrollUpKey = "";
    private string _scrollDownKey = "";
    private PlayerButtons _heldScrollButton;
    private int _nextRepeatTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="WasdMenuInstance"/> class.
    /// </summary>
    /// <param name="player">The player associated with this menu instance.</param>
    /// <param name="menu">The menu associated with this instance.</param>
    public WasdMenuInstance(CCSPlayerController player, IMenu menu) : base(player, menu)
    {
        if (Menu is not WasdMenu wasdMenu)
            return;

        _scrollUpKey = wasdMenu.WasdMenu_ScrollUpKey;
        _scrollDownKey = wasdMenu.WasdMenu_ScrollDownKey;

        // Scroll keys are handled separately (press + hold-to-repeat); only the
        // non-repeating actions live in this edge-triggered dictionary.
        Buttons = new Dictionary<string, Action>()
        {
            { wasdMenu.WasdMenu_SelectKey, Choose },
            { wasdMenu.WasdMenu_PrevKey, PrevSubMenu },
            { wasdMenu.WasdMenu_PageUpKey, PageUp },
            { wasdMenu.WasdMenu_PageDownKey, PageDown },
            { wasdMenu.WasdMenu_ExitKey, () => { if (Menu.ExitButton) Close(true); } }
        };

        Player.SaveSpeed(ref OldVelocityModifier);
        
        // Restore previously saved menu state (if exists)
        RestoreMenuState();
    }

    /// <summary>
    /// Displays the menu to the player.
    /// </summary>
    public override void Display()
    {
        if (Menu is not WasdMenu wasdMenu) return;

        string leftArrow = wasdMenu.WasdMenu_ShowArrows ? $"<font color='{wasdMenu.WasdMenu_ArrowColor}'>▶ [</font>" : string.Empty;
        string rightArrow = wasdMenu.WasdMenu_ShowArrows ? $"<font color='{wasdMenu.WasdMenu_ArrowColor}'> ] ◀</font>" : string.Empty;

        StringBuilder builder = new();
        int totalPages = (int)Math.Ceiling((double)Menu.ItemOptions.Count / MenuItemsPerPage);
        int currentPage = Page + 1;
        builder.Append($"<font color='{wasdMenu.WasdMenu_TitleColor}'>{Menu.Title}</font> ({currentPage}/{totalPages})<br>");

        // Fixed-width mode: center every option to a constant char width and frame the option block
        // with a horizontal rule, so the menu box no longer resizes as the selection (and its arrows)
        // moves between short and long items. 0 = off (original hug-the-text behavior).
        int fixedWidth = wasdMenu.WasdMenu_FixedWidth;
        bool pad = fixedWidth > 0;
        string rule = pad ? $"<font color='{wasdMenu.WasdMenu_OptionColor}'>{new string('─', fixedWidth)}</font><br>" : string.Empty;

        if (pad)
            builder.Append(rule);

        int maxIndex = Math.Min(CurrentOffset + MenuItemsPerPage, Menu.ItemOptions.Count);
        int visibleCount = maxIndex - CurrentOffset;
        for (int i = CurrentOffset; i < maxIndex; i++)
        {
            ItemOption option = Menu.ItemOptions[i];
            string bar = ScrollbarPrefix(i - CurrentOffset, visibleCount, wasdMenu);
            string text = pad ? CenterPad(option.Text, fixedWidth) : option.Text;

            if (i == CurrentChoiceIndex)
            {
                builder.AppendLine(option.DisableOption switch
                {
                    DisableOption.None =>
                        $"{bar}{leftArrow} <font color='{wasdMenu.WasdMenu_SelectedOptionColor}'>{text}</font> {rightArrow}<br>",
                    DisableOption.DisableShowNumber or DisableOption.DisableHideNumber =>
                        $"{bar}{leftArrow} <font color='{wasdMenu.WasdMenu_DisabledOptionColor}'>{text}</font> {rightArrow}<br>",
                    _ => string.Empty
                });
            }
            else
            {
                builder.AppendLine(option.DisableOption switch
                {
                    DisableOption.None =>
                        $"{bar}<font color='{wasdMenu.WasdMenu_OptionColor}'>{text}</font><br>",
                    DisableOption.DisableShowNumber or DisableOption.DisableHideNumber =>
                        $"{bar}<font color='{wasdMenu.WasdMenu_DisabledOptionColor}'>{text}</font><br>",
                    _ => string.Empty
                });
            }
        }

        if (pad)
            builder.Append(rule);

        List<string> buttomText =
        [
            $"<font color='{wasdMenu.WasdMenu_ScrollUpDownKeyColor}'>{Player.Localizer("ScrollKey", wasdMenu.WasdMenu_ScrollUpKey, wasdMenu.WasdMenu_ScrollDownKey)}</font>",
            $"<font color='{wasdMenu.WasdMenu_SelectKeyColor}'>{Player.Localizer("SelectKey", wasdMenu.WasdMenu_SelectKey)}</font>"
        ];

        if (totalPages > 1)
            buttomText.Add($"<font color='{wasdMenu.WasdMenu_ScrollUpDownKeyColor}'>{Player.Localizer("PageKey", wasdMenu.WasdMenu_PageUpKey, wasdMenu.WasdMenu_PageDownKey)}</font>");

        if (wasdMenu.PrevMenu != null)
            buttomText.Add($"<font color='{wasdMenu.WasdMenu_PrevKeyColor}'>{Player.Localizer("PrevKey", wasdMenu.WasdMenu_PrevKey)}</font>");

        if (HasExitButton)
            buttomText.Add($"<font color='{wasdMenu.WasdMenu_ExitKeyColor}'>{Player.Localizer("ExitKey", wasdMenu.WasdMenu_ExitKey)}</font>");

        string footer = string.Join(" | ", buttomText);
        if (!string.IsNullOrEmpty(wasdMenu.WasdMenu_FooterFontSize))
            footer = $"<font class='fontSize-{wasdMenu.WasdMenu_FooterFontSize}'>{footer}</font>";
        builder.AppendLine(footer);

        string content = builder.ToString();
        if (!string.IsNullOrEmpty(wasdMenu.WasdMenu_FontSize))
            content = $"<font class='fontSize-{wasdMenu.WasdMenu_FontSize}'>{content}</font>";

        DisplayString = content;
    }

    /// <summary>
    /// Fits <paramref name="text"/> to <paramref name="width"/> characters: shorter text is centered
    /// with non-breaking spaces (a regular space collapses in center-html); longer text is truncated
    /// with a trailing ellipsis so a row never wraps to a second line (which would grow the box height
    /// per menu and clip the key hints). The center-html font is proportional, so sizing is approximate
    /// — pick a width with margin.
    /// </summary>
    private static string CenterPad(string text, int width)
    {
        int len = text.Length;
        if (len > width)
            return width <= 1 ? text[..width] : text[..(width - 1)] + "…";

        if (len == width)
            return text;

        int total = width - len;
        int left = total / 2;
        int right = total - left;
        return new string(' ', left) + text + new string(' ', right);
    }

    /// <summary>
    /// Builds the scrollbar glyph shown to the left of a visible row.
    /// The thumb is placed at the row matching the selected item's position within the whole list.
    /// Returns an empty string when the scrollbar is disabled or all items fit one page.
    /// </summary>
    private string ScrollbarPrefix(int rowOnPage, int visibleCount, WasdMenu wasdMenu)
    {
        if (!wasdMenu.WasdMenu_Scrollbar || Menu.ItemOptions.Count <= MenuItemsPerPage || visibleCount <= 0)
            return string.Empty;

        int total = Menu.ItemOptions.Count;
        int thumbRow = total <= 1 || visibleCount <= 1
            ? 0
            : (int)Math.Round((double)CurrentChoiceIndex / (total - 1) * (visibleCount - 1));

        string glyph = rowOnPage == thumbRow ? ScrollbarThumb : ScrollbarTrack;
        return $"<font color='{wasdMenu.WasdMenu_ScrollbarColor}'>{glyph}</font> ";
    }

    /// <summary>
    /// Closes the menu.
    /// </summary>
    public override void Close(bool exitSound)
    {
        // Save current menu state before closing
        SaveMenuState();
        
        base.Close(exitSound);

        if (Player.IsValid)
        {
            Player.PrintToCenterHtml(" ");

            if (((WasdMenu)Menu).WasdMenu_FreezePlayer)
                Player.Unfreeze(OldVelocityModifier);

            if (exitSound && !string.IsNullOrEmpty(Config.Sound.Exit))
                Player.ExecuteClientCommand($"play {Config.Sound.Exit}");
        }
    }

    internal override void OnTickInternal()
    {
        OnTick();
    }

    /// <summary>
    /// Handles the tick event for the menu. Driven by <see cref="MenuManager"/>'s single
    /// per-plugin tick listener; kept public for API compatibility.
    /// </summary>
    public void OnTick()
    {
        // Only the player's active menu should react to input / repaint. Guards against a stale
        // instance still receiving OnTick after the player opened a different menu.
        if (!Player.IsValid || MenuManager.GetActiveMenu(Player) != this)
            return;

        PlayerButtons button = Player.Buttons;

        foreach (KeyValuePair<string, Action> kvp in Buttons)
        {
            if (!ButtonMapping.TryGetValue(kvp.Key, out PlayerButtons mappedBtn))
                continue;

            if ((button & mappedBtn) != 0 || (OldButton & mappedBtn) == 0)
                continue;

            kvp.Value.Invoke();
            break;
        }

        HandleScrollKeys(button);

        OldButton = button;

        if (((WasdMenu)Menu).WasdMenu_LookToScroll)
            HandleLookToScroll();

        if (!string.IsNullOrEmpty(DisplayString))
            Player.PrintToCenterHtml(DisplayString);

        if (((WasdMenu)Menu).WasdMenu_FreezePlayer)
            Player.Freeze();
    }

    /// <summary>
    /// Hold-to-repeat handling for the scroll keys: the first press scrolls once, then after
    /// <c>WasdMenu_ScrollRepeatDelay</c> ticks it auto-repeats every <c>WasdMenu_ScrollRepeatRate</c> ticks.
    /// </summary>
    private void HandleScrollKeys(PlayerButtons button)
    {
        if (Menu is not WasdMenu wasdMenu)
            return;

        if (TryScrollKey(_scrollDownKey, button, ScrollDown, wasdMenu))
            return;

        TryScrollKey(_scrollUpKey, button, ScrollUp, wasdMenu);
    }

    private bool TryScrollKey(string key, PlayerButtons button, Action scroll, WasdMenu wasdMenu)
    {
        if (!ButtonMapping.TryGetValue(key, out PlayerButtons mappedBtn))
            return false;

        bool down = (button & mappedBtn) != 0;
        bool wasDown = (OldButton & mappedBtn) != 0;

        if (down && !wasDown)
        {
            scroll();
            _heldScrollButton = mappedBtn;
            _nextRepeatTick = Server.TickCount + wasdMenu.WasdMenu_ScrollRepeatDelay;
            return true;
        }

        if (down && _heldScrollButton == mappedBtn)
        {
            if (Server.TickCount >= _nextRepeatTick)
            {
                scroll();
                _nextRepeatTick = Server.TickCount + wasdMenu.WasdMenu_ScrollRepeatRate;
            }
            return true;
        }

        if (!down && _heldScrollButton == mappedBtn)
            _heldScrollButton = 0;

        return false;
    }

    /// <summary>
    /// Scrolls the menu based on the player's vertical look (mouse pitch). Looking down scrolls
    /// down, looking up scrolls up. Cadence is throttled by <c>WasdMenu_ScrollRepeatRate</c>.
    /// </summary>
    private void HandleLookToScroll()
    {
        float pitch = Player.GetEyePitch();

        if (!_pitchInitialized)
        {
            _baselinePitch = pitch;
            _pitchInitialized = true;
            return;
        }

        if (Server.TickCount < _nextLookTick)
            return;

        int rate = ((WasdMenu)Menu).WasdMenu_ScrollRepeatRate;
        float delta = pitch - _baselinePitch;

        if (delta > LookScrollThreshold)
        {
            ScrollDown();
            _baselinePitch = pitch;
            _nextLookTick = Server.TickCount + rate;
        }
        else if (delta < -LookScrollThreshold)
        {
            ScrollUp();
            _baselinePitch = pitch;
            _nextLookTick = Server.TickCount + rate;
        }
    }

    /// <summary>
    /// Chooses the currently selected option.
    /// </summary>
    public void Choose()
    {
        if (CurrentChoiceIndex < 0 || CurrentChoiceIndex >= Menu.ItemOptions.Count)
            return;

        ItemOption option = Menu.ItemOptions[CurrentChoiceIndex];

        if (option.DisableOption != DisableOption.None)
        {
            Player.PrintToChat(Player.Localizer("WarnDisabledItem"));
            return;
        }

        if (!string.IsNullOrEmpty(Config.Sound.Select))
            Player.ExecuteClientCommand($"play {Config.Sound.Select}");

        // Save current menu state before selecting an option
        SaveMenuState();
        
        HandleSelectAction(option);
    }

    /// <summary>
    /// Scrolls down by the configured step, moving the page as needed.
    /// </summary>
    public void ScrollDown()
    {
        if (!ScrollBy(+1)) return;

        Display();
        if (!string.IsNullOrEmpty(Config.Sound.ScrollDown))
            Player.ExecuteClientCommand($"play {Config.Sound.ScrollDown}");
    }

    /// <summary>
    /// Scrolls up by the configured step, moving the page as needed.
    /// </summary>
    public void ScrollUp()
    {
        if (!ScrollBy(-1)) return;

        Display();
        if (!string.IsNullOrEmpty(Config.Sound.ScrollUp))
            Player.ExecuteClientCommand($"play {Config.Sound.ScrollUp}");
    }

    /// <summary>
    /// Jumps to the next page, landing the selection on that page's first item.
    /// No-op when already on the last page. Mirrors the offset/stack bookkeeping StepOnce does per item.
    /// </summary>
    public void PageDown()
    {
        int nextOffset = CurrentOffset + MenuItemsPerPage;
        if (nextOffset >= Menu.ItemOptions.Count) return;

        PrevPageOffsets.Push(CurrentOffset);
        CurrentOffset = nextOffset;
        Page++;
        CurrentChoiceIndex = CurrentOffset;

        Display();
        if (!string.IsNullOrEmpty(Config.Sound.ScrollDown))
            Player.ExecuteClientCommand($"play {Config.Sound.ScrollDown}");
    }

    /// <summary>
    /// Jumps to the previous page, landing the selection on that page's first item.
    /// No-op when already on the first page.
    /// </summary>
    public void PageUp()
    {
        if (CurrentOffset <= 0 || PrevPageOffsets.Count == 0) return;

        CurrentOffset = PrevPageOffsets.Pop();
        Page--;
        CurrentChoiceIndex = CurrentOffset;

        Display();
        if (!string.IsNullOrEmpty(Config.Sound.ScrollUp))
            Player.ExecuteClientCommand($"play {Config.Sound.ScrollUp}");
    }

    /// <summary>
    /// Moves the selection by <c>WasdMenu_ScrollStep</c> items in the given direction,
    /// adjusting the page offset. Returns true if the selection actually moved.
    /// Display and sound are left to the caller so a multi-item step refreshes once.
    /// </summary>
    private bool ScrollBy(int direction)
    {
        int step = Menu is WasdMenu wasdMenu ? wasdMenu.WasdMenu_ScrollStep : 1;
        bool moved = false;

        for (int s = 0; s < step; s++)
        {
            if (!StepOnce(direction))
                break;
            moved = true;
        }

        return moved;
    }

    private bool StepOnce(int direction)
    {
        if (direction > 0)
        {
            if (CurrentChoiceIndex >= Menu.ItemOptions.Count - 1)
                return false;

            CurrentChoiceIndex++;
            if (CurrentChoiceIndex >= CurrentOffset + NumPerPage)
            {
                PrevPageOffsets.Push(CurrentOffset);
                CurrentOffset += MenuItemsPerPage;
                Page++;
            }
        }
        else
        {
            if (CurrentChoiceIndex <= 0)
                return false;

            CurrentChoiceIndex--;
            if (CurrentChoiceIndex < CurrentOffset)
            {
                Page--;
                CurrentOffset = PrevPageOffsets.Pop();
            }
        }

        return true;
    }
    
    /// <summary>
    /// Saves the current menu state (page number, offset, selected option, and page offset stack)
    /// </summary>
    private void SaveMenuState()
    {
        // Use a static dictionary to save menu states for each player
        if (!MenuStateStorage.ContainsKey(Player.SteamID))
        {
            MenuStateStorage[Player.SteamID] = new Dictionary<string, MenuState>();
        }
        
        // Use the menu title as the key to save the state of this menu
        MenuStateStorage[Player.SteamID][Menu.Title] = new MenuState
        {
            Page = Page,
            CurrentOffset = CurrentOffset,
            CurrentChoiceIndex = CurrentChoiceIndex,
            PrevPageOffsets = new Stack<int>(PrevPageOffsets) // Create a copy of the stack
        };
    }
    
    /// <summary>
    /// Restores the previously saved menu state (page number, offset, selected option, and page offset stack)
    /// </summary>
    private void RestoreMenuState()
    {
        if (MenuStateStorage.ContainsKey(Player.SteamID) && 
            MenuStateStorage[Player.SteamID].ContainsKey(Menu.Title))
        {
            MenuState state = MenuStateStorage[Player.SteamID][Menu.Title];
            Page = state.Page;
            CurrentOffset = state.CurrentOffset;
            CurrentChoiceIndex = state.CurrentChoiceIndex;
            
            // Restore the page offset stack
            PrevPageOffsets.Clear();
            foreach (int offset in state.PrevPageOffsets)
            {
                PrevPageOffsets.Push(offset);
            }
        }
    }
    
    /// <summary>
    /// Menu state class, used to save the page number, offset, selected option, and page offset stack of a menu
    /// </summary>
    private class MenuState
    {
        public int Page { get; set; }
        public int CurrentOffset { get; set; }
        public int CurrentChoiceIndex { get; set; }
        public Stack<int> PrevPageOffsets { get; set; } = new Stack<int>();
    }
    
    /// <summary>
    /// Static dictionary used to store menu states for all players
    /// </summary>
    private static readonly Dictionary<ulong, Dictionary<string, MenuState>> MenuStateStorage = new();
    
    /// <summary>
    /// Cleans up the menu state for a specific player
    /// </summary>
    /// <param name="steamId">The player's SteamID</param>
    public static void CleanupPlayerState(ulong steamId)
    {
        if (MenuStateStorage.ContainsKey(steamId))
        {
            MenuStateStorage.Remove(steamId);
        }
    }
    
    /// <summary>
    /// Cleans up all menu states
    /// </summary>
    public static void CleanupAllStates()
    {
        MenuStateStorage.Clear();
    }
}

/// <summary>
/// WasdMenu state manager, used to handle state cleanup when players disconnect or when the map changes
/// </summary>
public static class WasdMenuStateManager
{
    /// <summary>
    /// Registers WasdMenu state manager events
    /// </summary>
    /// <param name="plugin">The plugin instance</param>
    public static void RegisterEvents(BasePlugin plugin)
    {
        // Register player disconnect event
        plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        
        // Register round end event
        plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
    }
    
    /// <summary>
    /// Handles player disconnect event
    /// </summary>
    private static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid != null)
        {
            WasdMenuInstance.CleanupPlayerState(@event.Userid.SteamID);
        }
        return HookResult.Continue;
    }
    
    /// <summary>
    /// Handles round end event
    /// </summary>
    private static HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        // Clean up all menu states at the end of each round
        WasdMenuInstance.CleanupAllStates();
        return HookResult.Continue;
    }
}
