using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DiscordRPC;
using MusicRpc.Utils;
using Button = System.Windows.Forms.Button;

namespace MusicRpc;

/// <summary>
/// 设置窗口，用于配置程序和RPC显示选项
/// </summary>
internal class SettingsForm : Form
{
    private readonly ConfigData _originalSettings;

    // 程序设置控件
    private CheckBox? _autoStartCheckBox;
    private CheckBox? _closeToTrayCheckBox;
    private CheckBox? _startInTrayCheckBox;

    // RPC图标显示控件
    private CheckBox? _showTitleIconCheckBox;
    private CheckBox? _showArtistIconCheckBox;
    private CheckBox? _showAlbumIconCheckBox;

    // 状态显示控件
    private ComboBox? _statusDisplayComboBox;
    private CheckBox? _showArtistNameCheckBox;
    private CheckBox? _showAlbumInfoCheckBox;
    private Label? _albumTooltipLabel;

    // 按钮控件
    private Button? _okButton;
    private Button? _cancelButton;
    private Button? _applyButton;

    public SettingsForm()
    {
        _originalSettings = Configurations.Instance.Settings;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "设置 - Music Discord RPC";
        Size = new Size(450, 720);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;

        // 创建滚动面板
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        var yOffset = 10;

        // 程序设置组
        var programGroupBox = CreateProgramSettingsGroup(ref yOffset);
        mainPanel.Controls.Add(programGroupBox);

        // RPC图标显示组
        var rpcIconGroupBox = CreateRpcIconSettingsGroup(ref yOffset);
        mainPanel.Controls.Add(rpcIconGroupBox);

        // 用户状态优先显示内容组
        var statusDisplayGroupBox = CreateStatusDisplayGroup(ref yOffset);
        mainPanel.Controls.Add(statusDisplayGroupBox);

        // 部分状态显示组
        var partialStatusGroupBox = CreatePartialStatusGroup(ref yOffset);
        mainPanel.Controls.Add(partialStatusGroupBox);

        // 性能监控组
        var performanceGroupBox = CreatePerformanceGroup(ref yOffset);
        mainPanel.Controls.Add(performanceGroupBox);

        // 按钮区域
        mainPanel.Controls.Add(CreateButtonPanelControl());

        Controls.Add(mainPanel);
    }

