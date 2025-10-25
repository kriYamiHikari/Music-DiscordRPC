using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MusicRpc.Models;
using MusicRpc.Players.Interfaces;
using MusicRpc.Utils;
using MusicRpc.Win32Api;

namespace MusicRpc.Players;

/// <summary>
/// 网易云音乐播放器的具体实现
/// 主要通过两种方式获取信息：
/// 1. 读取进程内存，获取播放进度、状态和当前歌曲ID
/// 2. 读取本地缓存文件 (`playingList`, `fmPlay`)，根据歌曲ID获取歌曲的详细元数据（标题、歌手等）
/// </summary>
internal sealed class NetEase : IMusicPlayer
{
    // 用于内存操作的对象
    private readonly ProcessMemory _process;

    // 指向音频播放器核心对象的指针
    private readonly nint _audioPlayerPointer;

    // 指向播放进度值的指针
    private readonly nint _schedulePointer;

    // 网易云音乐本地缓存文件的路径
    private readonly string _playlistPath; // 歌单播放列表
    private readonly string _fmPlayPath; // FM电台播放列表

    // 播放列表的缓存机制，避免在每次轮询时都重复读取和反序列化文件
    private NetEasePlaylist? _cachedPlaylist; // 缓存的反序列化后的播放列表对象
    private DateTime _lastPlaylistWriteTime; // 上次读取时，文件的最后修改时间
    private string? _cachedPlaylistHash; // 上次读取时，文件内容的“标准化”哈希值

    // FM电台列表的缓存机制
    private NetEaseFmPlaylist? _cachedFmPlaylist;
    private DateTime _lastFmPlayWriteTime;
    private string? _cachedFmPlayHash;

    // 用于在 `cloudmusic.dll` 中定位音频播放器对象地址的内存特征码
    // 如果网易云音乐版本更新，这个特征码可能需要同步更新
    private const string AudioPlayerPattern
        = "48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 90 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 05 ? ? ? ? 48 8D A5 ? ? ? ? 5F 5D C3 CC CC CC CC CC 48 89 4C 24 ? 55 57 48 81 EC ? ? ? ? 48 8D 6C 24 ? 48 8D 7C 24";

    // 用于定位播放进度值的内存特征码
    private const string AudioSchedulePattern = "66 0F 2E 0D ? ? ? ? 7A ? 75 ? 66 0F 2E 15";

    public NetEase(int pid)
    {
        // 确定网易云音乐的本地缓存文件路径
        var fileDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetEase", "CloudMusic", "WebData", "file");
        _playlistPath = Path.Combine(fileDirectory, "playingList");
        _fmPlayPath = Path.Combine(fileDirectory, "fmPlay");

        // 初始化缓存时间戳
        _lastPlaylistWriteTime = DateTime.MinValue;
        _lastFmPlayWriteTime = DateTime.MinValue;

        // 获取基值计算所有指针的地址
        var moduleBaseAddress = ProcessUtils.GetModuleBaseAddress(pid, "cloudmusic.dll");
        if (moduleBaseAddress == IntPtr.Zero)
        {
            throw new DllNotFoundException("Could not find cloudmusic.dll in the target process.");
        }

        _process = new ProcessMemory(pid);

        if (Memory.FindPattern(AudioPlayerPattern, pid, moduleBaseAddress, out var app))
        {
            var textAddress = nint.Add(app, 3);
            var displacement = _process.ReadInt32(textAddress);
            _audioPlayerPointer = textAddress + displacement + sizeof(int);
        }

        if (Memory.FindPattern(AudioSchedulePattern, pid, moduleBaseAddress, out var asp))
        {
            var textAddress = nint.Add(asp, 4);
            var displacement = _process.ReadInt32(textAddress);
            _schedulePointer = textAddress + displacement + sizeof(int);
        }

        if (_audioPlayerPointer == nint.Zero)
        {
            throw new EntryPointNotFoundException("Failed to find AudioPlayer pointer.");
        }

        if (_schedulePointer == nint.Zero)
        {
            throw new EntryPointNotFoundException("Failed to find Scheduler pointer.");
        }
    }

    public PlayerInfo? GetPlayerInfo()
    {
        var status = GetPlayerStatus();
        if (status == PlayStatus.Waiting)
        {
            return null;
        }

        var identity = GetCurrentSongId();
        if (string.IsNullOrEmpty(identity))
        {
            return null;
        }

        // 优先在 playlist (歌单列表) 中查找
        // 如果找不到去 fmPlay (电台) 中查找
        // 两个地方都找不到就返回 null
        var playerInfo = UpdateAndSearchPlaylist(identity, status);
        if (playerInfo != null)
        {
            return playerInfo;
        }

        playerInfo = UpdateAndSearchFmPlaylist(identity, status);
        return playerInfo ?? null;
    }

