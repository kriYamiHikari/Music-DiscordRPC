using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MusicRpc.Models;
using MusicRpc.Utils;

namespace MusicRpc;

/// <summary>
/// 主窗口窗体，用于显示三个播放器的歌曲信息和状态
/// </summary>
internal class MainForm : Form
{
    private readonly Timer _updateTimer;

    // UI控件 - 三个播放器区域
    private readonly Panel[] _playerPanels = new Panel[3];
    private readonly PictureBox[] _coverPictureBoxes = new PictureBox[3];
    private readonly Label[] _songTitleLabels = new Label[3];
    private readonly Label[] _artistLabels = new Label[3];
    private readonly Label[] _albumLabels = new Label[3];
    private readonly Label[] _statusLabels = new Label[3];
    private readonly ProgressBar[] _progressBars = new ProgressBar[3];
    private readonly Label[] _progressLabels = new Label[3];
    private readonly Label[] _playerNameLabels = new Label[3];
    private Label _lastUpdateLabel = null!;
    private Button _settingsButton = null!;

    private readonly string[] _playerNames = ["网易云音乐", "QQ音乐", "洛雪音乐"];

    private readonly Color[] _playerColors =
    [
        Color.FromArgb(241, 98, 70),
        Color.FromArgb(217, 215, 23),
        Color.FromArgb(96, 213, 105)
    ];

    // 跟踪每个播放器的当前歌曲ID，用于检测歌曲切换
    private readonly string[] _currentSongIds = new string[3];
    private readonly string[] _currentCoverUrls = ["", "", ""];

    public MainForm()
    {
        InitializeComponent();
        SetupForm();
        _updateTimer = new Timer { Interval = 1000 }; // 每秒更新
        _updateTimer.Tick += UpdateTimer_Tick;
    }