    private GroupBox CreateProgramSettingsGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "程序设置",
            Location = new Point(10, yOffset),
            Size = new Size(410, 105),
            BackColor = Color.White
        };

        yOffset += 120;

        _autoStartCheckBox = new CheckBox
        {
            Text = "开机自启",
            Location = new Point(15, 25),
            AutoSize = true,
            BackColor = Color.White
        };

        _closeToTrayCheckBox = new CheckBox
        {
            Text = "关闭窗口时隐藏到托盘",
            Location = new Point(15, 50),
            AutoSize = true,
            BackColor = Color.White
        };

        _startInTrayCheckBox = new CheckBox
        {
            Text = "启动时隐藏到托盘",
            Location = new Point(15, 75),
            AutoSize = true,
            BackColor = Color.White
        };

        groupBox.Controls.AddRange([_autoStartCheckBox, _closeToTrayCheckBox, _startInTrayCheckBox]);
        return groupBox;
    }

    private GroupBox CreateRpcIconSettingsGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "RPC歌曲信息图标显示",
            Location = new Point(10, yOffset),
            Size = new Size(410, 100),
            BackColor = Color.White
        };

        yOffset += 115;

        _showTitleIconCheckBox = new CheckBox
        {
            Text = "显示歌曲标题状态图标 (▶️/⏸️)",
            Location = new Point(15, 25),
            AutoSize = true,
            BackColor = Color.White
        };

        _showArtistIconCheckBox = new CheckBox
        {
            Text = "显示歌手图标 (🎤)",
            Location = new Point(15, 50),
            AutoSize = true,
            BackColor = Color.White
        };

        _showAlbumIconCheckBox = new CheckBox
        {
            Text = "显示专辑图标 (💿)",
            Location = new Point(15, 75),
            AutoSize = true,
            BackColor = Color.White
        };

        groupBox.Controls.AddRange([_showTitleIconCheckBox, _showArtistIconCheckBox, _showAlbumIconCheckBox]);
        return groupBox;
    }

    private GroupBox CreateStatusDisplayGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "用户状态优先显示内容",
            Location = new Point(10, yOffset),
            Size = new Size(410, 80),
            BackColor = Color.White
        };

        yOffset += 95;

        var statusDisplayLabel = new Label
        {
            Text = "优先显示:",
            Location = new Point(15, 30),
            AutoSize = true,
            BackColor = Color.White
        };

        _statusDisplayComboBox = new ComboBox
        {
            Location = new Point(90, 25),
            Size = new Size(200, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White
        };

        _statusDisplayComboBox.Items.AddRange("播放器名称", "歌手名称", "歌曲名称");

        groupBox.Controls.AddRange([statusDisplayLabel, _statusDisplayComboBox]);
        return groupBox;
    }

    private GroupBox CreatePartialStatusGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "部分状态显示",
            Location = new Point(10, yOffset),
            Size = new Size(410, 120),
            BackColor = Color.White
        };

        yOffset += 135;

        _showArtistNameCheckBox = new CheckBox
        {
            Text = "显示歌手名称",
            Location = new Point(15, 25),
            AutoSize = true,
            BackColor = Color.White
        };

        _showAlbumInfoCheckBox = new CheckBox
        {
            Text = "显示专辑信息",
            Location = new Point(15, 50),
            AutoSize = true,
            BackColor = Color.White
        };

        _albumTooltipLabel = new Label
        {
            Text = "💡 提示：如果隐藏专辑信息，专辑封面的tooltip也不会显示",
            Location = new Point(15, 80),
            Size = new Size(380, 30),
            ForeColor = Color.Gray,
            BackColor = Color.White
        };

        groupBox.Controls.AddRange([_showArtistNameCheckBox, _showAlbumInfoCheckBox, _albumTooltipLabel]);
        return groupBox;
    }

    private GroupBox CreatePerformanceGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "性能监控",
            Location = new Point(10, yOffset),
            Size = new Size(410, 150),
            BackColor = Color.White
        };

        yOffset += 165;

        // 内存使用情况标签
        var memoryLabel = new Label
        {
            Text = "内存使用情况:",
            Location = new Point(15, 25),
            AutoSize = true,
            BackColor = Color.White,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold)
        };

        // 内存信息显示
        var memoryInfoLabel = new Label
        {
            Name = "memoryInfoLabel",
            Text = "点击刷新查看当前内存使用情况",
            Location = new Point(15, 45),
            Size = new Size(380, 40),
            BackColor = Color.White,
            ForeColor = Color.Gray
        };

        // 刷新按钮
        var refreshButton = new Button
        {
            Text = "刷新内存信息",
            Location = new Point(15, 90),
            Size = new Size(100, 25),
            BackColor = Color.White,
            Font = new Font("Microsoft YaHei", 8)
        };
        refreshButton.Click += RefreshMemoryInfo_Click;

        groupBox.Controls.AddRange([memoryLabel, memoryInfoLabel, refreshButton]);
        return groupBox;
    }

    private Panel CreateButtonPanelControl()
    {
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Size = new Size(410, 50),
            BackColor = Color.White
        };

        const int buttonWidth = 80;
        const int buttonHeight = 30;
        const int spacing = 10;
        const int totalWidth = buttonWidth * 3 + spacing * 2;
        const int startX = (410 - totalWidth) / 2;

        _okButton = new Button
        {
            Text = "确定",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(startX, 10),
            DialogResult = DialogResult.OK,
            BackColor = Color.White
        };

        _cancelButton = new Button
        {
            Text = "取消",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(startX + buttonWidth + spacing, 10),
            DialogResult = DialogResult.Cancel,
            BackColor = Color.White
        };

        _applyButton = new Button
        {
            Text = "应用",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(startX + (buttonWidth + spacing) * 2, 10),
            BackColor = Color.White
        };

        _okButton.Click += OkButton_Click;
        _cancelButton.Click += CancelButton_Click;
        _applyButton.Click += ApplyButton_Click;

        buttonPanel.Controls.AddRange([_okButton, _cancelButton, _applyButton]);
        return buttonPanel;
    }

    private void RefreshMemoryInfo_Click(object? sender, EventArgs e)
    {
        try
        {
            var memoryInfo = PerformanceMonitor.GetMemoryInfo();
            var cacheStats = PerformanceMonitor.GetCacheStatistics();

            if (Controls.Find("memoryInfoLabel", true).FirstOrDefault() is not Label memoryLabel) return;

            memoryLabel.Text = $"""
                                工作集: {memoryInfo.GetFormattedWorkingSet()}, 私有内存: {memoryInfo.GetFormattedPrivateMemory()}
                                GC内存: {memoryInfo.GetFormattedGcMemory()}, 缓存项: {cacheStats.TotalCacheCount}
                                """;
            memoryLabel.ForeColor = Color.Black;
        }
        catch (Exception ex)
        {
            if (Controls.Find("memoryInfoLabel", true).FirstOrDefault() is Label memoryLabel)
            {
                memoryLabel.Text = $"获取内存信息失败: {ex.Message}";
                memoryLabel.ForeColor = Color.Red;
            }
        }
    }

    private void LoadSettings()
    {
        _autoStartCheckBox!.Checked = _originalSettings.AutoStart;
        _closeToTrayCheckBox!.Checked = _originalSettings.CloseToTray;
        _startInTrayCheckBox!.Checked = _originalSettings.StartInTray;

        _showTitleIconCheckBox!.Checked = _originalSettings.ShowTitleIcon;
        _showArtistIconCheckBox!.Checked = _originalSettings.ShowArtistIcon;
        _showAlbumIconCheckBox!.Checked = _originalSettings.ShowAlbumIcon;

        // 设置状态显示下拉框
        _statusDisplayComboBox!.SelectedIndex = (int)_originalSettings.StatusDisplayType;

        _showArtistNameCheckBox!.Checked = _originalSettings.ShowArtistName;
        _showAlbumInfoCheckBox!.Checked = _originalSettings.ShowAlbumInfo;
    }

    private void SaveSettings()
    {
        var settings = Configurations.Instance.Settings;

        settings.AutoStart = _autoStartCheckBox!.Checked;
        settings.CloseToTray = _closeToTrayCheckBox!.Checked;
        settings.StartInTray = _startInTrayCheckBox!.Checked;

        settings.ShowTitleIcon = _showTitleIconCheckBox!.Checked;
        settings.ShowArtistIcon = _showArtistIconCheckBox!.Checked;
        settings.ShowAlbumIcon = _showAlbumIconCheckBox!.Checked;

        settings.StatusDisplayType = (StatusDisplayType)_statusDisplayComboBox!.SelectedIndex;

        settings.ShowArtistName = _showArtistNameCheckBox!.Checked;
        settings.ShowAlbumInfo = _showAlbumInfoCheckBox!.Checked;

        // 为了向后兼容，同时更新 UseDetailsForStatus
        settings.UseDetailsForStatus = settings.StatusDisplayType == StatusDisplayType.Details;

        Configurations.Instance.Save();

        // 请求RPC刷新
        Program.GetRpcManager()?.RequestStateRefresh();

        // 更新开机自启
        if (settings.AutoStart == _originalSettings.AutoStart) return;

        var success = Win32Api.AutoStart.Set(settings.AutoStart);
        if (!success)
        {
            MessageBox.Show(
                $"无法 {(settings.AutoStart ? "设置" : "取消")} 开机自启。\n请尝试以管理员权限运行本程序一次。",
                "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        SaveSettings();
    }
}