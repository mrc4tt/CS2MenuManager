using System.Text;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Interface;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2MenuManager.API.Menu;

/// <summary>
/// Represents a center HTML menu with customizable colors and options.
/// </summary>
/// <param name="title">The title of the menu.</param>
/// <param name="plugin">The plugin associated with the menu.</param>
public class CenterHtmlMenu(string title, BasePlugin plugin) : BaseMenu(title, plugin)
{
    /// <summary>
    /// Displays the menu to the specified player for a specified duration.
    /// </summary>
    /// <param name="player">The player to whom the menu is displayed.</param>
    /// <param name="time">The duration for which the menu is displayed.</param>
    public override void Display(CCSPlayerController player, int time)
    {
        MenuManager.OpenMenu(player, this, null, (p, m) => new CenterHtmlMenuInstance(p, m), time);
    }

    /// <summary>
    /// Displays the menu to the specified player for a specified duration, starting from the given item.
    /// </summary>
    /// <param name="player">The player to whom the menu is displayed.</param>
    /// <param name="firstItem">The index of the first item to display.</param>
    /// <param name="time">The duration for which the menu is displayed.</param>
    public override void DisplayAt(CCSPlayerController player, int firstItem, int time)
    {
        MenuManager.OpenMenu(player, this, firstItem, (p, m) => new CenterHtmlMenuInstance(p, m), time);
    }
}

/// <summary>
/// Represents an instance of a center HTML menu with player-specific data.
/// </summary>
public class CenterHtmlMenuInstance : BaseMenuInstance
{
    private string _cachedDisplay = string.Empty;

    /// <summary>
    /// Gets the number of items displayed per page.
    /// </summary>
    public override int NumPerPage => 5;

    /// <summary>
    /// Gets the number of items displayed per page.
    /// </summary>
    protected override int MenuItemsPerPage => CalculateMenuItemsPerPage();

    /// <summary>
    /// Gets a value indicating whether the menu has a next button.
    /// </summary>
    protected override bool HasNextButton => Menu.ItemOptions.Count > NumPerPage + 1 && CurrentOffset + NumPerPage < Menu.ItemOptions.Count;

    /// <summary>
    /// Cached rendered markup, rebuilt only when <see cref="_dirty"/> is set.
    /// </summary>
    private string _html = "";

    /// <summary>
    /// Indicates the cached markup is stale and must be rebuilt on the next tick.
    /// </summary>
    private bool _dirty = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CenterHtmlMenuInstance"/> class.
    /// </summary>
    /// <param name="player">The player associated with this menu instance.</param>
    /// <param name="menu">The menu associated with this instance.</param>
    public CenterHtmlMenuInstance(CCSPlayerController player, IMenu menu) : base(player, menu)
    {
        Menu.Plugin.RegisterListener<OnTick>(OnTick);
    }

    /// <summary>
    /// Marks the rendered markup stale. Invoked by the base navigation methods on any state change.
    /// </summary>
    public override void Display()
    {
        _dirty = true;
    }

    /// <summary>
    /// Re-asserts the menu on the center HTML channel every tick so it survives other plugins
    /// writing to the same channel, but rebuilds the markup only when the menu state has changed.
    /// </summary>
    private void OnTick()
    {
        if (Menu is not CenterHtmlMenu centerHtmlMenu)
            return;

        // Stop drawing once this instance is no longer the player's active menu (closed/replaced).
        if (!Player.IsValid || MenuManager.GetActiveMenu(Player) != this)
            return;

        if (_dirty)
        {
            _html = BuildHtml(centerHtmlMenu);
            _dirty = false;
        }

        Player.PrintToCenterHtml(_html);
    }

    private string BuildHtml(CenterHtmlMenu centerHtmlMenu)
    {
        StringBuilder builder = new();
        string title = centerHtmlMenu.Title.TruncateHtml(centerHtmlMenu.CenterHtmlMenu_MaxTitleLength);
        builder.Append($"<b><font color='{centerHtmlMenu.CenterHtmlMenu_TitleColor}'>{title}</font></b><br>");

        int keyOffset = 1;
        int maxIndex = Math.Min(CurrentOffset + MenuItemsPerPage, Menu.ItemOptions.Count);
        for (int i = CurrentOffset; i < maxIndex; i++)
        {
            ItemOption option = centerHtmlMenu.ItemOptions[i];
            string color = option.DisableOption == DisableOption.None ? centerHtmlMenu.CenterHtmlMenu_EnabledColor : centerHtmlMenu.CenterHtmlMenu_DisabledColor;
            string text = option.Text.TruncateHtml(centerHtmlMenu.CenterHtmlMenu_MaxOptionLength);

            builder.Append(option.DisableOption switch
            {
                DisableOption.None or DisableOption.DisableShowNumber => $"<font color='{color}'>!{keyOffset}</font> {text}<br>",
                DisableOption.DisableHideNumber => $"<font color='{color}'>{text}</font><br>",
                _ => string.Empty
            });

            keyOffset++;
        }

        AddPageOptions(centerHtmlMenu, builder);
        return builder.ToString();
    }

    /// <summary>
    /// Closes the menu.
    /// </summary>
    public override void Close(bool exitSound)
    {
        base.Close(exitSound);
        Menu.Plugin.RemoveListener<OnTick>(OnTick);
        _html = string.Empty;
        _dirty = true;

        if (Player.IsValid)
            Player.PrintToCenterHtml(" ");
    }

    private void AddPageOptions(CenterHtmlMenu centerHtmlMenu, StringBuilder builder)
    {
        string prevText = $"<font color='{centerHtmlMenu.CenterHtmlMenu_PrevPageColor}'>!8 &#60;</font> {Player.Localizer("Prev")}";
        string closeText = $"<font color='{centerHtmlMenu.CenterHtmlMenu_ExitColor}'>!0 X</font> {Player.Localizer("Exit")}";
        string nextText = $"<font color='{centerHtmlMenu.CenterHtmlMenu_NextPageColor}'>!9 ></font> {Player.Localizer("Next")}";

        if (centerHtmlMenu.CenterHtmlMenu_InlinePageOptions)
            AddInlinePageOptions(prevText, closeText, nextText, builder);
        else
            AddMultilinePageOptions(prevText, closeText, nextText, builder);
    }

    private void AddInlinePageOptions(string prevText, string closeText, string nextText, StringBuilder builder)
    {
        List<string> options = [];
        if (HasPrevButton) options.Add(prevText);
        if (HasExitButton) options.Add(closeText);
        if (HasNextButton) options.Add(nextText);
        builder.Append(string.Join(" | ", options));
    }

    private void AddMultilinePageOptions(string prevText, string closeText, string nextText, StringBuilder builder)
    {
        if (HasPrevButton) builder.Append($"{prevText}<br>");
        if (HasNextButton) builder.Append($"{nextText}<br>");
        if (HasExitButton) builder.Append($"{closeText}<br>");
    }

    private int CalculateMenuItemsPerPage()
    {
        int count = NumPerPage;
        if (!((CenterHtmlMenu)Menu).CenterHtmlMenu_InlinePageOptions)
        {
            if (!HasPrevButton) count++;
            if (!HasNextButton) count++;
        }
        else
        {
            count++;
            if (!HasExitButton && !HasPrevButton && !HasNextButton) count++;
        }
        return count;
    }
}
