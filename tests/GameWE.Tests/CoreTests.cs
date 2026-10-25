// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text.Json;
using GameWE.Core;
using Xunit;

namespace GameWE.Tests;

public class LogFormatTests
{
    [Fact]
    public void TimestampIsTheCs2Prefix()
    {
        Assert.Equal("10/25/2026 - 22:40:05.007 - ", LogFormat.Timestamp(new DateTime(2026, 10, 25, 22, 40, 5, 7)));
    }

    [Theory]
    [InlineData(76561197960265729UL, false, "[U:1:1]")]
    [InlineData(76561198000000000UL, false, "[U:1:39734272]")]
    [InlineData(0UL, false, "STEAM_ID_PENDING")]
    [InlineData(12345UL, false, "STEAM_ID_PENDING")]
    [InlineData(0UL, true, "BOT")]
    public void UniqueIdLikeCs2(ulong steamId, bool bot, string expected)
    {
        Assert.Equal(expected, LogFormat.UniqueId(steamId, bot));
    }

    [Fact]
    public void PlayerRefLikeCs2()
    {
        Assert.Equal("Name<2><[U:1:1]><CT>", LogFormat.PlayerRef("Name", 2, "[U:1:1]", 3));
        Assert.Equal("Bot<7><BOT><TERRORIST>", LogFormat.PlayerRef("Bot", 7, "BOT", 2));
        Assert.Equal("X<1><BOT><Spectator>", LogFormat.PlayerRef("X", 1, "BOT", 1));
        Assert.Equal("X<1><BOT><Unassigned>", LogFormat.PlayerRef("X", 1, "BOT", 0));
    }

    [Fact]
    public void NamesCannotFakeEvents()
    {
        // a name that tries to close the field and add a kill
        string name = "Evil\"<3><[U:1:9]><CT>\" killed \"x\nL 10/25/2026";
        string reference = LogFormat.PlayerRef(name, 2, "[U:1:1]", 3);
        Assert.DoesNotContain("\"", reference);
        Assert.DoesNotContain("\n", reference);
        string line = LogFormat.Event(reference, "say", "/rank\" with \"ak47");
        Assert.Equal(4, line.Count(c => c == '"'));
    }

    [Fact]
    public void SanitizeLimitsTheLength()
    {
        Assert.Equal(128, LogFormat.Sanitize(new string('a', 500)).Length);
        Assert.Equal(string.Empty, LogFormat.Sanitize(null));
    }

    [Fact]
    public void LinesOfTheSourceModPlugin()
    {
        const string p = "Name<2><[U:1:1]><CT>";
        Assert.Equal("\"Name<2><[U:1:1]><CT>\" triggered \"headshot\"", LogFormat.Event(p, "triggered", "headshot"));
        Assert.Equal("\"Name<2><[U:1:1]><CT>\" triggered \"domination\" against \"V<3><BOT><TERRORIST>\"",
            LogFormat.EventAgainst(p, "triggered", "domination", "V<3><BOT><TERRORIST>"));
        Assert.Equal("\"Name<2><[U:1:1]><CT>\" setup \"gamewe_lang\" (value \"de\")", LogFormat.Setting(p, "setup", "gamewe_lang", "de"));
        Assert.Equal("Team \"CT\" triggered \"x\" (object \"y\")", LogFormat.Team("CT", "x", "y"));
        Assert.Equal("Team \"CT\" triggered \"x\"", LogFormat.Team("CT", "x", ""));
        Assert.Equal("World triggered \"Round_Start\"", LogFormat.World("Round_Start"));
    }
}

public class WeaponStatsTests
{
    private const string P = "Name<2><[U:1:1]><CT>";

    [Theory]
    [InlineData("weapon_ak47", "ak47")]
    [InlineData("AK47", "ak47")]
    [InlineData("knife_t", "knife_t")]
    [InlineData("world", null)]
    [InlineData("", null)]
    [InlineData("ak47\" (headshot", null)]
    [InlineData("weapon_", null)]
    public void NormalizeWeapon(string input, string? expected)
    {
        Assert.Equal(expected, PlayerWeaponStats.NormalizeWeapon(input));
    }

