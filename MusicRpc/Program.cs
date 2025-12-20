using System;
using System.Diagnostics;
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
        // 记录启动时的内存状态
        MemoryProfiler.LogMemorySnapshot("程序启动前");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MemoryProfiler.LogMemorySnapshot("初始化UI设置后");

        // 单实例运行
        using var mutex = new Mutex(true, "MusicDiscordRpc", out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("MusicDiscordRpc is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 用于取消后台任务
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // 为每个播放器初始化一个独立的 Discord RPC 客户端
        MemoryProfiler.LogMemorySnapshot("Discord客户端创建前");
        var netEaseClient = new DiscordRpcClient(NetEaseAppId);
        var tencentClient = new DiscordRpcClient(TencentAppId);
        var lxMusicClient = new DiscordRpcClient(LxMusicAppId);
        MemoryProfiler.LogMemorySnapshot("Discord客户端创建后，初始化前");
        
        Task.WaitAll([
            InitClientAsync(netEaseClient, token),
            InitClientAsync(tencentClient, token),
            InitClientAsync(lxMusicClient, token)
        ], token);

        MemoryProfiler.LogMemorySnapshot("Discord客户端初始化后");
        if (!netEaseClient.IsInitialized || !tencentClient.IsInitialized || !lxMusicClient.IsInitialized)
        {
            MessageBox.Show("Failed to initialize Discord RPC client.", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // 核心服务
        _rpcManager = new RpcManager(netEaseClient, tencentClient, lxMusicClient);
        Task.Run(_rpcManager.Start, token);
        MemoryProfiler.LogMemorySnapshot("RPC管理器启动后");

        // 启动全局内存监控
        Task.Run(() => GlobalMemoryMonitor(token), token);

        // 延迟执行初始内存优化
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, token); // 等待5秒让程序完全启动
                MemoryProfiler.LogMemorySnapshot("启动后优化前");
                await MemoryOptimizer.PerformAggressiveOptimizationAsync();
                MemoryProfiler.LogMemorySnapshot("启动后优化后");
            }
            catch (OperationCanceledException)
            {
                // 忽略取消异常，正常退出
            }
        }, token);

        // 创建并显示主窗口
        MemoryProfiler.LogMemorySnapshot("主窗体创建前");
        _mainForm = new MainForm();
        MemoryProfiler.LogMemorySnapshot("主窗体创建后");
        TrayIcon = CreateTrayIcon();
        TrayIcon.Visible = true;
        MemoryProfiler.LogMemorySnapshot("托盘图标创建后");
        // 强制GC查看是否可以回收内存
        MemoryProfiler.ForceGcAndLog();
        MemoryProfiler.LogMemorySnapshot("启动完成（GC后）");

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

        // 取消后台任务
        cts.Cancel();

        // 应用退出时清理资源
        netEaseClient.Dispose();
        tencentClient.Dispose();
        lxMusicClient.Dispose();
        return;

        // 静态局部函数：显式传递参数，避免闭包捕获
        static Task InitClientAsync(DiscordRpcClient client, CancellationToken ct) 
            => Task.Run(client.Initialize, ct);
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
    /// 全局内存监控，确保总内存使用不超过60MB
    /// </summary>
    private static async Task GlobalMemoryMonitor(CancellationToken token)
    {
        // 上次执行清理的时间
        var lastCleanupTime = DateTime.MinValue;
        // 上次执行图片缓存清理的时间
        var lastImageCleanupTime = DateTime.MinValue;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;

                // 定期执行图片缓存清理（每2分钟）
                if ((now - lastImageCleanupTime).TotalMinutes >= 2)
                {
                    ImageCacheManager.PerformCleanup();
                    lastImageCleanupTime = now;
                }

                // 检查内存压力
                using var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var privateMemory = process.PrivateMemorySize64;

                switch (workingSet)
                {
                    // 内存限制：50MB触发清理
                    case > 50 * 1024 * 1024:
                    {
                        MemoryProfiler.LogMemorySnapshot("内存超限触发清理");
                        Logger.Memory(
                            $"High memory usage detected - Working Set: {workingSet / 1024 / 1024:F1}MB, Private: {privateMemory / 1024 / 1024:F1}MB");

                        // 执行激进的图片缓存清理
                        ImageCacheManager.PerformAggressiveCleanup();

                        // 使用激进的内存优化器
                        await MemoryOptimizer.PerformAggressiveOptimizationAsync();

                        // 再次检查内存
                        process.Refresh();
                        var newWorkingSet = process.WorkingSet64;
                        var newPrivateMemory = process.PrivateMemorySize64;
                        var freedWorkingSet = workingSet - newWorkingSet;

                        Logger.Memory($"Aggressive cleanup completed.");
                        Logger.Memory(
                            $"Working Set: {workingSet / 1024 / 1024:F1}MB -> {newWorkingSet / 1024 / 1024:F1}MB (freed: {freedWorkingSet / 1024 / 1024:F1}MB)");
                        Logger.Memory(
                            $"Private Memory: {privateMemory / 1024 / 1024:F1}MB -> {newPrivateMemory / 1024 / 1024:F1}MB");

                        MemoryProfiler.LogMemorySnapshot("内存清理后");

                        lastCleanupTime = now;
                        break;
                    }
                    // 如果内存使用适中（>30MB），且距离上次清理超过30秒，执行轻量级清理
                    case > 30 * 1024 * 1024 when (now - lastCleanupTime).TotalSeconds >= 30:
                        // 仅执行图片缓存的激进清理
                        ImageCacheManager.PerformAggressiveCleanup();
                        lastCleanupTime = now;
                        break;
                }

                // 每10秒检查一次（更频繁地监控，但按需执行操作）
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Memory monitor error: {ex.Message}");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 当用户关闭窗口并选择最小化到托盘时，显示通知。
    /// </summary>
    public static void ShowMinimizeToTrayNotification()
    {
        TrayIcon?.ShowBalloonTip(1000, "应用仍在运行", "Music Discord RPC 已最小化到托盘区域。", ToolTipIcon.Info);
    }
}