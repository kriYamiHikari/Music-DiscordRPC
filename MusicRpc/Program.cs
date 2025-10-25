using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;

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

    private static RpcManager? _rpcManager;

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
        netEaseClient.Initialize();
        tencentClient.Initialize();

        if (!netEaseClient.IsInitialized || !tencentClient.IsInitialized)
        {
            MessageBox.Show("Failed to initialize Discord RPC client.", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // 核心服务
        _rpcManager = new RpcManager(netEaseClient, tencentClient);
        Task.Run(_rpcManager.Start);

        // 托盘图标
        using var trayIcon = CreateTrayIcon();
        trayIcon.Visible = true;
        Application.Run();

        // 应用退出时清理RPC客户端
        netEaseClient.Dispose();
        tencentClient.Dispose();
    }

    /// <summary>
    /// 创建并配置系统托盘图标及其菜单
    /// </summary>
    private static NotifyIcon CreateTrayIcon()
    {
        var config = Configurations.Instance;

        var toggleTitleIconItem = new ToolStripMenuItem("显示歌曲标题状态图标(▶️)", null)
            { CheckOnClick = true, Checked = config.Settings.ShowTitleIcon };
        var toggleArtistIconItem = new ToolStripMenuItem("显示歌手图标(🎤)", null)
            { CheckOnClick = true, Checked = config.Settings.ShowArtistIcon };
        var toggleAlbumIconItem = new ToolStripMenuItem("显示专辑图标(💿)", null)
            { CheckOnClick = true, Checked = config.Settings.ShowAlbumIcon };
        var toggleStatusDisplayItem = new ToolStripMenuItem("在用户状态优先显示歌曲标题", null)
            { CheckOnClick = true, Checked = config.Settings.UseDetailsForStatus };

        var autoStartMenuItem = new ToolStripMenuItem("开机自启") { CheckOnClick = true };
        var exitMenuItem = new ToolStripMenuItem("退出");

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(autoStartMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(toggleTitleIconItem);
        contextMenu.Items.Add(toggleArtistIconItem);
        contextMenu.Items.Add(toggleAlbumIconItem);
        contextMenu.Items.Add(toggleStatusDisplayItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        autoStartMenuItem.Checked = Win32Api.AutoStart.Check();
        autoStartMenuItem.Click += (_, _) =>
        {
            var isEnabled = autoStartMenuItem.Checked;
            var success = Win32Api.AutoStart.Set(autoStartMenuItem.Checked);

            if (success) return;

            MessageBox.Show($"无法 {(isEnabled ? "设置" : "取消")} 开机自启。\n请尝试以管理员权限运行本程序一次。",
                "操作失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            autoStartMenuItem.Checked = !isEnabled;
        };
        exitMenuItem.Click += (_, _) => Application.Exit();

        toggleTitleIconItem.Click += (_, _) =>
        {
            config.Settings.ShowTitleIcon = toggleTitleIconItem.Checked;
            config.Save();
            _rpcManager?.RequestStateRefresh();
        };
        toggleArtistIconItem.Click += (_, _) =>
        {
            config.Settings.ShowArtistIcon = toggleArtistIconItem.Checked;
            config.Save();
            _rpcManager?.RequestStateRefresh();
        };
        toggleAlbumIconItem.Click += (_, _) =>
        {
            config.Settings.ShowAlbumIcon = toggleAlbumIconItem.Checked;
            config.Save();
            _rpcManager?.RequestStateRefresh();
        };
        toggleStatusDisplayItem.Click += (_, _) =>
        {
            config.Settings.UseDetailsForStatus = toggleStatusDisplayItem.Checked;
            config.Save();
            _rpcManager?.RequestStateRefresh();
        };

        return new NotifyIcon
        {
            Icon = AppResource.icon,
            Text = "Music Discord RPC",
            ContextMenuStrip = contextMenu
        };
    }
}