    [Fact]
    public void FlushWritesTheSourceModFormat()
    {
        var stats = new PlayerWeaponStats();
        for (int i = 0; i < 14; i++)
        {
            stats.Shot("weapon_ak47");
        }
        stats.Hit("ak47", 100);
        stats.Hit("ak47", 27);
        stats.Kill("ak47", headshot: true, teamkill: false);
        var lines = stats.Flush(P);
        Assert.Equal(new[]
        {
            "\"Name<2><[U:1:1]><CT>\" triggered \"weaponstats\" (weapon \"ak47\") (shots \"14\") (hits \"2\") (kills \"1\") (headshots \"1\") (tks \"0\") (damage \"127\") (deaths \"0\")",
        }, lines);
        Assert.True(stats.IsEmpty);
        Assert.Empty(stats.Flush(P));
    }

    [Fact]
    public void GrenadesCountNoShots()
    {
        var stats = new PlayerWeaponStats();
        stats.Shot("weapon_hegrenade");
        stats.Shot("weapon_molotov");
        stats.Kill("hegrenade", false, false);
        Assert.Empty(stats.Flush(P));
    }

    [Fact]
    public void AHitWithoutAShotCountsOneShot()
    {
        var stats = new PlayerWeaponStats();
        stats.Hit("deagle", 50);
        Assert.Contains("(shots \"1\") (hits \"1\")", stats.Flush(P)[0]);
    }

    [Fact]
    public void DeathsAndTeamkills()
    {
        var stats = new PlayerWeaponStats();
        stats.Shot("weapon_m4a1_silencer");
        stats.Kill("m4a1_silencer", false, teamkill: true);
        stats.Death("m4a1_silencer");
        Assert.Contains("(tks \"1\") (damage \"0\") (deaths \"1\")", stats.Flush(P)[0]);
    }

    [Fact]
    public void WeaponCountIsLimited()
    {
        var stats = new PlayerWeaponStats();
        for (int i = 0; i < 200; i++)
        {
            stats.Shot("w" + i);
        }
        Assert.Equal(PlayerWeaponStats.MaxWeapons, stats.Flush(P).Count);
    }
}

public class CommandTextTests
{
    [Theory]
    [InlineData("2 1 \"[gameWE] Rank 1\"", 2, "[gameWE] Rank 1")]
    [InlineData("\"2\" 1 \"[gameWE] Rank 1\"", 2, "[gameWE] Rank 1")]
    [InlineData("2 [gameWE] no quotes here", 1, "[gameWE] no quotes here")]
    [InlineData("2,3,4 0 \"text\"", 2, "text")]
    [InlineData("2", 1, "")]
    [InlineData("", 0, "")]
    [InlineData("\"only\"", 0, "only")]
    [InlineData("10 2 1 \"menu\\nline\"", 3, "menu\\nline")]
    public void MessageLikeGetCmdMessage(string args, int skip, string expected)
    {
        Assert.Equal(expected, CommandText.Message(args, skip));
    }

    [Fact]
    public void UserIds()
    {
        Assert.Equal(new[] { 2, 3, 4 }, CommandText.UserIds("2,3,4"));
        Assert.Equal(new[] { 2 }, CommandText.UserIds("\"2\""));
        Assert.Equal(new[] { 2, 5 }, CommandText.UserIds("2,x,-1,5,2"));
        Assert.Equal(CommandText.MaxRecipients, CommandText.UserIds(string.Join(",", Enumerable.Range(1, 500))).Count);
    }

