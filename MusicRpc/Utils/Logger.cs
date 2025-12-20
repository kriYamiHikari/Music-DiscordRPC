using System.Diagnostics;

namespace MusicRpc.Utils;

/// <summary>
/// 统一日志管理工具，仅在 DEBUG 模式下输出日志
/// </summary>
internal static class Logger
{
    [Conditional("DEBUG")]
    public static void Debug(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
    }

    [Conditional("DEBUG")]
    public static void Diagnose(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[DIAGNOSE] {message}");
    }

    [Conditional("DEBUG")]
    public static void Error(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
    }

    [Conditional("DEBUG")]
    public static void Info(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
    }

    [Conditional("DEBUG")]
    public static void Memory(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[MEMORY] {message}");
    }
}