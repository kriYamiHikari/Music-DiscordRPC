using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicRpc.Models;
using MusicRpc.Players.Interfaces;

namespace MusicRpc.Players;

/// <summary>
/// 落雪音乐 (LX Music) 播放器的具体实现。
/// 通过播放器内置的 OpenAPI 获取播放状态，而非读取内存。
/// </summary>
internal sealed class LxMusic : IMusicPlayer
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiBaseUrl;
    private readonly bool _isEnabled;

    // 由于 API 不提供稳定的歌曲 ID
    // 当歌曲切换时，重新生成一个新的 GUID 作为 Identity。
    private string? _lastSongTitle;
    private string? _lastSongArtist;
    private string _currentSongId = Guid.NewGuid().ToString();

    public LxMusic(int pid)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        try
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "lx-music-desktop", "LxDatas", "config_v2.json");

            if (!File.Exists(configPath))
            {
                Debug.WriteLine("[LX Music] Config file not found.");
                _isEnabled = false;
                return;
            }

            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<LxMusicConfig>(configJson);

            if (config?.Setting is { Enable: true, Port: not null } settings)
            {
                _apiBaseUrl = $"http://localhost:{settings.Port}";
                _isEnabled = true;
                Debug.WriteLine($"[LX Music] API enabled on port: {settings.Port}");
            }
            else
            {
                _isEnabled = false;
                Debug.WriteLine("[LX Music] OpenAPI is disabled or could not be read from config file.");
                if (config?.Setting != null)
                {
                    Debug.WriteLine(
                        $"[LX Music] Debug Info: Found Enable={config.Setting.Enable}, Port={config.Setting.Port ?? "null"}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ERROR] Failed to initialize LX Music player: {e.Message}");
            _isEnabled = false;
        }
    }

    public PlayerInfo? GetPlayerInfo()
    {
        if (!_isEnabled || string.IsNullOrEmpty(_apiBaseUrl)) return null;

        try
        {
            // 请求 API 以获取所有需要的字段
            var requestUrl = $"{_apiBaseUrl}/status?filter=status,name,singer,albumName,duration,progress,picUrl";
            var responseJson = _httpClient.GetStringAsync(requestUrl).Result;
            var status = JsonSerializer.Deserialize<LxMusicStatus>(responseJson);

            if (status is null || string.IsNullOrEmpty(status.Name)) return null;

            // 如果播放器状态不是 "playing" 或 "paused"，则认为没有在播放
            var isPaused = status.Status switch
            {
                "playing" => false,
                "paused" => true,
                _ => (bool?)null
            };

            if (isPaused is null) return null;

            // 检查歌曲是否已切换，如果切换了，就生成一个新的 Identity
            // ReSharper disable once InvertIf
            if (status.Name != _lastSongTitle || status.Singer != _lastSongArtist)
            {
                _currentSongId = Guid.NewGuid().ToString();
                _lastSongTitle = status.Name;
                _lastSongArtist = status.Singer;
            }

            return new PlayerInfo
            {
                Identity = _currentSongId,
                Title = status.Name,
                Artists = status.Singer,
                Album = status.AlbumName,
                Cover = status.PicUrl,
                Schedule = status.Progress,
                Duration = status.Duration,
                Pause = isPaused.Value,
                Url = string.Empty
            };
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[Lx Music] API request failed: {e.Message}");
            return null;
        }
    }
}

file record LxMusicConfig
{
    [JsonPropertyName("setting")] public LxMusicSetting? Setting { get; init; }
}

file record LxMusicSetting
{
    [JsonPropertyName("openAPI.enable")] public bool Enable { get; init; }

    [JsonPropertyName("openAPI.port")] public string? Port { get; init; }
}

file record LxMusicStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("singer")] string Singer,
    [property: JsonPropertyName("albumName")]
    string AlbumName,
    [property: JsonPropertyName("duration")]
    double Duration,
    [property: JsonPropertyName("progress")]
    double Progress,
    [property: JsonPropertyName("picUrl")] string PicUrl
);