    [Theory]
    [InlineData("http://collector:41338/abcdefabcdefabcd/27015", true)]
    [InlineData("https://stats.example.org/token/27015", true)]
    [InlineData("ftp://x/y", false)]
    [InlineData("http://x/y;quit", false)]
    [InlineData("http://x/y\"", false)]
    [InlineData("http://user:pw@x/y", false)]
    [InlineData("http://x/a b", false)]
    [InlineData("javascript:alert(1)", false)]
    public void LogUrl(string url, bool valid)
    {
        Assert.Equal(valid, CommandText.IsLogUrl(url));
    }

    [Theory]
    [InlineData("https://example.org/stats", true)]
    [InlineData("HTTP://example.org", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("www.example.org", false)]
    public void WebUrl(string url, bool valid)
    {
        Assert.Equal(valid, CommandText.IsWebUrl(url));
    }

    [Theory]
    [InlineData("203.0.113.5:27015", true)]
    [InlineData("cs2.example.org:27015", true)]
    [InlineData("203.0.113.5;quit", false)]
    [InlineData("", false)]
    public void ServerAddress(string address, bool valid)
    {
        Assert.Equal(valid, CommandText.IsServerAddress(address));
    }

    [Fact]
    public void SafeCommandValue()
    {
        Assert.True(CommandText.IsSafeCommandValue("http://x:1/y"));
        Assert.False(CommandText.IsSafeCommandValue("a;b"));
        Assert.False(CommandText.IsSafeCommandValue("a\nb"));
        Assert.False(CommandText.IsSafeCommandValue("a\"b"));
    }
}

public class ChatTextTests
{
    [Fact]
    public void ColorTagsAndGameMeCodes()
    {
        Assert.Equal("\x04green\x01 x", ChatText.ColorEntities("{green}green{default} x"));
        Assert.Equal("\x04" + "a\x01", ChatText.ColorEntities("x04ax01"));
        Assert.Equal("\x10orange", ChatText.ColorEntities("{ORANGE}orange"));
    }

    [Fact]
    public void ColorAtTheStartGetsASpace()
    {
        Assert.Equal(" \x04x", ChatText.ChatStart("\x04x"));
        Assert.Equal("x", ChatText.ChatStart("x"));
    }

    [Fact]
    public void PrefixReplacesTheTag()
    {
        Assert.Equal("\x04[Srv]\x01 Rank 1", ChatText.ApplyPrefix("[gameWE] Rank 1", "\x04[Srv]"));
        Assert.Equal("[gameWE] Rank 1", ChatText.ApplyPrefix("[gameWE] Rank 1", ""));
        Assert.Equal("Rank 1", ChatText.ApplyPrefix("Rank 1", "[Srv]"));
    }

    [Fact]
    public void HtmlIsEscaped()
    {
        Assert.Equal("Top 1<br>&lt;b&gt;Evil&lt;/b&gt; &amp; co", ChatText.Html("Top 1\\n<b>Evil</b> & co"));
        Assert.Equal("a", ChatText.Html("{green}a\x04"));
    }

