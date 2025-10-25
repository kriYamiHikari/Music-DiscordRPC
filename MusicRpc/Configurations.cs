using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicRpc;

/// <summary>
/// 存储需要被序列化到JSON文件的配置项。
/// </summary>
internal class ConfigData
{
    // 以下所有属性都将被保存到 config.json 文件中。
    // 默认值 (= true) 仅在首次创建配置文件时生效。

    /// <summary>
    /// 是否在歌曲标题前显示播放/暂停图标。
    /// </summary>

    public bool ShowTitleIcon { get; set; } = true;

    /// <summary>
    /// 是否在歌手名前显示图标。
    /// </summary>
    public bool ShowArtistIcon { get; set; } = true;

    /// <summary>
    /// 是否在专辑名前显示图标。
    /// </summary>
    public bool ShowAlbumIcon { get; set; } = true;

    /// <summary>
    /// 在Discord状态中，是优先显示歌曲标题 (true)，还是显示应用名称 (false)。
    /// </summary>
    public bool UseDetailsForStatus { get; set; } = true;
}

/// <summary>
/// 应用程序的配置管理器，采用单例模式。
/// 负责配置的加载、保存以及在整个应用程序中的全局访问。
/// </summary>
internal class Configurations
{
    // 单例
    public static readonly Configurations Instance = new();
    // 缓存 JsonSerializerOptions 实例以提高性能，避免在每次保存时都重新创建。
    private static readonly JsonSerializerOptions SJsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// 公开的配置属性，持有所有可配置项的数据实例。
    /// </summary>
    public ConfigData Settings { get; private set; }

    [JsonIgnore] public bool IsFirstLoad { get; }
    [JsonIgnore] private readonly string _path;

    private Configurations()
    {
        Settings = new ConfigData();

        // 在用户的本地应用数据文件夹下创建一个专用的配置目录
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicDiscordRPC");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "config.json");

        if (File.Exists(_path))
        {
            IsFirstLoad = false;
            Load();
        }
        else
        {
            IsFirstLoad = true;
            Save();
        }
    }

    /// <summary>
    /// 将当前的配置保存到硬盘上的 JSON 文件中。
    /// </summary>
    public void Save()
    {
        try
        {
            var jsonString = JsonSerializer.Serialize(Settings, SJsonOptions);
            File.WriteAllText(_path, jsonString, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] Failed to save configurations: {e.Message}");
        }
    }

    /// <summary>
    /// 从硬盘上的 JSON 文件加载配置。
    /// </summary>
    private void Load()
    {
        try
        {
            var jsonString = File.ReadAllText(_path, Encoding.UTF8);
            var loadedConfig = JsonSerializer.Deserialize<ConfigData>(jsonString);

            if (loadedConfig == null) return;
            Settings = loadedConfig;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ERROR] Failed to load configuration, resetting to defaults: {e.Message}");
            Save();
        }
    }
}