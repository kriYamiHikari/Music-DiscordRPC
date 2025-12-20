using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicRpc.Utils;

/// <summary>
/// 激进的内存优化工具
/// </summary>
internal static partial class MemoryOptimizer
{
    [LibraryImport("psapi.dll")]
    private static partial int EmptyWorkingSet(IntPtr hProcess);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    /// <summary>
    /// 执行激进的内存优化
    /// </summary>
    public static async Task PerformAggressiveOptimizationAsync()
    {
        // 检查是否满足优化条件：只有在程序处于后台（最小化或无可见窗口）时才执行
        if (!IsAppInBackground())
        {
            Logger.Memory("App is in foreground or active, skipping optimization to ensure UI smoothness.");
            return;
        }

        try
        {
            // 将所有耗时操作移到后台线程，避免阻塞 UI
            await Task.Run(async () =>
            {
                // 分析托管内存 (仅在 DEBUG 模式下有效)
                ObjectMemoryAnalyzer.LogMemoryAnalysis();

                // 强制 GC
                Logger.Memory("App is in background. Starting GC collection...");
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();

                // 只有在内存压力极大时才进行第二次回收
                if (GC.GetTotalMemory(false) > 40 * 1024 * 1024)
                {
                    await Task.Delay(100);
                    GC.Collect();
                }

                // 尝试压缩工作集 (Win32 API)
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var hProcess = GetCurrentProcess();

                    if (EmptyWorkingSet(hProcess) == 0)
                    {
                        Logger.Memory("EmptyWorkingSet returned 0.");
                    }

                    if (!SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1)))
                    {
                        Logger.Memory("SetProcessWorkingSetSize failed.");
                    }
                }

                // 清理临时文件和缓存
                ClearTemporaryResources();

                Logger.Memory("Background aggressive optimization completed.");
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Memory optimization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查应用程序是否处于后台状态
    /// </summary>
    private static bool IsAppInBackground()
    {
        try
        {
            // 获取所有打开的窗体
            var openForms = Application.OpenForms.Cast<Form>().ToList();

            // 如果没有任何窗体显示（例如只在托盘运行），视为后台
            if (openForms.Count == 0 || openForms.All(f => !f.Visible))
            {
                return true;
            }

            // 如果主窗口存在但处于最小化状态，视为后台
            var mainForm = openForms.OfType<MainForm>().FirstOrDefault();
            if (mainForm != null && (mainForm.WindowState == FormWindowState.Minimized || !mainForm.Visible))
            {
                // 即使主窗口最小化，也要确保没有其他模态窗口（如设置窗口）正在显示
                return !openForms.Any(f => f is SettingsForm || f.Modal);
            }

            // 其他情况（主窗口正常显示或设置窗口打开）均视为前台
            return false;
        }
        catch
        {
            // 发生异常时保守起见，认为在前台
            return false;
        }
    }

    /// <summary>
    /// 清理临时资源
    /// </summary>
    private static void ClearTemporaryResources()
    {
        try
        {
            // 清理图片缓存
            ImageCacheManager.ForceCleanupCache(3);

            // 清理其他缓存
            MemoryPressureMonitor.UnregisterImageSize(0);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to clear temporary resources: {ex.Message}");
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize,
        IntPtr dwMaximumWorkingSetSize);
}