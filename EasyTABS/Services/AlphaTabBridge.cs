using System.Text;
using System.Text.Json;

namespace EasyTABS.Services
{
    // Розпарсена інформація про партитуру, що прийшла з JS (scoreLoaded).
    public class ScoreInfo
    {
        public string Title { get; set; } = string.Empty;
        public double Tempo { get; set; } = 120;
        public List<TrackInfo> Tracks { get; set; } = new();
    }

    public class TrackInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Позиція відтворення (мс).
    public class PlaybackPosition
    {
        public int Current { get; set; }
        public int End { get; set; }
    }

    /// <summary>
    /// Обгортка над WebView, що спілкується з alphatab_host.html.
    /// C# -> JS: EvaluateJavaScriptAsync (виклик функцій).
    /// JS -> C#: hash-навігація "#athost=...", яку ловить сторінка й передає сюди.
    /// </summary>
    public class AlphaTabBridge
    {
        private readonly WebView _webView;

        public event Action? Ready;
        public event Action? PlayerReady;
        public event Action<ScoreInfo>? ScoreLoaded;
        public event Action<int>? PlayerStateChanged;      // 0 пауза / 1 грає
        public event Action<PlaybackPosition>? PositionChanged;
        public event Action<int>? ActiveNoteChanged;       // MIDI, -1 = немає
        public event Action<string>? ErrorOccurred;

        public AlphaTabBridge(WebView webView)
        {
            _webView = webView;
        }

        // Викликається з code-behind у обробнику Navigating.
        // Повертає true, якщо це наше внутрішнє повідомлення (перехід треба скасувати).
        public bool TryHandleNavigation(string url)
        {
            const string scheme = "athost://";
            if (string.IsNullOrEmpty(url) ||
                !url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            const string marker = "data=";
            var idx = url.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return true; // наша схема, але без даних — просто гасимо

            var raw = url.Substring(idx + marker.Length);
            var amp = raw.IndexOf('&');
            if (amp >= 0) raw = raw.Substring(0, amp);

            var decoded = Uri.UnescapeDataString(raw);
            Dispatch(decoded);
            return true;
        }

        private void Dispatch(string message)
        {
            // Формат: "type" або "type:payload".
            string type;
            string payload = string.Empty;
            var colon = message.IndexOf(':');
            if (colon >= 0)
            {
                type = message.Substring(0, colon);
                payload = message.Substring(colon + 1);
            }
            else
            {
                type = message;
            }

            switch (type)
            {
                case "ready":
                    Ready?.Invoke();
                    break;
                case "playerReady":
                    PlayerReady?.Invoke();
                    break;
                case "scoreLoaded":
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        var info = new ScoreInfo
                        {
                            Title = root.GetProperty("title").GetString() ?? string.Empty,
                            Tempo = root.GetProperty("tempo").GetDouble()
                        };
                        foreach (var t in root.GetProperty("tracks").EnumerateArray())
                            info.Tracks.Add(new TrackInfo
                            {
                                Index = t.GetProperty("index").GetInt32(),
                                Name = t.GetProperty("name").GetString() ?? string.Empty
                            });
                        ScoreLoaded?.Invoke(info);
                    }
                    catch { }
                    break;
                case "playerState":
                    if (int.TryParse(payload, out var st)) PlayerStateChanged?.Invoke(st);
                    break;
                case "position":
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        PositionChanged?.Invoke(new PlaybackPosition
                        {
                            Current = root.GetProperty("current").GetInt32(),
                            End = root.GetProperty("end").GetInt32()
                        });
                    }
                    catch { }
                    break;
                case "activeNote":
                    if (int.TryParse(payload, out var midi)) ActiveNoteChanged?.Invoke(midi);
                    break;
                case "error":
                    ErrorOccurred?.Invoke(payload);
                    break;
            }
        }

        // ---- Команди в JS ----
        private async Task EvalAsync(string js)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await _webView.EvaluateJavaScriptAsync(js));
            }
            catch { }
        }

        public Task LoadScoreAsync(byte[] tabBytes)
        {
            var b64 = System.Convert.ToBase64String(tabBytes);
            return EvalAsync($"loadScoreBase64('{b64}')");
        }

        public Task PlayPauseAsync() => EvalAsync("playPause()");
        public Task StopAsync() => EvalAsync("stop()");
        public Task SetTrackAsync(int index) => EvalAsync($"setTrack({index})");
        public Task SetSpeedAsync(double factor)
            => EvalAsync($"setSpeed({factor.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        public Task SetMetronomeAsync(bool on) => EvalAsync($"setMetronome({(on ? "true" : "false")})");
        public Task SetMasterVolumeAsync(double v)
            => EvalAsync($"setMasterVolume({v.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        public Task SeekAsync(int ms) => EvalAsync($"seek({ms})");
    }
}
