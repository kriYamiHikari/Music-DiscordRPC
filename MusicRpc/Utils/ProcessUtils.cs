using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MusicRpc.Utils;

/// <summary>
/// 提供与进程相关的实用工具方法。
/// </summary>
internal static class ProcessUtils
{
    // 用于缓存已查询过的模块基地址。
    // 键是 (进程ID, 模块名)，值是模块的基地址。
    // 性能优化用，遍历进程模块列表是一个相对耗时的操作。
    private static readonly Dictionary<(int, string), nint> ModuleAddressCache = new();

    // 记录上一次查询的进程ID，用于智能地清空缓存。
    private static int _lastPid = -1;

    // 最后清理时间
    private static DateTime _lastCleanupTime = DateTime.Now;

    /// <summary>
    /// 获取指定进程中模块的基地址，利用缓存避免重复的慢速查询。
    /// </summary>
    /// <param name="pid">目标进程的ID。</param>
    /// <param name="moduleName">要查找的模块名称（例如 "cloudmusic.dll"）。</param>
    /// <returns>模块的基地址；如果找不到，则返回 IntPtr.Zero。</returns>
    public static nint GetModuleBaseAddress(int pid, string moduleName)
    {
        if (pid != _lastPid)
        {
            ModuleAddressCache.Clear();
            _lastPid = pid;
        }

        // 检查是否需要清理缓存
        var now = DateTime.Now;
        if ((now - _lastCleanupTime) > PerformanceConfig.ProcessModuleCacheCleanupInterval ||
            ModuleAddressCache.Count > PerformanceConfig.ProcessModuleCacheMaxSize)
        {
            CleanupCache();
            _lastCleanupTime = now;
        }

        var cacheKey = (pid, moduleName);
        if (ModuleAddressCache.TryGetValue(cacheKey, out var cachedAddress))
        {
            return cachedAddress;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            foreach (ProcessModule module in process.Modules)
            {
                if (!module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase)) continue;
                var baseAddress = module.BaseAddress;
                ModuleAddressCache[cacheKey] = baseAddress;
                return baseAddress;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to get modules for PID {pid}: {ex.Message}");
        }

        ModuleAddressCache[cacheKey] = IntPtr.Zero;
        return IntPtr.Zero;
    }

    /// <summary>
    /// 清理过期的缓存项，避免内存无限增长
    /// </summary>
    private static void CleanupCache()
    {
        // 更激进的清理策略：如果缓存大小超过限制的一半就开始清理
        if (ModuleAddressCache.Count <= PerformanceConfig.ProcessModuleCacheMaxSize / 2)
            return;

        // 清理70%的缓存项，只保留最常用的30%
        var keysToRemove = (int)(ModuleAddressCache.Count * 0.7);
        var cacheList = new List<(int, string)>(ModuleAddressCache.Keys);

        // 按进程ID排序，优先清理旧进程的缓存
        cacheList.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        for (var i = 0; i < keysToRemove && i < cacheList.Count; i++)
        {
            ModuleAddressCache.Remove(cacheList[i]);
        }

        Debug.WriteLine(
            $"[DIAGNOSE] Process module cache cleanup: removed {keysToRemove} items, {ModuleAddressCache.Count} items remaining");
    }
}