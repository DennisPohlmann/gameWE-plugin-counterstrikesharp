// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GameWE;

/// <summary>
/// configs/plugins/GameWE/GameWE.json, written with these defaults on the
/// first start. The names follow the ConVars of the SourceMod plugin
/// (gamewe_message_prefix -> MessagePrefix, ...).
/// </summary>
public sealed class GameWEConfig : BasePluginConfig
{
    /// <summary>
    /// Where the plugin posts its log lines. Empty (default): the address the
    /// server got with logaddress_add_http (the collector sets it over RCON).
    /// Keep it secret: the path holds the collector's token.
    /// </summary>
    [JsonPropertyName("LogUrl")] public string LogUrl { get; set; } = string.Empty;

    /// <summary>
    /// Adds the log address again when someone removes it
    /// (logaddress_delall_http, logaddress_del_http) and turns logging on
    /// again after "log off" (gamewe_protect_address of the SourceMod plugin).
    /// </summary>
    [JsonPropertyName("ProtectLogAddress")] public bool ProtectLogAddress { get; set; } = false;

    /// <summary>Prefix of every gameWE chat line, also of the collector's answers (color tags allowed).</summary>
    [JsonPropertyName("MessagePrefix")] public string MessagePrefix { get; set; } = string.Empty;

    /// <summary>More chat commands to hide with "/", separated by spaces; "clear" for none.</summary>
    [JsonPropertyName("BlockCommandsValues")] public string BlockCommandsValues { get; set; } = string.Empty;

    /// <summary>Seconds a player has to wait between two gameWE commands (0-30, 0 = off).</summary>
    [JsonPropertyName("ChatCooldown")] public float ChatCooldown { get; set; } = 1.0f;

    /// <summary>Default for players: damage summary on death and round end (0 off, 1 panel, 2 chat).</summary>
    [JsonPropertyName("DamageDisplay")] public int DamageDisplay { get; set; } = 0;

    /// <summary>Allow the damage display at all (false: off for everyone, no damage tracking).</summary>
    [JsonPropertyName("DamageDisplayAllow")] public bool DamageDisplayAllow { get; set; } = true;

    /// <summary>Default for players: the stats of the spectated player (0 off, 1 on).</summary>
    [JsonPropertyName("DisplaySpectatorinfo")] public int DisplaySpectatorinfo { get; set; } = 0;

    /// <summary>Allow the spectator info at all.</summary>
    [JsonPropertyName("DisplaySpectatorinfoAllow")] public bool DisplaySpectatorinfoAllow { get; set; } = true;

    /// <summary>"center" (default): menus in the center of the screen; "chat": menus in chat (!1 - !9).</summary>
    [JsonPropertyName("MenuType")] public string MenuType { get; set; } = "center";

    /// <summary>Takes only valid values; a wrong value falls back to the default.</summary>
    public void Validate()
    {
        LogUrl = LogUrl.Trim();
        if (LogUrl.Length > 0 && !Core.CommandText.IsLogUrl(LogUrl))
        {
            LogUrl = string.Empty;
        }
        if (MessagePrefix.Length > 63)
        {
            MessagePrefix = MessagePrefix.Substring(0, 63);
        }
        MessagePrefix = Core.ChatText.StripControl(MessagePrefix);
        if (BlockCommandsValues.Length > 1024)
        {
            BlockCommandsValues = BlockCommandsValues.Substring(0, 1024);
        }
        if (float.IsNaN(ChatCooldown) || ChatCooldown < 0f || ChatCooldown > 30f)
        {
            ChatCooldown = 1.0f;
        }
        if (DamageDisplay is < 0 or > 2)
        {
            DamageDisplay = 0;
        }
        if (DisplaySpectatorinfo is < 0 or > 1)
        {
            DisplaySpectatorinfo = 0;
        }
        MenuType = MenuType.Trim().Equals("chat", StringComparison.OrdinalIgnoreCase) ? "chat" : "center";
    }
}