    /// <summary>
    /// 更新并搜索常规播放列表缓存
    /// </summary>
    private PlayerInfo? UpdateAndSearchPlaylist(string identity, PlayStatus status)
    {
        try
        {
            if (!File.Exists(_playlistPath))
            {
                _cachedPlaylist = null;
                return null;
            }

            var currentWriteTime = File.GetLastWriteTimeUtc(_playlistPath);
            if (currentWriteTime != _lastPlaylistWriteTime || _cachedPlaylist is null)
            {
                var fileBytes = File.ReadAllBytes(_playlistPath);
                if (TryGetNormalizedPlaylistContent(fileBytes, out var normalizedJson, out var newHash))
                {
                    if (newHash != _cachedPlaylistHash || _cachedPlaylist is null)
                    {
                        Debug.WriteLine("[NetEase] Playlist content changed. Deserializing new playlist.");
                        _cachedPlaylist = JsonSerializer.Deserialize<NetEasePlaylist>(normalizedJson);
                        _cachedPlaylistHash = newHash;
                    }
                }
                else
                {
                    _cachedPlaylist = null;
                }

                _lastPlaylistWriteTime = currentWriteTime;
            }

            var currentTrackItem = _cachedPlaylist?.List.Find(x => x.Identity == identity);
            if (currentTrackItem is not { Track: { } track })
            {
                return null;
            }

            return new PlayerInfo
            {
                Identity = identity,
                Title = track.Name,
                Artists = string.Join(',', track.Artists.Select(x => x.Singer)),
                Album = track.Album.Name,
                Cover = track.Album.Cover,
                Duration = GetSongDuration(),
                Schedule = GetSchedule(),
                Pause = status == PlayStatus.Paused,
                Url = $"https://music.163.com/#/song?id={identity}",
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to process NetEase playlist: {ex.Message}");
            _cachedPlaylist = null;
            return null;
        }
    }

    /// <summary>
    /// 更新并搜索FM电台播放列表缓存
    /// </summary>
    private PlayerInfo? UpdateAndSearchFmPlaylist(string identity, PlayStatus status)
    {
        try
        {
            if (!File.Exists(_fmPlayPath))
            {
                _cachedFmPlaylist = null;
                return null;
            }

            var currentWriteTime = File.GetLastWriteTimeUtc(_fmPlayPath);
            if (currentWriteTime != _lastFmPlayWriteTime || _cachedFmPlaylist is null)
            {
                var fileBytes = File.ReadAllBytes(_fmPlayPath);
                if (TryGetNormalizedFmContent(fileBytes, out var normalizedJson, out var newHash))
                {
                    if (newHash != _cachedFmPlayHash || _cachedFmPlaylist is null)
                    {
                        Debug.WriteLine("[NetEase] FM Playlist content changed. Deserializing new FM playlist.");
                        _cachedFmPlaylist = JsonSerializer.Deserialize<NetEaseFmPlaylist>(normalizedJson);
                        _cachedFmPlayHash = newHash;
                    }
                }
                else
                {
                    _cachedFmPlaylist = null;
                }

                _lastFmPlayWriteTime = currentWriteTime;
            }

            var currentTrackItem = _cachedFmPlaylist?.Queue.Find(x => x.Identity == identity);
            if (currentTrackItem is null)
            {
                return null;
            }

            return new PlayerInfo
            {
                Identity = identity,
                Title = currentTrackItem.Name,
                Artists = string.Join(',', currentTrackItem.Artists.Select(x => x.Singer)),
                Album = currentTrackItem.Album.Name,
                Cover = currentTrackItem.Album.Cover,
                Duration = GetSongDuration(),
                Schedule = GetSchedule(),
                Pause = status == PlayStatus.Paused,
                Url = $"https://music.163.com/#/song?id={identity}",
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to process NetEase FM playlist: {ex.Message}");
            _cachedFmPlaylist = null;
            return null;
        }
    }

    /// <summary>
    /// 对播放列表JSON内容进行标准化处理。
    /// 目的是移除那些频繁变化但与歌曲信息无关的字段（如权限、推荐信息等），
    /// 以便生成一个稳定的哈希值，避免不必要的JSON反序列化操作。
    /// </summary>
    private static bool TryGetNormalizedPlaylistContent(byte[] fileBytes, out string normalizedJson, out string newHash)
    {
        normalizedJson = string.Empty;
        newHash = string.Empty;
        try
        {
            var rootNode = JsonNode.Parse(fileBytes);
            if (rootNode is not JsonObject rootObj || !rootObj.ContainsKey("list") || rootObj["list"] is not JsonArray)
            {
                return false;
            }

            var listArray = rootNode["list"]!.AsArray();
            var clonedArray = JsonNode.Parse(listArray.ToJsonString())!.AsArray();

            foreach (var item in clonedArray)
            {
                if (item is not JsonObject songObject) continue;
                songObject.Remove("randomOrder");
                songObject.Remove("privilege");
                songObject.Remove("referInfo");
                songObject.Remove("fromInfo");

                if (songObject.TryGetPropertyValue("track", out var trackNode) && trackNode is JsonObject trackObj)
                {
                    trackObj.Remove("privilege");
                }
            }

            var newRoot = new JsonObject { ["list"] = clonedArray };
            normalizedJson = newRoot.ToJsonString();
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedJson));
            newHash = Convert.ToBase64String(hashBytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 对FM电台列表JSON内容进行标准化处理，逻辑同上
    /// </summary>
    private static bool TryGetNormalizedFmContent(byte[] fileBytes, out string normalizedJson, out string newHash)
    {
        normalizedJson = string.Empty;
        newHash = string.Empty;
        try
        {
            var rootNode = JsonNode.Parse(fileBytes);
            if (rootNode is not JsonObject rootObj || !rootObj.ContainsKey("queue") ||
                rootObj["queue"] is not JsonArray)
            {
                return false;
            }

            var listArray = rootNode["queue"]!.AsArray();
            var clonedArray = JsonNode.Parse(listArray.ToJsonString())!.AsArray();

            foreach (var item in clonedArray)
            {
                if (item is not JsonObject songObject) continue;
                songObject.Remove("privilege");
                songObject.Remove("alg");
                songObject.Remove("score");
            }

            var newRoot = new JsonObject { ["queue"] = clonedArray };
            normalizedJson = newRoot.ToJsonString();
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedJson));
            newHash = Convert.ToBase64String(hashBytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    #region Unsafe

    private enum PlayStatus
    {
        Waiting,
        Playing,
        Paused,
        Unknown3,
        Unknown4,
    }

    /// <summary>
    /// 从内存直接读取当前播放进度
    /// </summary>
    private double GetSchedule()
        => _process.ReadDouble(_schedulePointer);

    /// <summary>
    /// 从内存直接读取播放器状态
    /// </summary>
    private PlayStatus GetPlayerStatus()
        => (PlayStatus)_process.ReadInt32(_audioPlayerPointer, 0x60);

    /// <summary>
    /// 从内存直接读取播放器音量
    /// </summary>
    private float GetPlayerVolume()
        => _process.ReadFloat(_audioPlayerPointer, 0x64);

    /// <summary>
    /// 从内存直接读取当前音量
    /// </summary>
    private float GetCurrentVolume()
        => _process.ReadFloat(_audioPlayerPointer, 0x68);

    /// <summary>
    /// 从内存直接读取歌曲总时长
    /// </summary>
    private double GetSongDuration()
        => _process.ReadDouble(_audioPlayerPointer, 0xa8);

    /// <summary>
    /// 从内存直接读取当前歌曲的ID
    /// </summary>
    private string GetCurrentSongId()
    {
        var audioPlayInfo = _process.ReadInt64(_audioPlayerPointer, 0x50);
        if (audioPlayInfo == 0)
        {
            return string.Empty;
        }

        var strPtr = audioPlayInfo + 0x10;
        var strLength = _process.ReadInt64((nint)strPtr, 0x10);

        byte[] strBuffer;
        // C++ std::string 的“小字符串优化”(SSO): 
        // 如果字符串长度很短（通常小于16字节），字符串内容会直接存储在 std::string 对象内部，而不是在堆上分配内存
        if (strLength <= 15)
        {
            strBuffer = _process.ReadBytes((nint)strPtr, (int)strLength);
        }
        else
        {
            var strAddress = _process.ReadInt64((nint)strPtr);
            strBuffer = _process.ReadBytes((nint)strAddress, (int)strLength);
        }

        var str = Encoding.UTF8.GetString(strBuffer);
        return string.IsNullOrEmpty(str) ? string.Empty : str[..str.IndexOf('_')];
    }

    #endregion
}

internal record NetEasePlaylistTrackArtist([property: JsonPropertyName("name")] string Singer);

internal record NetEasePlaylistTrackAlbum(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cover")] string Cover);

internal record NetEasePlaylistTrack(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")]
    NetEasePlaylistTrackArtist[] Artists,
    [property: JsonPropertyName("album")] NetEasePlaylistTrackAlbum Album);

internal record NetEasePlaylistItem(
    [property: JsonPropertyName("id")] string Identity,
    [property: JsonPropertyName("track")] NetEasePlaylistTrack Track);

internal record NetEasePlaylist([property: JsonPropertyName("list")] List<NetEasePlaylistItem> List);

internal record NetEaseFmPlaylist(
    [property: JsonPropertyName("queue")] List<NetEaseFmPlaylistItem> Queue
);

internal record NetEaseFmPlaylistItem(
    [property: JsonPropertyName("id")] string Identity,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")]
    NetEasePlaylistTrackArtist[] Artists,
    [property: JsonPropertyName("album")] NetEasePlaylistTrackAlbum Album
);