    private void InitializeComponent()
    {
        // 创建三个播放器区域
        for (var i = 0; i < 3; i++)
        {
            CreatePlayerPanel(i);
        }

        // 创建最后更新时间标签
        _lastUpdateLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 8),
            ForeColor = Color.Gray,
            Location = new Point(10, 490),
            Size = new Size(150, 13),
            Text = "最后更新: --:--:--"
        };

        // 创建设置按钮
        _settingsButton = new Button
        {
            Text = "设置",
            Size = new Size(75, 30),
            Location = new Point(516, 485),
            BackColor = Color.White,
            ForeColor = Color.Black,
            Font = new Font("Microsoft YaHei", 9),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1 }
        };
        _settingsButton.Click += SettingsButton_Click;

        Controls.AddRange([_lastUpdateLabel, _settingsButton]);
    }

    private void CreatePlayerPanel(int index)
    {
        var yOffset = index * 160;
        var playerColor = _playerColors[index];

        // 创建播放器面板
        var panel = new Panel
        {
            Size = new Size(580, 155),
            Location = new Point(10, yOffset),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        // 创建专辑封面
        var coverPictureBox = new PictureBox
        {
            Size = new Size(80, 80),
            Location = new Point(10, 15),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.LightGray
        };

        // 创建播放器名称标签
        var playerNameLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            ForeColor = playerColor,
            Location = new Point(100, 15),
            Size = new Size(100, 15),
            Text = _playerNames[index]
        };

        // 创建一个FlowLayoutPanel来容纳可变高度的文本标签
        var textFlowPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Location = new Point(100, 35),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        // 创建歌曲标题标签
        var songTitleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(240, 0),
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            ForeColor = Color.Black,
            Text = "暂无播放",
            Padding = new Padding(0, 0, 0, 5),
            Margin = new Padding(0)
        };

        // 创建艺术家标签
        var artistLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(240, 0),
            Font = new Font("Microsoft YaHei", 9),
            ForeColor = Color.DarkGray,
            Text = "",
            Padding = new Padding(0, 0, 0, 5),
            Margin = new Padding(0)
        };

        // 创建专辑标签
        var albumLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(240, 0),
            Font = new Font("Microsoft YaHei", 9),
            ForeColor = Color.DarkGray,
            Text = "",
            Margin = new Padding(0)
        };

        // 将标签添加到FlowLayoutPanel
        textFlowPanel.Controls.AddRange(songTitleLabel, artistLabel, albumLabel);

        // 创建状态标签
        var statusLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
            ForeColor = Color.Gray,
            Location = new Point(350, 15),
            Size = new Size(80, 14),
            Text = "未运行"
        };

        // 创建进度条（位置下移）
        var progressBar = new ProgressBar
        {
            Location = new Point(350, 40),
            Size = new Size(200, 18),
            Style = ProgressBarStyle.Continuous,
            Value = 0
        };

        // 创建进度时间标签（位置下移）
        var progressLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 8),
            ForeColor = Color.DarkGray,
            Location = new Point(350, 65),
            Size = new Size(100, 12),
            Text = "00:00 / 00:00"
        };

        // 添加控件到面板
        panel.Controls.AddRange(coverPictureBox, playerNameLabel, textFlowPanel, statusLabel, progressBar,
            progressLabel);

        // 保存控件引用
        _playerPanels[index] = panel;
        _coverPictureBoxes[index] = coverPictureBox;
        _playerNameLabels[index] = playerNameLabel;
        _songTitleLabels[index] = songTitleLabel;
        _artistLabels[index] = artistLabel;
        _albumLabels[index] = albumLabel;
        _statusLabels[index] = statusLabel;
        _progressBars[index] = progressBar;
        _progressLabels[index] = progressLabel;

        // 添加面板到窗体
        Controls.Add(panel);
    }

    private void SetupForm()
    {
        Text = "Music Discord RPC - 播放器状态";
        Size = new Size(620, 570);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        BackColor = Color.WhiteSmoke;

        // 设置窗体图标
        Icon = AppResource.Icon;

        // 窗体关闭时根据配置决定行为
        FormClosing += MainForm_FormClosing;
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing) return;

        var config = Configurations.Instance;
        if (config.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            Program.ShowMinimizeToTrayNotification();
        }
        else
        {
            Application.Exit();
        }
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm();
        settingsForm.ShowDialog(this);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateDisplay();
    }

    /// <summary>
    /// 更新显示信息
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新</param>
    private void UpdateDisplay(bool forceRefresh = false)
    {
        try
        {
            var rpcManager = Program.GetRpcManager();
            if (rpcManager == null) return;

            // 获取所有播放器状态
            var allPlayersStatus = rpcManager.GetAllPlayersStatus();

            for (var i = 0; i < 3; i++)
            {
                var (playerInfo, playerName, isActive) = allPlayersStatus[i];
                UpdatePlayerDisplay(i, playerInfo, playerName, isActive);
            }

            _lastUpdateLabel.Text = $"最后更新: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _lastUpdateLabel.Text = $"更新失败: {ex.Message}";
        }
    }

    private void UpdatePlayerDisplay(int index, PlayerInfo? playerInfo, string playerName, bool isActive)
    {
        if (isActive && playerInfo != null)
        {
            // 播放器正在运行且有播放信息
            // 使用与RpcManager相同的字符串截断逻辑
            const string zeroWidthSpace = "\u200B";

            var title = string.IsNullOrEmpty(playerInfo.Value.Title)
                ? StringUtils.GetTruncatedStringByMaxByteLength("未知歌曲", 128)
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Title + zeroWidthSpace, 128);
            var artists = string.IsNullOrEmpty(playerInfo.Value.Artists)
                ? ""
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Artists + zeroWidthSpace, 128);
            var album = string.IsNullOrEmpty(playerInfo.Value.Album)
                ? ""
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Album + zeroWidthSpace, 128);

            // 检测歌曲是否切换
            var currentSongId = playerInfo.Value.Identity;
            var previousSongId = _currentSongIds[index];

            // 如果歌曲ID发生变化，更新跟踪
            if (!string.IsNullOrEmpty(currentSongId) && currentSongId != previousSongId)
            {
                // 更新跟踪的歌曲ID
                _currentSongIds[index] = currentSongId;
            }

            _songTitleLabels[index].Text = title;
            _artistLabels[index].Text = string.IsNullOrEmpty(artists) ? "" : $"🎤 {artists}";
            _albumLabels[index].Text = string.IsNullOrEmpty(album) ? "" : $"💿 {album}";

            // 设置状态
            var statusText = playerInfo.Value.Pause ? "⏸️ 已暂停" : "▶️ 正在播放";
            var statusColor = playerInfo.Value.Pause ? Color.Orange : Color.Green;
            _statusLabels[index].Text = statusText;
            _statusLabels[index].ForeColor = statusColor;

            // 更新进度
            if (playerInfo.Value.Duration > 0)
            {
                var progressPercentage = (int)((playerInfo.Value.Schedule / playerInfo.Value.Duration) * 100);
                _progressBars[index].Value = Math.Max(0, Math.Min(100, progressPercentage));

                var currentTime = TimeSpan.FromSeconds(playerInfo.Value.Schedule);
                var totalTime = TimeSpan.FromSeconds(playerInfo.Value.Duration);
                _progressLabels[index].Text = $@"{currentTime:mm\:ss} / {totalTime:mm\:ss}";
            }
            else
            {
                _progressBars[index].Value = 0;
                _progressLabels[index].Text = "00:00 / 00:00";
            }

            // 异步加载封面
            if (!string.IsNullOrEmpty(playerInfo.Value.Cover))
            {
                _currentCoverUrls[index] = playerInfo.Value.Cover;
                var uniqueCacheKey = string.IsNullOrEmpty(currentSongId)
                    ? playerInfo.Value.Cover
                    : $"{playerInfo.Value.Cover}_{currentSongId}";

                Debug.WriteLine(
                    $"[DIAGNOSE] Updating cover for '{playerInfo.Value.Title}' (ID: {currentSongId}).");
                Debug.WriteLine($"[DIAGNOSE]   - Cover URL: {playerInfo.Value.Cover}");
                Debug.WriteLine($"[DIAGNOSE]   - Cache Key: {uniqueCacheKey}");

                LoadCoverAsyncWithUniqueKey(index, playerInfo.Value.Cover, uniqueCacheKey);
            }
            else
            {
                var oldImage = _coverPictureBoxes[index].Image;
                _coverPictureBoxes[index].Image = null;
                _coverPictureBoxes[index].BackColor = Color.LightGray;
                if (oldImage != null)
                {
                }
            }

            _playerPanels[index].BackColor = Color.White;
            _playerNameLabels[index].ForeColor = _playerColors[index];
        }
        else
        {
            // 播放器未运行或无播放信息，显示占位符
            _songTitleLabels[index].Text = StringUtils.GetTruncatedStringByMaxByteLength("播放器未运行", 128);
            _artistLabels[index].Text = "";
            _albumLabels[index].Text = "";
            _statusLabels[index].Text = "未运行";
            _statusLabels[index].ForeColor = Color.Gray;
            _progressBars[index].Value = 0;
            _progressLabels[index].Text = "00:00 / 00:00";

            var pictureBox = _coverPictureBoxes[index];
            pictureBox.Image = null;
            pictureBox.BackColor = Color.LightGray;

            // 设置面板状态
            _playerPanels[index].BackColor = Color.FromArgb(248, 248, 248);
            _playerNameLabels[index].ForeColor = Color.Gray;

            // 清除跟踪的歌曲ID
            _currentSongIds[index] = string.Empty;
            _currentCoverUrls[index] = string.Empty;
        }
    }

    /// <summary>
    /// 强制加载图片，使用时间戳确保缓存键唯一
    /// </summary>
    private async void LoadCoverAsyncWithUniqueKey(int index, string coverUrl, string forceCacheKey)
    {
        try
        {
            var image = await ImageCacheManager.LoadImageAsync(forceCacheKey, coverUrl);

            if (IsHandleCreated && image != null)
            {
                await InvokeAsync(() =>
                {
                    // 仅当此任务加载的封面URL仍是当前期望的URL时，才更新UI
                    if (coverUrl == _currentCoverUrls[index])
                    {
                        var pictureBox = _coverPictureBoxes[index];
                        try
                        {
                            _ = image.RawFormat;
                            pictureBox.Image = image;
                            pictureBox.BackColor = Color.White;
                        }
                        catch (Exception ex)
                        {
                            pictureBox.Image = null;
                            pictureBox.BackColor = Color.LightGray;
                            Debug.WriteLine($"[ERROR] Invalid image detected: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[DIAGNOSE] Stale cover update ignored for URL: {coverUrl}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to load cover image with timestamp: {ex.Message}");

            if (IsHandleCreated)
            {
                await InvokeAsync(() =>
                {
                    var pictureBox = _coverPictureBoxes[index];
                    pictureBox.Image = null;
                    pictureBox.BackColor = Color.LightGray;
                });
            }
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _updateTimer.Start();
        UpdateDisplay(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}