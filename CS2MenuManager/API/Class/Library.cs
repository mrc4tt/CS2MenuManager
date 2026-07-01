using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using Tomlyn.Model;
using static CS2MenuManager.API.Class.ConfigManager;

namespace CS2MenuManager.API.Class;

internal static partial class Library
{
    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)] private static partial Regex TagRegex();

    public static void SaveSpeed(this CCSPlayerController player, ref float oldModifier)
    {
        if (player.PlayerPawn.Value is { } playerPawn)
            oldModifier = playerPawn.VelocityModifier;
    }

    public static void Freeze(this CCSPlayerController player)
    {
        if (player.PlayerPawn.Value is { } playerPawn)
            playerPawn.VelocityModifier = 0.0f;
    }

    public static void Unfreeze(this CCSPlayerController player, float oldModifier)
    {
        if (player.PlayerPawn.Value is { } playerPawn)
            playerPawn.VelocityModifier = oldModifier;
    }

    /// <summary>
    /// Gets the player's current vertical look angle (pitch). Positive looks down, negative up.
    /// Returns 0 when the pawn is unavailable.
    /// </summary>
    public static float GetEyePitch(this CCSPlayerController player)
    {
        return player.PlayerPawn.Value is { } playerPawn ? playerPawn.EyeAngles.X : 0f;
    }

    public static string Localizer(this CCSPlayerController player, string key, params object?[] args)
    {
        CultureInfo cultureInfo = player.GetLanguage();

        if (Config.Lang.TryGetValue(cultureInfo.Name, out Dictionary<string, string>? lang) && lang.TryGetValue(key, out string? text))
            return SafeFormat(text, args);

        string shortName = cultureInfo.TwoLetterISOLanguageName.ToLower();
        return Config.Lang.TryGetValue(shortName, out lang) && lang.TryGetValue(key, out text)
            ? SafeFormat(text, args)
            : Config.Lang.TryGetValue("en", out lang) && lang.TryGetValue(key, out text) ? SafeFormat(text, args) : key;
    }

    private static string SafeFormat(string text, object?[] args)
    {
        string formatted = text.ReplaceColorTags();

        if (args.Length == 0)
            return formatted;

        try
        {
            return string.Format(formatted, args);
        }
        catch (FormatException)
        {
            // Malformed placeholder (e.g. a stray '{' or '}' in the lang string) — return raw.
            return formatted;
        }
    }

    public static string TruncateHtml(this string html, int maxLength)
    {
        if (maxLength <= 0 || string.IsNullOrEmpty(html))
            return html;

        string textOnly = TagRegex().Replace(html, string.Empty);
        if (textOnly.Length <= maxLength)
            return html;

        Stack<string> tagStack = new();
        StringBuilder result = new();
        int visibleLength = 0, i = 0;

        while (i < html.Length && visibleLength < maxLength)
        {
            if (html[i] == '<')
            {
                Match match = TagRegex().Match(html, i);
                if (match.Success && match.Index == i)
                {
                    string tag = match.Value;
                    result.Append(tag);
                    i += tag.Length;

                    if (!tag.StartsWith("</", StringComparison.Ordinal))
                    {
                        string tagName = tag.Split([' ', '>', '/'], StringSplitOptions.RemoveEmptyEntries)[0].TrimStart('<');
                        if (!tag.EndsWith("/>", StringComparison.Ordinal) && !tagName.StartsWith('!'))
                            tagStack.Push(tagName);
                    }
                    else if (tagStack.Count > 0)
                    {
                        tagStack.Pop();
                    }

                    continue;
                }
            }

            result.Append(html[i]);
            visibleLength++;
            i++;
        }

        while (tagStack.Count > 0)
            result.Append($"</{tagStack.Pop()}>");

        return result.ToString();
    }

    private static readonly Dictionary<string, char> ChatColorCache = BuildChatColorCache();

    private static Dictionary<string, char> BuildChatColorCache()
    {
        // ChatColors exposes aliases that collide case-insensitively (e.g. "Darkred" / "DarkRed"),
        // so fill manually and keep the first occurrence rather than using ToDictionary (which throws on dupes).
        Dictionary<string, char> cache = new(StringComparer.OrdinalIgnoreCase);

        foreach (System.Reflection.FieldInfo field in typeof(ChatColors)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(char)))
        {
            cache.TryAdd(field.Name, (char)field.GetValue(null)!);
        }

        return cache;
    }

    public static char GetChatColor(this string colorName)
    {
        return ChatColorCache.TryGetValue(colorName, out char color) ? color : ChatColors.White;
    }

    public static T GetValueOrDefault<T>(this TomlTable table, string key, T defaultValue)
    {
        if (!table.TryGetValue(key, out object value))
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}