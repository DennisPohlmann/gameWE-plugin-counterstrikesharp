// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;

namespace GameWE.Core;

/// <summary>
/// Chat colors and the message prefix. CS2 has the fixed chat colors of
/// CS:GO; the tags are the ones of the SourceMod plugin.
/// </summary>
public static class ChatText
{
    // the collector starts every chat line with this tag
    public const string ChatTag = "[gameWE]";

    private static readonly (string Tag, char Code)[] ColorTags =
    {
        ("{default}", '\x01'), ("{teamcolor}", '\x03'), ("{team}", '\x03'), ("{green}", '\x04'),
        ("{lightgreen}", '\x05'), ("{olive}", '\x05'), ("{red}", '\x07'), ("{lightred}", '\x0F'),
        ("{darkred}", '\x02'), ("{blue}", '\x0C'), ("{lightblue}", '\x0B'), ("{grey}", '\x08'),
        ("{gray}", '\x08'), ("{yellow}", '\x09'), ("{orange}", '\x10'), ("{purple}", '\x0E'),
        ("{lime}", '\x06'),
    };

    /// <summary>
    /// Replaces the color tags <c>{green}</c>, ... and gameME's <c>x01</c>,
    /// <c>x03</c>, <c>x04</c>, <c>x05</c>, <c>x07</c>, <c>x08</c> by chat color codes.
    /// </summary>
    public static string ColorEntities(string message)
    {
        if (message.Contains('{'))
        {
            foreach (var (tag, code) in ColorTags)
            {
                message = message.Replace(tag, code.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }
        return message
            .Replace("x08", "\x08").Replace("x07", "\x07").Replace("x05", "\x05")
            .Replace("x04", "\x04").Replace("x03", "\x03").Replace("x01", "\x01")
            .Replace("x||0", "x0");
    }

    /// <summary>
    /// CS2 ignores a color code in the first byte of a chat line: a space
    /// goes in front of it.
    /// </summary>
    public static string ChatStart(string message) =>
        message.Length > 0 && message[0] > '\0' && message[0] <= '\x10' ? " " + message : message;

    /// <summary>With a message prefix set, it replaces the [gameWE] tag at the start.</summary>
    public static string ApplyPrefix(string message, string prefix)
    {
        if (prefix.Length == 0 || !message.StartsWith(ChatTag, StringComparison.Ordinal))
        {
            return message;
        }
        return string.Concat(prefix, "\x01", message.AsSpan(ChatTag.Length));
    }

    /// <summary>
    /// Removes control characters except the chat colors 0x01-0x10 (the
    /// collector removes control characters, so these only come from color tags).
    /// </summary>
    public static string StripControl(string message)
    {
        var builder = new StringBuilder(message.Length);
        foreach (char c in message)
        {
            if (c >= ' ' || (c >= '\x01' && c <= '\x10'))
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Text for the center HTML panel: escaped, so names cannot inject markup,
    /// and lines joined with &lt;br&gt;. The collector separates lines with a
    /// literal \n (backslash, n).
    /// </summary>
    public static string Html(string text)
    {
        var lines = text.Replace("\\n", "\n").Split('\n');
        return string.Join("<br>", lines.Select(line => WebUtility.HtmlEncode(StripColors(line))));
    }

    /// <summary>The text without color codes and tags, for HTML and the console.</summary>
    public static string StripColors(string text)
    {
        foreach (var (tag, _) in ColorTags)
        {
            text = text.Replace(tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= ' ')
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>The chat lines of a text with \n (backslash, n) line breaks.</summary>
    public static IEnumerable<string> Lines(string text) =>
        text.Replace("\\n", "\n").Split('\n').Select(StripControl).Where(line => line.Trim().Length > 0);
}
