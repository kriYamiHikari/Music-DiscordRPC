using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using MusicRpc.Utils;

namespace MusicRpc;

/// <summary>
/// 应用程序的主入口点类。
/// </summary>
internal static class Program
{
    // Discord 开发者门户中为不同播放器创建的应用ID。
    // https://discord.com/developers/applications
    private const string NetEaseAppId = "1431734033515286610";
    private const string TencentAppId = "1431607752945434655";
    private const string LxMusicAppId = "1435103861089243248";

    private static RpcManager? _rpcManager;
    private static MainForm? _mainForm;

    /// <summary>
    /// 托盘图标实例
    /// </summary>
    private static NotifyIcon? TrayIcon { get; set; }

    /// <summary>
    /// 获取当前RpcManager实例，供其他窗体使用
    /// </summary>
    public static RpcManager? GetRpcManager() => _rpcManager;

    /// <summary>
    /// 应用程序的主入口点方法。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 单实例运行
        using var mutex = new Mutex(true, "MusicDiscordRpc", out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("MusicDiscordRpc is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 为每个播放器初始化一个独立的 Discord RPC 客户端
        var netEaseClient = new DiscordRpcClient(NetEaseAppId);
        var tencentClient = new DiscordRpcClient(TencentAppId);
        var lxMusicClient = new DiscordRpcClient(LxMusicAppId);
        netEaseClient.Initialize();
        tencentClient.Initialize();
        lxMusicClient.Initialize();

        if (!netEaseClient.IsInitialized || !tencentClient.IsInitialized || !lxMusicClient.IsInitialized)
        {
            MessageBox.Show("Failed to initialize Discord RPC client.", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // 核心服务
        _rpcManager = new RpcManager(netEaseClient, tencentClient, lxMusicClient);
        Task.Run(_rpcManager.Start);

        // 创建并显示主窗口
        _mainForm = new MainForm();

        TrayIcon = CreateTrayIcon();
        TrayIcon.Visible = true;

        // 根据配置决定是否显示主窗口
        if (!Configurations.Instance.Settings.StartInTray)
        {
            _mainForm.Show();
        }
        else
        {
            ShowMinimizeToTrayNotification();
        }

        Application.Run();

        // 应用退出时清理资源
        netEaseClient.Dispose();
        tencentClient.Dispose();
        lxMusicClient.Dispose();
    }

    /// <summary>
    /// 创建并配置系统托盘图标及其菜单
    /// </summary>
    private static NotifyIcon CreateTrayIcon()
    {
        // 其他菜单项
        var showSettingsItem = new ToolStripMenuItem("显示设置");
        var showMainWindowItem = new ToolStripMenuItem("显示主窗口");
        var exitMenuItem = new ToolStripMenuItem("退出");

        // 创建上下文菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(
            showMainWindowItem, showSettingsItem, new ToolStripSeparator(),
            exitMenuItem);

        showSettingsItem.Click += (_, _) =>
        {
            using var settingsForm = new SettingsForm();
            settingsForm.StartPosition = FormStartPosition.CenterScreen;
            settingsForm.ShowDialog();
        };

        showMainWindowItem.Click += (_, _) =>
        {
            if (_mainForm == null) return;

            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        };

        exitMenuItem.Click += (_, _) => Application.Exit();

        var notifyIcon = new NotifyIcon
        {
            Icon = AppResource.Icon,
            Text = "Music Discord RPC",
            ContextMenuStrip = contextMenu
        };

        notifyIcon.DoubleClick += (_, _) =>
        {
            if (_mainForm == null) return;

            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        };

        return notifyIcon;
    }

    /// <summary>
    /// 当用户关闭窗口并选择最小化到托盘时，显示通知。
    /// </summary>
    public static void ShowMinimizeToTrayNotification()
    {
        TrayIcon?.ShowBalloonTip(1000, "应用仍在运行", "Music Discord RPC 已最小化到托盘区域。", ToolTipIcon.Info);
    }
}