using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DiscordRPC;
using MusicRpc.Models;
using MusicRpc.Players;
using MusicRpc.Players.Interfaces;
using MusicRpc.Utils;

namespace MusicRpc;

/// <summary>
/// RPC管理器，封装了所有与Discord RPC更新相关的核心逻辑。
/// 它负责在一个无限循环中轮询各个音乐播放器的状态，检测变化，并推送更新到Discord。
/// </summary>
/// <param name="netEaseClient">用于网易云音乐的Discord RPC客户端实例。</param>
/// <param name="tencentClient">用于QQ音乐的Discord RPC客户端实例。</param>
internal class RpcManager(
    DiscordRpcClient netEaseClient,
    DiscordRpcClient tencentClient,
    DiscordRpcClient lxMusicClient)
{
    /// <summary>
    /// 维护单个播放器的运行时状态。
    /// </summary>
    private class PlayerState
    {
        /// <summary>
        /// 当前活跃的播放器实例（如果检测到进程）。
        /// </summary>
        public IMusicPlayer? Player { get; set; }

        // 状态检测
        public PlayerInfo? LastPolledInfo { get; set; }
        public DateTime LastPollTime { get; set; } = DateTime.MinValue;

        // 防抖机制
        public PlayerInfo? PendingUpdateInfo { get; set; }
        public DateTime LastChangeDetectedTime { get; set; } = DateTime.MinValue;
    }

    private readonly PlayerState _netEaseState = new();
    private readonly PlayerState _tencentState = new();
    private readonly PlayerState _lxMusicState = new();

    // 接收来自UI的刷新请求，标志位
    private volatile bool _stateRefreshRequested;

    // 如果实际进度变化与时间流逝的差异超过0.4秒，则认为跳转了歌曲进度
    private const double JumpToleranceSeconds = 0.4;

    // 防抖处理，只有在状态稳定超过1.5秒后，才发送RPC更新
    private const double DebounceWindowSeconds = 1.5;

    /// <summary>
    /// 允许外部（如UI线程）请求立即刷新所有活跃播放器的 RPC 状态。
    /// 通常在用户更改了显示设置后调用。
    /// </summary>
    public void RequestStateRefresh() => _stateRefreshRequested = true;

    /// <summary>
    /// 启动无限循环的更新线程
    /// </summary>
    public async Task Start()
    {
        while (true)
        {
            var currentTime = DateTime.UtcNow;
            try
            {
                // 对于每个播放器, 先检测进程/窗口是否存在。
                // 存在则开始轮询状态，否则清理状态。

                // Netease
                var neteaseHwnd = Win32Api.User32.FindWindow("OrpheusBrowserHost", null);
                if (neteaseHwnd != IntPtr.Zero &&
                    Win32Api.User32.GetWindowThreadProcessId(neteaseHwnd, out var neteasePid) != 0)
                {
                    PollAndUpdatePlayer(_netEaseState, "NetEase CloudMusic", netEaseClient, neteasePid,
                        pid => new NetEase(pid), currentTime);
                }
                else
                {
                    CleanupPlayerState(_netEaseState, "NetEase CloudMusic", netEaseClient);
                }

                // Tencent
                var tencentHwnd = Win32Api.User32.FindWindow("QQMusic_Daemon_Wnd", null);
                if (tencentHwnd != IntPtr.Zero &&
                    Win32Api.User32.GetWindowThreadProcessId(tencentHwnd, out var tencentPid) != 0)
                {
                    PollAndUpdatePlayer(_tencentState, "Tencent QQMusic", tencentClient, tencentPid,
                        pid => new Tencent(pid), currentTime);
                }
                else
                {
                    CleanupPlayerState(_tencentState, "Tencent QQMusic", tencentClient);
                }

                // LX Music
                var lxProcess = Process.GetProcessesByName("lx-music-desktop").FirstOrDefault();
                if (lxProcess != null)
                {
                    PollAndUpdatePlayer(_lxMusicState, "LX Music", lxMusicClient, lxProcess.Id, pid => new LxMusic(pid),
                        currentTime);
                }
                else
                {
                    CleanupPlayerState(_lxMusicState, "LX Music", lxMusicClient);
                }

                // 处理状态刷新请求
                if (_stateRefreshRequested)
                {
                    _stateRefreshRequested = false;
                    Debug.WriteLine("Settings changed. Forcing immediate RPC update for active players.");
                    ForceRefreshPlayer(_netEaseState, netEaseClient, "NetEase CloudMusic");
                    ForceRefreshPlayer(_tencentState, tencentClient, "Tencent QQMusic");
                    ForceRefreshPlayer(_lxMusicState, lxMusicClient, "LX Music");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FATAL ERROR] An exception occurred in the main poll loop: {ex.Message}");
                ClearAllPlayers();
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMilliseconds(233));
            }
        }
    }

    /// <summary>
    /// 播放器轮询方法
    /// </summary>
    private static void PollAndUpdatePlayer(PlayerState state, string playerName, DiscordRpcClient rpcClient, int pid,
        Func<int, IMusicPlayer> playerFactory, DateTime currentTime)
    {
        if (state.Player is null)
        {
            Debug.WriteLine($"[{playerName}] Player process detected. Creating instance.");
            state.Player = playerFactory(pid);
        }

        var currentInfo = state.Player.GetPlayerInfo();

        var isStateChanged = DetectStateChange(currentInfo, state.LastPolledInfo, currentTime, state.LastPollTime,
            JumpToleranceSeconds);
        if (isStateChanged)
        {
            Debug.WriteLine(
                $"[{playerName}] State change detected. Resetting debounce timer for: {currentInfo?.Title ?? "None (Clear)"}");
            state.PendingUpdateInfo = currentInfo;
            state.LastChangeDetectedTime = currentTime;
        }

        if (state.PendingUpdateInfo is not null &&
            (currentTime - state.LastChangeDetectedTime).TotalSeconds > DebounceWindowSeconds)
        {
            Debug.WriteLine($"[{playerName}] Debounce window passed. Sending RPC update.");
            UpdateOrClearPresence(rpcClient, state.PendingUpdateInfo, playerName);
            state.PendingUpdateInfo = null;
        }

        state.LastPolledInfo = currentInfo;
        state.LastPollTime = currentTime;
    }

    /// <summary>
    /// 清理指定音乐播放器的运行时状态，移除当前关联的播放器实例并清空Discord RPC状态。
    /// </summary>
    /// <param name="state">要清理的音乐播放器状态对象。</param>
    /// <param name="playerName">播放器的名称，用于日志记录。</param>
    /// <param name="rpcClient">关联的Discord RPC客户端实例，用于清除RPC状态。</param>
    private static void CleanupPlayerState(PlayerState state, string playerName, DiscordRpcClient rpcClient)
    {
        if (state.Player is null) return;
        Debug.WriteLine($"[{playerName}] Player process lost. Clearing instance and RPC.");
        rpcClient.ClearPresence();
        state.Player = null;
        state.LastPolledInfo = null;
        state.PendingUpdateInfo = null;
    }

    /// <summary>
    /// 强制刷新指定播放器的RPC状态。
    /// 如果播放器存在，这将使用最近轮询的信息更新或清除其Discord Rich Presence状态。
    /// </summary>
    /// <param name="state">表示播放器的当前运行状态。</param>
    /// <param name="rpcClient">用于Discord RPC通信的客户端实例。</param>
    /// <param name="playerName">播放器的名称，用于区分不同的音乐播放器。</param>
    private static void ForceRefreshPlayer(PlayerState state, DiscordRpcClient rpcClient, string playerName)
    {
        if (state.Player is not null)
        {
            UpdateOrClearPresence(rpcClient, state.LastPolledInfo, playerName);
        }
    }

    /// <summary>
    /// 发生严重错误时，清理所有播放器状态
    /// </summary>
    private void ClearAllPlayers()
    {
        CleanupPlayerState(_netEaseState, "NetEase CloudMusic", netEaseClient);
        CleanupPlayerState(_tencentState, "Tencent QQMusic", tencentClient);
        CleanupPlayerState(_lxMusicState, "LX Music", lxMusicClient);
    }

    /// <summary>
    /// 比较当前和上一次的播放信息，以确定是否有“有意义的”状态变化
    /// </summary>
    private static bool DetectStateChange(PlayerInfo? current, PlayerInfo? last, DateTime currentTime,
        DateTime lastTime, double tolerance)
    {
        if ((current is null && last is not null) || (current is not null && last is null)) return true;
        if (current is not { } c || last is not { } l) return false;
        if (c.Identity != l.Identity || c.Pause != l.Pause) return true;
        if (c.Pause) return false;

        var elapsed = (currentTime - lastTime).TotalSeconds;
        var progressDelta = c.Schedule - l.Schedule;

        return Math.Abs(progressDelta - elapsed) > tolerance;
    }

    /// <summary>
    /// 根据播放信息更新或清除Rich Presence
    /// </summary>
    private static void UpdateOrClearPresence(DiscordRpcClient rpcClient, PlayerInfo? info, string playerName)
    {
        if (info is not { } playerInfo)
        {
            rpcClient.ClearPresence();
            return;
        }

        var config = Configurations.Instance;

        var titleIcon = config.Settings.ShowTitleIcon ? (playerInfo.Pause ? "⏸️ " : "▶️ ") : "";
        var artistIcon = config.Settings.ShowArtistIcon ? "🎤 " : "";
        var albumIcon = config.Settings.ShowAlbumIcon ? "💿 " : "";

        var buttons = new List<Button>();
        // Url 地址不为空的情况下才添加一起听按钮
        if (!string.IsNullOrEmpty(playerInfo.Url))
        {
            buttons.Add(new Button { Label = "🎧 Listen", Url = playerInfo.Url });
        }

        buttons.Add(new Button
        {
            Label = "🔍 View App on GitHub",
            Url = "https://github.com/kriYamiHikari/Music-DiscordRPC"
        });

        // 插入一个不可见零宽空格，以绕过Discord内容过滤器（是否存在存疑但有效）
        // 例如播放`Ado - 唱`这首歌时，不做这样的处理会导致无法同步信息到Discord，无论是QQ音乐还是落雪音乐
        const string zeroWidthSpace = "\u200B";
        var sanitizedTitle = playerInfo.Title + zeroWidthSpace;
        var sanitizedArtists = playerInfo.Artists + zeroWidthSpace;
        var sanitizedAlbum = playerInfo.Album + zeroWidthSpace;

        Debug.WriteLine($"cover url length:{playerInfo.Cover.Length}");
        Debug.WriteLine(
            $"pause: {playerInfo.Pause}, progress: {playerInfo.Schedule}, duration: {playerInfo.Duration}");
        Debug.WriteLine(
            $"id: {playerInfo.Identity}, name: {playerInfo.Title}, singer: {playerInfo.Artists}, album: {playerInfo.Album}, cover: {playerInfo.Cover},");

        var presence = new RichPresence
        {
            Details = StringUtils.GetTruncatedStringByMaxByteLength($"{titleIcon}{sanitizedTitle}", 128),
            State = StringUtils.GetTruncatedStringByMaxByteLength($"{artistIcon}{sanitizedArtists}", 128),
            StatusDisplay = config.Settings.UseDetailsForStatus ? StatusDisplayType.Details : StatusDisplayType.Name,
            Type = ActivityType.Listening,
            Assets = new Assets
            {
                LargeImageKey = playerInfo.Cover,
                LargeImageText = StringUtils.GetTruncatedStringByMaxByteLength($"{albumIcon}{sanitizedAlbum}", 128),
                SmallImageKey = "timg",
                SmallImageText = playerName,
            },
            Buttons = buttons.ToArray()
        };

        // 根据播放状态决定是否设置时间戳
        // 暂停时切换为暂停状态图标，但由于限制时间进度依旧会自动增长
        if (!playerInfo.Pause)
        {
            presence.Timestamps = new Timestamps(
                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(playerInfo.Schedule)),
                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(playerInfo.Schedule))
                    .Add(TimeSpan.FromSeconds(playerInfo.Duration))
            );
        }

        rpcClient.SetPresence(presence);
    }
}