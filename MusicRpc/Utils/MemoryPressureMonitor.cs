using System;
using System.Threading;

namespace MusicRpc.Utils;

/// <summary>
/// 内存压力监控器，用于动态管理内存使用
/// </summary>
internal static class MemoryPressureMonitor
{
    private const long MaxMemoryBytes = 40 * 1024 * 1024; // 40MB
    private static long _currentCacheMemory;
    private static readonly Lock LockObject = new();

    /// <summary>
    /// 检查是否应该淘汰缓存项
    /// </summary>
    public static bool ShouldEvictItems(long newImageSize)
    {
        lock (LockObject)
        {
            return _currentCacheMemory + newImageSize > MaxMemoryBytes;
        }
    }

    /// <summary>
    /// 检查是否需要主动清理缓存（即使未达到最大限制）
    /// </summary>
    public static bool ShouldProactivelyCleanup()
    {
        lock (LockObject)
        {
            // 当内存使用超过25MB时，开始主动清理
            return _currentCacheMemory > 25 * 1024 * 1024;
        }
    }

    /// <summary>
    /// 获取建议清理的缓存项数量
    /// </summary>
    public static int GetSuggestedCleanupCount()
    {
        lock (LockObject)
        {
            // 根据当前内存使用情况计算需要清理的项数
            var memoryUsage = _currentCacheMemory;
            return memoryUsage switch
            {
                > 35 * 1024 * 1024 => 15,
                > 30 * 1024 * 1024 => 10,
                > 25 * 1024 * 1024 => 5,
                _ => 3
            };
        }
    }

    /// <summary>
    /// 注册图片大小到内存监控
    /// </summary>
    public static void RegisterImageSize(long size)
    {
        lock (LockObject)
        {
            _currentCacheMemory += size;
        }
    }

    /// <summary>
    /// 从内存监控中移除图片大小
    /// </summary>
    public static void UnregisterImageSize(long size)
    {
        lock (LockObject)
        {
            _currentCacheMemory = Math.Max(0, _currentCacheMemory - size);
        }
    }

    /// <summary>
    /// 获取当前缓存内存使用量
    /// </summary>
    public static long GetCurrentMemoryUsage()
    {
        lock (LockObject)
        {
            return _currentCacheMemory;
        }
    }

    /// <summary>
    /// 估算图片内存大小
    /// </summary>
    public static long EstimateImageSize(System.Drawing.Image image)
    {
        // 更准确的估算：宽 × 高 × 4字节(RGBA) + 20%的GDI+开销
        var baseSize = (long)image.Width * image.Height * 4;
        return (long)(baseSize * 1.2);
    }
}