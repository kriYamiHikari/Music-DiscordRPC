using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MusicRpc.Utils;

/// <summary>
/// 内存分析工具，用于详细监控和分析内存使用情况
/// </summary>
internal static class MemoryProfiler
{
    /// <summary>
    /// 获取详细的内存使用报告
    /// </summary>
    private static string GetDetailedMemoryReport()
    {
        var sb = new StringBuilder();
        using var process = Process.GetCurrentProcess();

        // 基本内存信息
        sb.AppendLine("=== 基本内存信息 ===");
        sb.AppendLine($"工作集 (Working Set): {process.WorkingSet64 / 1024 / 1024:F1} MB");
        sb.AppendLine($"私有内存 (Private Memory): {process.PrivateMemorySize64 / 1024 / 1024:F1} MB");
        sb.AppendLine($"虚拟内存 (Virtual Memory): {process.VirtualMemorySize64 / 1024 / 1024:F1} MB");
        sb.AppendLine($"托管内存 (Managed Memory): {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");
        sb.AppendLine($"托管内存 (包含已释放): {GC.GetTotalMemory(true) / 1024 / 1024:F1} MB");

        // GC信息
        sb.AppendLine("\n=== GC信息 ===");
        sb.AppendLine($"Gen 0: {GC.CollectionCount(0)} 次");
        sb.AppendLine($"Gen 1: {GC.CollectionCount(1)} 次");
        sb.AppendLine($"Gen 2: {GC.CollectionCount(2)} 次");
        sb.AppendLine($"总内存: {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");

        // 各代内存使用
        sb.AppendLine("\n=== 各代内存使用 ===");
        for (var i = 0; i <= GC.MaxGeneration; i++)
        {
            sb.AppendLine($"Gen {i}: {GC.GetGeneration(GC.GetTotalMemory(false))} 代");
        }

        // 大对象堆信息
        sb.AppendLine("\n=== 大对象堆信息 ===");
        sb.AppendLine($"LOH大小: {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");

        // 程序集信息
        sb.AppendLine("\n=== 已加载程序集 ===");
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        sb.AppendLine($"已加载程序集数量: {assemblies.Length}");

        var assemblySizes = new Dictionary<string, long>();
        foreach (var assembly in assemblies)
        {
            try
            {
                if (string.IsNullOrEmpty(assembly.Location)) continue;
                var fileInfo = new System.IO.FileInfo(assembly.Location);
                var name = assembly.GetName().Name;
                if (name != null) assemblySizes[name] = fileInfo.Length;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get assembly size for {assembly.GetName().Name}: {ex.Message}");
            }
        }

        var topAssemblies = assemblySizes.OrderByDescending(kvp => kvp.Value).Take(10);
        foreach (var asm in topAssemblies)
        {
            sb.AppendLine($"  {asm.Key}: {asm.Value / 1024:F1} KB");
        }

        // 进程模块信息
        sb.AppendLine("\n=== 进程模块 ===");
        try
        {
            var modules = process.Modules.Cast<ProcessModule>().Take(10);
            foreach (var module in modules)
            {
                sb.AppendLine($"  {module.ModuleName}: {module.ModuleMemorySize / 1024:F1} KB");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"获取模块信息失败: {ex.Message}");
        }

        // 线程信息
        sb.AppendLine("\n=== 线程信息 ===");
        sb.AppendLine($"线程数量: {process.Threads.Count}");

        // 句柄信息
        sb.AppendLine("\n=== 句柄信息 ===");
        sb.AppendLine($"句柄数量: {process.HandleCount}");

        return sb.ToString();
    }

    /// <summary>
    /// 记录内存快照
    /// </summary>
    [Conditional("DEBUG")]
    public static void LogMemorySnapshot(string label)
    {
        var report = GetDetailedMemoryReport();
        Logger.Memory($"\n[MEMORY SNAPSHOT] {label} - {DateTime.Now:HH:mm:ss.fff}");
        Logger.Memory(report);
    }

    /// <summary>
    /// 比较两个内存快照的差异
    /// </summary>
    [Conditional("DEBUG")]
    public static void CompareMemorySnapshots(string beforeLabel, string afterLabel)
    {
        using var process = Process.GetCurrentProcess();

        Logger.Memory($"\n[MEMORY COMPARISON] {beforeLabel} vs {afterLabel}");
        Logger.Memory($"工作集变化: {process.WorkingSet64 / 1024 / 1024:F1} MB");
        Logger.Memory($"私有内存变化: {process.PrivateMemorySize64 / 1024 / 1024:F1} MB");
        Logger.Memory($"托管内存变化: {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");
    }

    /// <summary>
    /// 强制垃圾回收并记录结果
    /// </summary>
    [Conditional("DEBUG")]
    public static void ForceGcAndLog()
    {
        var beforeMemory = GC.GetTotalMemory(false);
        Logger.Memory($"[GC] 执行前托管内存: {beforeMemory / 1024 / 1024:F1} MB");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterMemory = GC.GetTotalMemory(true);
        var freedMemory = beforeMemory - afterMemory;
        Logger.Memory($"[GC] 执行后托管内存: {afterMemory / 1024 / 1024:F1} MB");
        Logger.Memory($"[GC] 释放内存: {freedMemory / 1024 / 1024:F1} MB");
    }
}