    [Fact]
    public void Lines()
    {
        Assert.Equal(new[] { "a", "b" }, ChatText.Lines("a\\nb\\n "));
    }
}

public class PlayerSettingsTests
{
    [Fact]
    public void DefaultsAreNotStored()
    {
        string path = Path.Combine(Path.GetTempPath(), "gamewe-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new PlayerSettingsStore(path);
            Assert.False(store.Set(76561197960265729UL, new PlayerSettings()));
            var settings = new PlayerSettings { DamageDisplay = 2, Lang = "de", AutoStart = "rank" };
            Assert.True(store.Set(76561197960265729UL, settings));
            store.Save();

            var loaded = new PlayerSettingsStore(path);
            loaded.Load();
            var got = loaded.Get(76561197960265729UL);
            Assert.Equal(2, got.DamageDisplay);
            Assert.Equal("de", got.Lang);
            Assert.Equal("rank", got.AutoStart);
            Assert.True(loaded.Get(1).IsDefault);

            Assert.True(loaded.Set(76561197960265729UL, new PlayerSettings()));
            Assert.Equal(0, loaded.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidValuesInTheFileAreDropped()
    {
        string path = Path.Combine(Path.GetTempPath(), "gamewe-test-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["76561197960265729"] = new { damage_display = 9, spectatorinfo = 1, auto_kill = "quit", lang = "xx" },
                ["not-a-steamid"] = new { damage_display = 1 },
            }));
            var store = new PlayerSettingsStore(path);
            store.Load();
            Assert.Equal(1, store.Count);
            var got = store.Get(76561197960265729UL);
            Assert.Equal(PlayerSettings.ServerDefault, got.DamageDisplay);
            Assert.Equal(1, got.SpectatorInfo);
            Assert.Equal(string.Empty, got.AutoKill);
            Assert.Equal(string.Empty, got.Lang);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class PlayerCommandTests
{
    [Fact]
    public void AutoCommandsAreOnlyStatsCommands()
    {
        Assert.Equal("top10", AutoCommands.Find("TOP10"));
        Assert.Null(AutoCommands.Find("say hi"));
        Assert.Null(AutoCommands.Find("quit"));
        Assert.Equal(AutoEvent.Kill, AutoCommands.Event("kill"));
        Assert.Null(AutoCommands.Event("x"));
    }

    [Fact]
    public void Languages()
    {
        Assert.Equal("da", Core.Languages.Culture("dk"));
        Assert.Equal("de", Core.Languages.Culture("DE"));
        Assert.Null(Core.Languages.Culture("xx"));
    }

    [Fact]
    public void BlockedCommands()
    {
        var blocked = new BlockedCommands();
        Assert.True(blocked.IsBlocked("/rank"));
        Assert.True(blocked.IsBlocked("!top10"));
        Assert.True(blocked.IsBlocked("gameme_hideranking 1"));
        Assert.False(blocked.IsBlocked("/hello"));
        blocked.Apply("hello  world");
        Assert.True(blocked.IsBlocked("/hello"));
        Assert.True(blocked.IsBlocked("!world"));
        blocked.Apply("clear");
        Assert.False(blocked.IsBlocked("/rank"));
        Assert.True(Core.BlockedCommands.IsMenuCommand("!gameme"));
        Assert.True(Core.BlockedCommands.IsSettingsCommand("/gamewe_settings"));
        Assert.False(Core.BlockedCommands.IsSettingsCommand("gamewe_settings"));
    }
}

public class DamageTrackerTests
{
    [Fact]
    public void SummaryOfAPlayer()
    {
        var tracker = new DamageTracker();
        tracker.Hurt(1, 0, 30);  // 1 hurts 0
        tracker.Hurt(2, 0, 50);
        tracker.Hurt(2, 0, 50);
        tracker.Kill(2, 0, 64, "ak47"); // 2 kills 0
        tracker.Hurt(0, 3, 20);  // 0 hurts 3
        tracker.Hurt(0, 4, 100);
        tracker.Kill(0, 4, 100, "deagle");

        var summary = tracker.Build(0);
        Assert.Equal(new DamageTracker.Line(1, 1, 30, 0, ""), Assert.Single(summary.Attackers));
        Assert.Equal(new DamageTracker.Line(2, 2, 100, 64, "ak47"), Assert.Single(summary.Killers));
        Assert.Equal(3, Assert.Single(summary.Wounded).Slot);
        Assert.Equal(4, Assert.Single(summary.Killed).Slot);

        // the killer is shown once
        Assert.Empty(tracker.Build(0).Killers);
        tracker.Reset(0);
        Assert.True(tracker.Build(0).IsEmpty);
    }

    [Fact]
    public void InvalidSlotsAreIgnored()
    {
        var tracker = new DamageTracker();
        tracker.Hurt(-1, 0, 10);
        tracker.Hurt(0, 64, 10);
        tracker.Hurt(5, 5, 10);
        Assert.True(tracker.Build(0).IsEmpty);
        Assert.True(tracker.Build(5).IsEmpty);
    }
}

public class LogSenderTests
{
    private sealed class Recorder : HttpMessageHandler
    {
        public readonly List<(string Url, string Body, string? ContentType)> Requests = new();
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Fail;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new HttpRequestException("down");
            }
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.RequestUri!.ToString(), body, request.Content?.Headers.ContentType?.MediaType));
            }
            return new HttpResponseMessage(Status);
        }
    }

    [Fact]
    public async Task PostsTheLinesToTheUrl()
    {
        var recorder = new Recorder();
        using var sender = new LogSender(_ => { }, recorder, TimeSpan.FromHours(1));
        var time = new DateTime(2026, 10, 25, 22, 40, 0);
        sender.Add("gameWE Plugin 0.1.0 (CounterStrikeSharp)", time);
        sender.Add("\"A<2><BOT><CT>\" triggered \"mvp\"", time);

        // no address yet: the lines wait
        await sender.FlushAsync();
        Assert.Empty(recorder.Requests);
        Assert.Equal(2, sender.QueuedLines);

        sender.Url = "http://collector:41338/token0123456789ab/27015";
        await sender.FlushAsync();
        var request = Assert.Single(recorder.Requests);
        Assert.Equal("http://collector:41338/token0123456789ab/27015", request.Url);
        Assert.Equal("text/plain", request.ContentType);
        Assert.Equal("10/25/2026 - 22:40:00.000 - gameWE Plugin 0.1.0 (CounterStrikeSharp)\n" +
                     "10/25/2026 - 22:40:00.000 - \"A<2><BOT><CT>\" triggered \"mvp\"\n", request.Body);
        Assert.Equal(0, sender.QueuedLines);
    }

    [Fact]
    public async Task LargeQueuesAreSplitIntoRequests()
    {
        var recorder = new Recorder();
        using var sender = new LogSender(_ => { }, recorder, TimeSpan.FromHours(1)) { Url = "http://c/t/1" };
        for (int i = 0; i < 3000; i++)
        {
            sender.Add(new string('x', 100), DateTime.Now);
        }
        await sender.FlushAsync();
        Assert.True(recorder.Requests.Count > 1);
        Assert.All(recorder.Requests, r => Assert.True(r.Body.Length <= LogSender.MaxBodyBytes));
        Assert.Equal(3000, recorder.Requests.Sum(r => r.Body.Count(c => c == '\n')));
    }

    [Fact]
    public async Task TheQueueIsLimitedAndErrorsDoNotThrow()
    {
        var recorder = new Recorder { Fail = true };
        var warnings = new List<string>();
        using var sender = new LogSender(warnings.Add, recorder, TimeSpan.FromHours(1)) { Url = "http://c/secret-token/1" };
        for (int i = 0; i < LogSender.MaxQueuedLines + 10; i++)
        {
            sender.Add("line", DateTime.Now);
        }
        Assert.Equal(LogSender.MaxQueuedLines, sender.QueuedLines);
        await sender.FlushAsync();
        var warning = Assert.Single(warnings);
        // the address holds the collector's token: never in a message
        Assert.DoesNotContain("secret-token", warning);
    }
}

public class TranslationTests
{
    private static Dictionary<string, string> Load(string lang) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", lang + ".json")))!;

    [Theory]
    [InlineData("de")]
    [InlineData("ru")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("pt")]
    [InlineData("nl")]
    [InlineData("da")]
    [InlineData("pl")]
    public void EveryLanguageHasEveryPhrase(string lang)
    {
        var en = Load("en");
        var other = Load(lang);
        Assert.Equal(en.Keys.OrderBy(k => k), other.Keys.OrderBy(k => k));
        foreach (var (key, text) in en)
        {
            // the same placeholders, so string.Format never fails
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(text.Contains("{" + i + "}"), other[key].Contains("{" + i + "}"));
            }
        }
    }
}
