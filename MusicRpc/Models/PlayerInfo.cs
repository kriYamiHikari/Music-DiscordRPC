namespace MusicRpc.Models;

/// <summary>
/// 标准化的播放器信息结构
/// </summary>
internal readonly record struct PlayerInfo
{
    /// <summary>
    /// 歌曲的唯一标识符
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>
    /// 歌曲标题
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 歌手名称，多个歌手使用逗号分隔
    /// </summary>
    public required string Artists { get; init; }

    /// <summary>
    /// 歌曲所属专辑名称
    /// </summary>
    public required string Album { get; init; }

    /// <summary>
    /// 专辑封面图片URL
    /// </summary>
    public required string Cover { get; init; }

    /// <summary>
    /// 当前播放进度（单位：秒）
    /// </summary>
    public required double Schedule { get; init; }

    /// <summary>
    /// 歌曲总时长（单位：秒）
    /// </summary>
    public required double Duration { get; init; }

    /// <summary>
    /// 指向歌曲页面的URL，用于Discord的“一起听”按钮
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// 指示播放器当前是否处于暂停状态
    /// </summary>
    public required bool Pause { get; init; }
}