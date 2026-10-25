// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Headers;
using System.Text;

namespace GameWE.Core;

/// <summary>
/// Sends the plugin's log lines to the collector by HTTP POST, the way CS2
/// sends its own log (logaddress_add_http): one line per event, separated by
/// line breaks. Lines are collected and sent about once per second from a
/// background task, so the game thread never waits for the network. If the
/// collector cannot be reached, the lines are dropped (as CS2 does with its
/// own log); the queue is limited.
/// </summary>
public sealed class LogSender : IDisposable
{
    public const int MaxQueuedLines = 5000;
    // the collector's limit per request is 128 KiB (listen.http.max_body)
    public const int MaxBodyBytes = 64 * 1024;

    private readonly object _lock = new();
    private readonly Queue<string> _lines = new();
    private readonly HttpClient _http;
    private readonly Action<string> _warn;
    private readonly Timer _timer;
    private string _url = string.Empty;
    private int _sending;
    private DateTime _lastWarning = DateTime.MinValue;
    private int _dropped;

    public LogSender(Action<string> warn, HttpMessageHandler? handler = null, TimeSpan? interval = null)
    {
        _warn = warn;
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(5);
        var every = interval ?? TimeSpan.FromSeconds(1);
        _timer = new Timer(_ => _ = FlushAsync(), null, every, every);
    }

    /// <summary>The address the lines go to; empty = none known yet (lines are kept).</summary>
    public string Url
    {
        get { lock (_lock) { return _url; } }
        set { lock (_lock) { _url = value; } }
    }

    public bool HasUrl => Url.Length > 0;

    public int QueuedLines
    {
        get { lock (_lock) { return _lines.Count; } }
    }

    /// <summary>Queues one log line, with the time stamp of now.</summary>
    public void Add(string line, DateTime time)
    {
        string full = LogFormat.Timestamp(time) + line;
        if (full.Length > LogFormat.MaxLineLength)
        {
            full = full.Substring(0, LogFormat.MaxLineLength);
        }
        lock (_lock)
        {
            if (_lines.Count >= MaxQueuedLines)
            {
                _lines.Dequeue();
                _dropped++;
            }
            _lines.Enqueue(full);
        }
    }

    /// <summary>Sends what is queued; one request at a time.</summary>
    public async Task FlushAsync()
    {
        if (Interlocked.Exchange(ref _sending, 1) == 1)
        {
            return;
        }
        try
        {
            while (true)
            {
                string url;
                string body;
                lock (_lock)
                {
                    url = _url;
                    if (url.Length == 0 || _lines.Count == 0)
                    {
                        return;
                    }
                    var builder = new StringBuilder();
                    while (_lines.Count > 0 && builder.Length + _lines.Peek().Length + 1 <= MaxBodyBytes / 2)
                    {
                        builder.Append(_lines.Dequeue()).Append('\n');
                    }
                    if (builder.Length == 0)
                    {
                        builder.Append(_lines.Dequeue()).Append('\n');
                    }
                    body = builder.ToString();
                }
                if (!await PostAsync(url, body).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        finally
        {
            Volatile.Write(ref _sending, 0);
        }
    }

    private async Task<bool> PostAsync(string url, string body)
    {
        try
        {
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "utf-8" };
            using var response = await _http.PostAsync(url, content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // the address is not written: its path holds the collector's token
                Warn($"the collector answered HTTP {(int)response.StatusCode}, log lines dropped");
            }
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Warn($"the collector cannot be reached ({ex.GetType().Name}), log lines dropped");
            return false;
        }
    }

    // at most one warning per minute
    private void Warn(string message)
    {
        var now = DateTime.UtcNow;
        if (now - _lastWarning < TimeSpan.FromMinutes(1))
        {
            return;
        }
        _lastWarning = now;
        int dropped = Interlocked.Exchange(ref _dropped, 0);
        _warn(dropped > 0 ? $"{message} ({dropped} more dropped because the queue was full)" : message);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _http.Dispose();
    }
}
