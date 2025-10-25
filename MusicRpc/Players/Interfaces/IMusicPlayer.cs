using MusicRpc.Models;

namespace MusicRpc.Players.Interfaces;

/// <summary>
/// 音乐播放器接口
/// </summary>
internal interface IMusicPlayer
{
    /// <summary>
    /// 获取当前播放器的状态信息
    /// </summary>
    /// <returns>一个标准化的 PlayerInfo 对象；如果播放器未在播放或无法获取信息，则返回 null。</returns>
    PlayerInfo? GetPlayerInfo();
}