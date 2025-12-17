using System;
using System.Diagnostics;
using MusicRpc.Win32Api;

namespace MusicRpc.Utils;

/// <summary>
/// 性能监控工具，提供内存使用统计和监控功能
/// </summary>
internal static class PerformanceMonitor
{
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    /// <summary>
    /// 获取当前进程的内存使用情况
    /// </summary>
    public static ProcessMemoryInfo GetMemoryInfo()
    {
        try
        {
            var workingSet = CurrentProcess.WorkingSet64;
            var privateMemory = CurrentProcess.PrivateMemorySize64;
            var virtualMemory = CurrentProcess.VirtualMemorySize64;
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
        try
        {
            var imageCacheType = typeof(ImageCacheManager);
            var cacheField = imageCacheType.GetField("Cache", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (cacheField?.GetValue(null) is System.Collections.IDictionary cacheDict)
            {
                return cacheDict.Count;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to get image cache count: {ex.Message}");
        }
        return 0;
    }

    private static int GetModuleCacheCount()
    {
        try
        {
            var memoryType = typeof(Memory);
            var moduleCacheField = memoryType.GetField("ModuleCache", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (moduleCacheField?.GetValue(null) is System.Collections.IDictionary cacheDict)
            {
                return cacheDict.Count;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to get module cache count: {ex.Message}");
        }
        return 0;
    }

    private static int GetProcessModuleCacheCount()
    {
        try
        {
            var processUtilsType = typeof(ProcessUtils);
            var moduleAddressCacheField = processUtilsType.GetField("ModuleAddressCache", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (moduleAddressCacheField?.GetValue(null) is System.Collections.IDictionary cacheDict)
            {
                return cacheDict.Count;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to get process module cache count: {ex.Message}");
        }
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
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
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