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
}