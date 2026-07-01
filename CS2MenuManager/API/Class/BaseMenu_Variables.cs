using static CS2MenuManager.API.Class.ConfigManager;

namespace CS2MenuManager.API.Class;

/// <summary>
/// Represents a base menu with common properties and methods.
/// </summary>
public abstract partial class BaseMenu
{
    internal static void CopySettings(BaseMenu oldMenu, BaseMenu newMenu)
    {
        System.Reflection.PropertyInfo[] properties = typeof(BaseMenu).GetProperties(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        foreach (System.Reflection.PropertyInfo property in properties)
        {
            if (property.CanWrite)
            {
                object? value = property.GetValue(oldMenu);
                property.SetValue(newMenu, value);
            }
        }
    }

    private static T ConditionalSet<T>(T defaultValue, T newValue)
    {
        return Config.ForceConfigSettings ? defaultValue : newValue;
    }

    private char _chatMenuTitleColor = Config.ChatMenu.TitleColor;
    private char _chatMenuEnabledColor = Config.ChatMenu.EnabledColor;
    private char _chatMenuDisabledColor = Config.ChatMenu.DisabledColor;
    private char _chatMenuPrevPageColor = Config.ChatMenu.PrevPageColor;
    private char _chatMenuNextPageColor = Config.ChatMenu.NextPageColor;
    private char _chatMenuExitColor = Config.ChatMenu.ExitColor;
    private string _centerHtmlMenuTitleColor = Config.CenterHtmlMenu.TitleColor;
    private string _centerHtmlMenuEnabledColor = Config.CenterHtmlMenu.EnabledColor;
    private string _centerHtmlMenuDisabledColor = Config.CenterHtmlMenu.DisabledColor;
    private string _centerHtmlMenuPrevPageColor = Config.CenterHtmlMenu.PrevPageColor;
    private string _centerHtmlMenuNextPageColor = Config.CenterHtmlMenu.NextPageColor;
    private string _centerHtmlMenuExitColor = Config.CenterHtmlMenu.ExitColor;
    private bool _centerHtmlMenuInlinePageOptions = Config.CenterHtmlMenu.InlinePageOptions;
    private int _centerHtmlMenuMaxTitleLength = Config.CenterHtmlMenu.MaxTitleLength;
    private int _centerHtmlMenuMaxOptionLength = Config.CenterHtmlMenu.MaxOptionLength;
    private string _wasdMenuTitleColor = Config.WasdMenu.TitleColor;
    private string _wasdMenuScrollUpKey = Config.Buttons.ScrollUp;
    private string _wasdMenuScrollDownKey = Config.Buttons.ScrollDown;
    private string _wasdMenuSelectKey = Config.Buttons.Select;
    private string _wasdMenuPrevKey = Config.Buttons.Prev;
    private string _wasdMenuExitKey = Config.Buttons.Exit;
    private string _wasdMenuPageUpKey = Config.Buttons.PageUp;
    private string _wasdMenuPageDownKey = Config.Buttons.PageDown;
    private string _wasdMenuScrollUpDownKeyColor = Config.WasdMenu.ScrollUpDownKeyColor;
    private string _wasdMenuSelectKeyColor = Config.WasdMenu.SelectKeyColor;
    private string _wasdMenuPrevKeyColor = Config.WasdMenu.PrevKeyColor;
    private string _wasdMenuExitKeyColor = Config.WasdMenu.ExitKeyColor;
    private string _wasdMenuSelectedOptionColor = Config.WasdMenu.SelectedOptionColor;
    private string _wasdMenuOptionColor = Config.WasdMenu.OptionColor;
    private string _wasdMenuDisabledOptionColor = Config.WasdMenu.DisabledOptionColor;
    private string _wasdMenuArrowColor = Config.WasdMenu.ArrowColor;
    private bool _wasdMenuShowArrows = Config.WasdMenu.ShowArrows;
    private string _wasdMenuFontSize = Config.WasdMenu.FontSize;
    private int _wasdMenuFixedWidth = Config.WasdMenu.FixedWidth;
    private bool _wasdMenuFreezePlayer = Config.WasdMenu.FreezePlayer;
    private bool _wasdMenuLookToScroll = Config.WasdMenu.LookToScroll;
    private bool _wasdMenuScrollbar = Config.WasdMenu.Scrollbar;
    private string _wasdMenuScrollbarColor = Config.WasdMenu.ScrollbarColor;
    private int _wasdMenuScrollStep = Config.WasdMenu.ScrollStep;
    private int _wasdMenuScrollRepeatDelay = Config.WasdMenu.ScrollRepeatDelay;
    private int _wasdMenuScrollRepeatRate = Config.WasdMenu.ScrollRepeatRate;

    /// <summary>
    /// Gets or sets the color of the title.
    /// </summary>
    public char ChatMenu_TitleColor
    {
        get => _chatMenuTitleColor;
        set => _chatMenuTitleColor = ConditionalSet(Config.ChatMenu.TitleColor, value);
    }

    /// <summary>
    /// Gets or sets the color of enabled items.
    /// </summary>
    public char ChatMenu_EnabledColor
    {
        get => _chatMenuEnabledColor;
        set => _chatMenuEnabledColor = ConditionalSet(Config.ChatMenu.EnabledColor, value);
    }

    /// <summary>
    /// Gets or sets the color of disabled items.
    /// </summary>
    public char ChatMenu_DisabledColor
    {
        get => _chatMenuDisabledColor;
        set => _chatMenuDisabledColor = ConditionalSet(Config.ChatMenu.DisabledColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the previous page button.
    /// </summary>
    public char ChatMenu_PrevPageColor
    {
        get => _chatMenuPrevPageColor;
        set => _chatMenuPrevPageColor = ConditionalSet(Config.ChatMenu.PrevPageColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the next page button.
    /// </summary>
    public char ChatMenu_NextPageColor
    {
        get => _chatMenuNextPageColor;
        set => _chatMenuNextPageColor = ConditionalSet(Config.ChatMenu.NextPageColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the close button.
    /// </summary>
    public char ChatMenu_ExitColor
    {
        get => _chatMenuExitColor;
        set => _chatMenuExitColor = ConditionalSet(Config.ChatMenu.ExitColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the title.
    /// </summary>
    public string CenterHtmlMenu_TitleColor
    {
        get => _centerHtmlMenuTitleColor;
        set => _centerHtmlMenuTitleColor = ConditionalSet(Config.CenterHtmlMenu.TitleColor, value);
    }

    /// <summary>
    /// Gets or sets the color of enabled items.
    /// </summary>
    public string CenterHtmlMenu_EnabledColor
    {
        get => _centerHtmlMenuEnabledColor;
        set => _centerHtmlMenuEnabledColor = ConditionalSet(Config.CenterHtmlMenu.EnabledColor, value);
    }

    /// <summary>
    /// Gets or sets the color of disabled items.
    /// </summary>
    public string CenterHtmlMenu_DisabledColor
    {
        get => _centerHtmlMenuDisabledColor;
        set => _centerHtmlMenuDisabledColor = ConditionalSet(Config.CenterHtmlMenu.DisabledColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the previous page button.
    /// </summary>
    public string CenterHtmlMenu_PrevPageColor
    {
        get => _centerHtmlMenuPrevPageColor;
        set => _centerHtmlMenuPrevPageColor = ConditionalSet(Config.CenterHtmlMenu.PrevPageColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the next page button.
    /// </summary>
    public string CenterHtmlMenu_NextPageColor
    {
        get => _centerHtmlMenuNextPageColor;
        set => _centerHtmlMenuNextPageColor = ConditionalSet(Config.CenterHtmlMenu.NextPageColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the close button.
    /// </summary>
    public string CenterHtmlMenu_ExitColor
    {
        get => _centerHtmlMenuExitColor;
        set => _centerHtmlMenuExitColor = ConditionalSet(Config.CenterHtmlMenu.ExitColor, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether page options are displayed inline.
    /// </summary>
    public bool CenterHtmlMenu_InlinePageOptions
    {
        get => _centerHtmlMenuInlinePageOptions;
        set => _centerHtmlMenuInlinePageOptions = ConditionalSet(Config.CenterHtmlMenu.InlinePageOptions, value);
    }

    /// <summary>
    /// Gets or sets the maximum length of the title.
    /// </summary>
    public int CenterHtmlMenu_MaxTitleLength
    {
        get => _centerHtmlMenuMaxTitleLength;
        set => _centerHtmlMenuMaxTitleLength = ConditionalSet(Config.CenterHtmlMenu.MaxTitleLength, value);
    }

    /// <summary>
    /// Gets or sets the maximum length of each option.
    /// </summary>
    public int CenterHtmlMenu_MaxOptionLength
    {
        get => _centerHtmlMenuMaxOptionLength;
        set => _centerHtmlMenuMaxOptionLength = ConditionalSet(Config.CenterHtmlMenu.MaxOptionLength, value);
    }

    /// <summary>
    /// Gets or sets the color of the title.
    /// </summary>
    public string WasdMenu_TitleColor
    {
        get => _wasdMenuTitleColor;
        set => _wasdMenuTitleColor = ConditionalSet(Config.WasdMenu.TitleColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the scroll up/down buttons.
    /// </summary>
    public string WasdMenu_ScrollUpDownKeyColor
    {
        get => _wasdMenuScrollUpDownKeyColor;
        set => _wasdMenuScrollUpDownKeyColor = ConditionalSet(Config.WasdMenu.ScrollUpDownKeyColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the select button.
    /// </summary>
    public string WasdMenu_SelectKeyColor
    {
        get => _wasdMenuSelectKeyColor;
        set => _wasdMenuSelectKeyColor = ConditionalSet(Config.WasdMenu.SelectKeyColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the prev button.
    /// </summary>
    public string WasdMenu_PrevKeyColor
    {
        get => _wasdMenuPrevKeyColor;
        set => _wasdMenuPrevKeyColor = ConditionalSet(Config.WasdMenu.PrevKeyColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the exit button.
    /// </summary>
    public string WasdMenu_ExitKeyColor
    {
        get => _wasdMenuExitKeyColor;
        set => _wasdMenuExitKeyColor = ConditionalSet(Config.WasdMenu.ExitKeyColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the selected option.
    /// </summary>
    public string WasdMenu_SelectedOptionColor
    {
        get => _wasdMenuSelectedOptionColor;
        set => _wasdMenuSelectedOptionColor = ConditionalSet(Config.WasdMenu.SelectedOptionColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the options.
    /// </summary>
    public string WasdMenu_OptionColor
    {
        get => _wasdMenuOptionColor;
        set => _wasdMenuOptionColor = ConditionalSet(Config.WasdMenu.OptionColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the disabled options.
    /// </summary>
    public string WasdMenu_DisabledOptionColor
    {
        get => _wasdMenuDisabledOptionColor;
        set => _wasdMenuDisabledOptionColor = ConditionalSet(Config.WasdMenu.DisabledOptionColor, value);
    }

    /// <summary>
    /// Gets or sets the color of the arrows.
    /// </summary>
    public string WasdMenu_ArrowColor
    {
        get => _wasdMenuArrowColor;
        set => _wasdMenuArrowColor = ConditionalSet(Config.WasdMenu.ArrowColor, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selection arrows are shown around the highlighted option.
    /// </summary>
    public bool WasdMenu_ShowArrows
    {
        get => _wasdMenuShowArrows;
        set => _wasdMenuShowArrows = ConditionalSet(Config.WasdMenu.ShowArrows, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the player is frozen while the menu is open.
    /// </summary>
    public bool WasdMenu_FreezePlayer
    {
        get => _wasdMenuFreezePlayer;
        set => _wasdMenuFreezePlayer = ConditionalSet(Config.WasdMenu.FreezePlayer, value);
    }

    /// <summary>
    /// The key binding used to scroll up in the menu.
    /// </summary>
    public string WasdMenu_ScrollUpKey
    {
        get => _wasdMenuScrollUpKey;
        set => _wasdMenuScrollUpKey = ConditionalSet(Config.Buttons.ScrollUp, value);
    }

    /// <summary>
    /// The key binding used to scroll down in the menu.
    /// </summary>
    public string WasdMenu_ScrollDownKey
    {
        get => _wasdMenuScrollDownKey;
        set => _wasdMenuScrollDownKey = ConditionalSet(Config.Buttons.ScrollDown, value);
    }

    /// <summary>
    /// The key binding used to select the currently highlighted menu option.
    /// </summary>
    public string WasdMenu_SelectKey
    {
        get => _wasdMenuSelectKey;
        set => _wasdMenuSelectKey = ConditionalSet(Config.Buttons.Select, value);
    }

    /// <summary>
    /// The key binding used to navigate to the previous page or option in the menu.
    /// </summary>
    public string WasdMenu_PrevKey
    {
        get => _wasdMenuPrevKey;
        set => _wasdMenuPrevKey = ConditionalSet(Config.Buttons.Prev, value);
    }

    /// <summary>
    /// The key binding used to close the menu.
    /// </summary>
    public string WasdMenu_ExitKey
    {
        get => _wasdMenuExitKey;
        set => _wasdMenuExitKey = ConditionalSet(Config.Buttons.Exit, value);
    }

    /// <summary>
    /// The key binding used to jump to the previous page in the menu.
    /// </summary>
    public string WasdMenu_PageUpKey
    {
        get => _wasdMenuPageUpKey;
        set => _wasdMenuPageUpKey = ConditionalSet(Config.Buttons.PageUp, value);
    }

    /// <summary>
    /// The key binding used to jump to the next page in the menu.
    /// </summary>
    public string WasdMenu_PageDownKey
    {
        get => _wasdMenuPageDownKey;
        set => _wasdMenuPageDownKey = ConditionalSet(Config.Buttons.PageDown, value);
    }

    /// <summary>
    /// Gets or sets the center-html font size class for the whole menu.
    /// Valid values: "s", "sm", "m", "l", "xl", "xxl". Empty keeps the game default size.
    /// </summary>
    public string WasdMenu_FontSize
    {
        get => _wasdMenuFontSize;
        set => _wasdMenuFontSize = ConditionalSet(Config.WasdMenu.FontSize, value);
    }

    /// <summary>
    /// When &gt; 0, option rows are centered to this width (in characters) and a horizontal rule of
    /// the same width frames the options, giving the menu a constant, filled box width. 0 = off.
    /// </summary>
    public int WasdMenu_FixedWidth
    {
        get => _wasdMenuFixedWidth;
        set => _wasdMenuFixedWidth = ConditionalSet(Config.WasdMenu.FixedWidth, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether moving the mouse (eye pitch) scrolls the menu.
    /// </summary>
    public bool WasdMenu_LookToScroll
    {
        get => _wasdMenuLookToScroll;
        set => _wasdMenuLookToScroll = ConditionalSet(Config.WasdMenu.LookToScroll, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a scrollbar is drawn beside the options.
    /// </summary>
    public bool WasdMenu_Scrollbar
    {
        get => _wasdMenuScrollbar;
        set => _wasdMenuScrollbar = ConditionalSet(Config.WasdMenu.Scrollbar, value);
    }

    /// <summary>
    /// Gets or sets the color of the scrollbar.
    /// </summary>
    public string WasdMenu_ScrollbarColor
    {
        get => _wasdMenuScrollbarColor;
        set => _wasdMenuScrollbarColor = ConditionalSet(Config.WasdMenu.ScrollbarColor, value);
    }

    /// <summary>
    /// Gets or sets how many items a single scroll action moves. Minimum 1.
    /// </summary>
    public int WasdMenu_ScrollStep
    {
        get => _wasdMenuScrollStep;
        set => _wasdMenuScrollStep = ConditionalSet(Config.WasdMenu.ScrollStep, Math.Max(1, value));
    }

    /// <summary>
    /// Gets or sets the tick delay before a held scroll key starts auto-repeating. Minimum 1.
    /// </summary>
    public int WasdMenu_ScrollRepeatDelay
    {
        get => _wasdMenuScrollRepeatDelay;
        set => _wasdMenuScrollRepeatDelay = ConditionalSet(Config.WasdMenu.ScrollRepeatDelay, Math.Max(1, value));
    }

    /// <summary>
    /// Gets or sets the tick interval between auto-repeat scroll steps while a key is held. Minimum 1.
    /// </summary>
    public int WasdMenu_ScrollRepeatRate
    {
        get => _wasdMenuScrollRepeatRate;
        set => _wasdMenuScrollRepeatRate = ConditionalSet(Config.WasdMenu.ScrollRepeatRate, Math.Max(1, value));
    }
}