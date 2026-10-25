// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace GameWE.Core;

/// <summary>
/// Reads the arguments of the collector's server commands the way the
/// SourceMod plugin does: leading arguments may be quoted or not, the
/// message is the rest of the line without surrounding quotes.
/// </summary>
public static class CommandText
{
    // the most players one command may address
    public const int MaxRecipients = 64;

    /// <summary>
    /// The text after the first <paramref name="skip"/> arguments of
    /// <paramref name="argString"/>, trimmed and without surrounding quotes;
    /// empty when there is nothing after them (SourceMod's get_cmd_message).
    /// </summary>
    public static string Message(string argString, int skip)
    {
        int pos = 0;
        for (int i = 0; i < skip; i++)
        {
            int next = NextArgument(argString, pos);
            if (next < 0)
            {
                return string.Empty;
            }
            pos = next;
        }
        return StripQuotes(argString.Substring(pos).Trim());
    }

    // index of the argument after the one at pos (BreakString), -1 if none follows
    private static int NextArgument(string text, int pos)
    {
        int i = pos;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        if (i >= text.Length)
        {
            return -1;
        }
        if (text[i] == '"')
        {
            int close = text.IndexOf('"', i + 1);
            i = close < 0 ? text.Length : close + 1;
        }
        else
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        return i >= text.Length ? -1 : i;
    }

    public static string StripQuotes(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text.Substring(1, text.Length - 2) : text;

    /// <summary>The user ids of "2,5,7" (or "2"); invalid parts are skipped.</summary>
    public static List<int> UserIds(string list)
    {
        var ids = new List<int>();
        foreach (string part in StripQuotes(list.Trim()).Split(','))
        {
            if (ids.Count >= MaxRecipients)
            {
                break;
            }
            if (int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    public static int Int(string text, int fallback) =>
        int.TryParse(StripQuotes(text.Trim()), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    /// <summary>Values passed to Server.ExecuteCommand must not chain or break commands.</summary>
    public static bool IsSafeCommandValue(string value)
    {
        foreach (char c in value)
        {
            if (c == ';' || c == '"' || c == '\n' || c == '\r' || char.IsControl(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Only http:// and https:// URLs are shown to players.</summary>
    public static bool IsWebUrl(string url) =>
        url.Length <= 512 &&
        (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
        url.All(c => c > ' ' && c != '"' && c < 0x7f);

    /// <summary>
    /// The log address the plugin posts to: http(s)://host[:port]/path, at
    /// most 512 characters, nothing that could break a command.
    /// </summary>
    public static bool IsLogUrl(string url)
    {
        if (!IsWebUrl(url) || !IsSafeCommandValue(url))
        {
            return false;
        }
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               uri.Host.Length > 0 && string.IsNullOrEmpty(uri.UserInfo);
    }

    /// <summary>A server address for gamewe_redirect: host or IP with optional port.</summary>
    public static bool IsServerAddress(string address) =>
        address.Length is > 0 and <= 128 &&
        address.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ':' || c == '-' || c == '[' || c == ']');
}
