using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        try
        {
            // 分析托管内存
            ObjectMemoryAnalyzer.LogMemoryAnalysis();

            await Task.Run(async () =>
            {
                // 强制多次GC
                for (var i = 0; i < 3; i++)
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await Task.Delay(50);
                }
            });

            // 尝试压缩工作集
            var process = Process.GetCurrentProcess();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // 使用EmptyWorkingSet API
                var result = EmptyWorkingSet(GetCurrentProcess());
                if (result == 0)
                {
                    Debug.WriteLine("[WARNING] EmptyWorkingSet failed, trying alternative method");
                }

                // 或者使用SetProcessWorkingSetSize
                if (!SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1)))
                {
                    Debug.WriteLine("[WARNING] SetProcessWorkingSetSize failed");
                }
            }

            // 清理临时文件和缓存
            ClearTemporaryResources();

            // 通知系统回收内存
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            if (!SetProcessWorkingSetSize(process.Handle, new IntPtr(0xFFFFFFFF), new IntPtr(0xFFFFFFFF)))
            {
                Debug.WriteLine("[WARNING] Final SetProcessWorkingSetSize failed");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Memory optimization failed: {ex.Message}");
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
            Debug.WriteLine($"[ERROR] Failed to clear temporary resources: {ex.Message}");
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize,
        IntPtr dwMaximumWorkingSetSize);
}