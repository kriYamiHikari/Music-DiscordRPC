using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MusicRpc.Utils;

/// <summary>
/// 性能监控工具，提供内存使用统计和监控功能
/// </summary>
internal static class PerformanceMonitor
{
    [DllImport("psapi.dll")]
    private static extern int GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS
    {
        public uint cb;
        public uint PageFaultCount;
        public uint PeakWorkingSetSize;
        public uint WorkingSetSize;
        public uint QuotaPeakPagedPoolUsage;
        public uint QuotaPagedPoolUsage;
        public uint QuotaPeakNonPagedPoolUsage;
        public uint QuotaNonPagedPoolUsage;
        public uint PagefileUsage;
        public uint PeakPagefileUsage;
    }

    private static readonly Process _currentProcess = Process.GetCurrentProcess();

    /// <summary>
    /// 获取当前进程的内存使用情况
    /// </summary>
    public static ProcessMemoryInfo GetMemoryInfo()
    {
        try
        {
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;
            var virtualMemory = _currentProcess.VirtualMemorySize64;
            var gcMemory = GC.GetTotalMemory(false);

            return new ProcessMemoryInfo
            {
                WorkingSetSize = workingSet,
                PrivateMemorySize = privateMemory,
                VirtualMemorySize = virtualMemory,
                GcMemorySize = gcMemory,
                Timestamp = DateTime.Now
            };
        }
        catch
        {
            return new ProcessMemoryInfo();
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public static CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            ImageCacheCount = GetImageCacheCount(),
            ModuleCacheCount = GetModuleCacheCount(),
            ProcessModuleCacheCount = GetProcessModuleCacheCount()
        };
    }

    private static int GetImageCacheCount()
    {
        // 这里需要反射来访问私有字段，或者我们可以修改 ImageCacheManager 来暴露这个信息
        // 为了简单起见，这里返回估计值
        return 0;
    }

    private static int GetModuleCacheCount()
    {
        // 这里需要反射来访问私有字段
        return 0;
    }

    private static int GetProcessModuleCacheCount()
    {
        // 这里需要反射来访问私有字段
        return 0;
    }

    /// <summary>
    /// 强制执行垃圾回收并返回内存统计
    /// </summary>
    public static ProcessMemoryInfo ForceGcAndGetMemoryInfo()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return GetMemoryInfo();
    }
}

/// <summary>
/// 进程内存信息
/// </summary>
internal record ProcessMemoryInfo
{
    public long WorkingSetSize { get; init; }
    public long PrivateMemorySize { get; init; }
    public long VirtualMemorySize { get; init; }
    public long GcMemorySize { get; init; }
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 获取格式化的内存大小字符串
    /// </summary>
    public string GetFormattedWorkingSet() => FormatBytes(WorkingSetSize);

    public string GetFormattedPrivateMemory() => FormatBytes(PrivateMemorySize);
    public string GetFormattedVirtualMemory() => FormatBytes(VirtualMemorySize);
    public string GetFormattedGcMemory() => FormatBytes(GcMemorySize);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
internal record CacheStatistics
{
    public int ImageCacheCount { get; init; }
    public int ModuleCacheCount { get; init; }
    public int ProcessModuleCacheCount { get; init; }
    public int TotalCacheCount => ImageCacheCount + ModuleCacheCount + ProcessModuleCacheCount;
}