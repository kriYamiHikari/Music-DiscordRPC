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

    // RPC 按钮设置控件
    private readonly ComboBox[] _buttonTypeComboBoxes = new ComboBox[2];
    private readonly TextBox[] _buttonLabelTextBoxes = new TextBox[2];
    private readonly TextBox[] _buttonUrlTextBoxes = new TextBox[2];

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
        Size = new Size(480, 700);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;

        // 创建中间的滚动面板（填充剩余空间）
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        var yOffset = 10;

        // 程序设置组
        var programGroupBox = CreateProgramSettingsGroup(ref yOffset);
        scrollPanel.Controls.Add(programGroupBox);

        // RPC图标显示组
        var rpcIconGroupBox = CreateRpcIconSettingsGroup(ref yOffset);
        scrollPanel.Controls.Add(rpcIconGroupBox);

        // 用户状态优先显示内容组
        var statusDisplayGroupBox = CreateStatusDisplayGroup(ref yOffset);
        scrollPanel.Controls.Add(statusDisplayGroupBox);

        // 部分状态显示组
        var partialStatusGroupBox = CreatePartialStatusGroup(ref yOffset);
        scrollPanel.Controls.Add(partialStatusGroupBox);

        // RPC 按钮设置组
        var rpcButtonGroupBox = CreateRpcButtonSettingsGroup(ref yOffset);
        scrollPanel.Controls.Add(rpcButtonGroupBox);

        // 性能监控组
        var performanceGroupBox = CreatePerformanceGroup(ref yOffset);
        scrollPanel.Controls.Add(performanceGroupBox);

        // 添加一个底部的占位符，确保滚动条能完全滚动到底部，不被底部固定按钮遮挡
        scrollPanel.Controls.Add(new Label { Location = new Point(0, yOffset), Size = new Size(1, 60) });

        // 创建并添加底部按钮面板
        var buttonPanel = CreateButtonPanelControl();
        Controls.Add(buttonPanel);

        Controls.Add(scrollPanel);
    }

    private int GetHorizontalCenterOffset(int controlWidth)
    {
        // 减去滚动条的大约宽度（约 20 像素）后再计算居中
        // 这样分组框会相对于“可见的白色区域”居中，而不是相对于整个窗口宽度居中
        return (ClientSize.Width - 20 - controlWidth) / 2;
    }

    private GroupBox CreateProgramSettingsGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "程序设置",
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 105),
            BackColor = Color.White
        };

        yOffset += 115;

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
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 100),
            BackColor = Color.White
        };

        yOffset += 110;

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
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 80),
            BackColor = Color.White
        };

        yOffset += 90;

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
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 120),
            BackColor = Color.White
        };

        yOffset += 130;

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

    private GroupBox CreateRpcButtonSettingsGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "RPC 按钮设置 (最多2个)",
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 255), // 稍微增加高度以容纳调整后的布局
            BackColor = Color.White
        };

        yOffset += 270; // 相应增加间距

        // 添加重置按钮
        var resetButton = new Button
        {
            Text = "重置为默认",
            Location = new Point(295, 160), // 移动到第二个按钮下方，右侧对齐
            Size = new Size(100, 25),
            BackColor = Color.White,
            Font = new Font(Font.FontFamily, 8.5f)
        };
        resetButton.Click += (_, _) =>
        {
            ResetButtonsToDefault();
            MessageBox.Show("已重置按钮配置，请点击“应用”或“确定”以保存更改。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        groupBox.Controls.Add(resetButton);

        for (var i = 0; i < 2; i++)
        {
            var startY = 25 + (i * 75);
            var index = i;

            var lblBtn = new Label { Text = $"按钮 {i + 1}:", Location = new Point(15, startY + 3), AutoSize = true };

            _buttonTypeComboBoxes[i] = new ComboBox
            {
                Location = new Point(60, startY),
                Size = new Size(110, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _buttonTypeComboBoxes[i].Items.AddRange(["关闭", "随身听", "项目 Github", "自定义"]);

            var lblText = new Label { Text = "文字:", Location = new Point(180, startY + 3), AutoSize = true };
            _buttonLabelTextBoxes[i] = new TextBox
            {
                Location = new Point(220, startY),
                Size = new Size(175, 23)
            };

            var lblUrl = new Label { Text = "链接:", Location = new Point(15, startY + 33), AutoSize = true };
            _buttonUrlTextBoxes[i] = new TextBox
            {
                Location = new Point(60, startY + 30),
                Size = new Size(335, 23)
            };

            _buttonTypeComboBoxes[i].SelectedIndexChanged += (_, _) =>
            {
                _buttonUrlTextBoxes[index].Enabled = _buttonTypeComboBoxes[index].SelectedIndex == 3; // Custom
            };

            groupBox.Controls.AddRange(
            [
                lblBtn, _buttonTypeComboBoxes[i],
                lblText, _buttonLabelTextBoxes[i],
                lblUrl, _buttonUrlTextBoxes[i]
            ]);
        }

        // 添加安全免责声明
        var lblWarning = new Label
        {
            Text = "⚠️ 免责声明：自定义链接由用户自行设定。请确保链接安全合规，由此产生的任何风险由用户自行承担，与软件开发者无关。",
            Location = new Point(15, 195), // 向下移动，为重置按钮留出空间
            Size = new Size(380, 45),
            ForeColor = Color.Firebrick,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold)
        };
        groupBox.Controls.Add(lblWarning);

        return groupBox;
    }

    private void ResetButtonsToDefault()
    {
        // 按钮 1 默认：随身听
        _buttonTypeComboBoxes[0].SelectedIndex = (int)RpcButtonType.MusicLink;
        _buttonLabelTextBoxes[0].Text = "🎧 Listen";
        _buttonUrlTextBoxes[0].Text = string.Empty;
        _buttonUrlTextBoxes[0].Enabled = false;

        // 按钮 2 默认：GitHub
        _buttonTypeComboBoxes[1].SelectedIndex = (int)RpcButtonType.GitHubRepo;
        _buttonLabelTextBoxes[1].Text = "🔍 View App on GitHub";
        _buttonUrlTextBoxes[1].Text = string.Empty;
        _buttonUrlTextBoxes[1].Enabled = false;
    }

    private GroupBox CreatePerformanceGroup(ref int yOffset)
    {
        var groupBox = new GroupBox
        {
            Text = "性能监控",
            Location = new Point(GetHorizontalCenterOffset(410), yOffset),
            Size = new Size(410, 150),
            BackColor = Color.White
        };

        yOffset += 160;

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
            Height = 60,
            BackColor = Color.FromArgb(245, 245, 245), // 使用更柔和的浅灰色区分
            Padding = new Padding(0, 5, 0, 0)
        };

        const int buttonWidth = 80;
        const int buttonHeight = 30;
        const int spacing = 10;
        const int totalWidth = buttonWidth * 3 + spacing * 2;
        var startX = (ClientSize.Width - totalWidth) / 2;

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

        // 加载按钮设置
        for (var i = 0; i < 2; i++)
        {
            if (i >= _originalSettings.CustomButtons.Count) continue;
            var btn = _originalSettings.CustomButtons[i];
            _buttonTypeComboBoxes[i].SelectedIndex = (int)btn.Type;
            _buttonLabelTextBoxes[i].Text = btn.Label;
            _buttonUrlTextBoxes[i].Text = btn.CustomUrl;
        }
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

        // 保存按钮设置
        settings.CustomButtons.Clear();
        for (var i = 0; i < 2; i++)
        {
            settings.CustomButtons.Add(new RpcButtonSetting
            {
                Type = (RpcButtonType)_buttonTypeComboBoxes[i].SelectedIndex,
                Label = _buttonLabelTextBoxes[i].Text,
                CustomUrl = _buttonUrlTextBoxes[i].Text
            });
        }

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