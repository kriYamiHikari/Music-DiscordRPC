using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace MusicRpc.Win32Api;

/// <summary>
/// 提供管理应用程序开机自启设置的方法。
/// 所有操作都通过读写 Windows 注册表来实现。
/// </summary>
internal static class AutoStart
{
    private const string AppValueName = "MusicRpc";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// 启用或禁用应用程序的开机自启功能。
    /// </summary>
    /// <param name="enable">true 表示启用自启，false 表示禁用。</param>
    /// <returns>如果操作成功，返回 true；否则返回 false。</returns>
    internal static bool Set(bool enable)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Debug.WriteLine("[ERROR] Could not determine the application's executable path.");
            return false;
        }

        using var runKey = OpenRunKey(true);
        if (runKey is null) return false;

        try
        {
            if (enable)
            {
                if (runKey.GetValue(AppValueName) as string != exePath)
                {
                    runKey.SetValue(AppValueName, exePath);
                }
            }
            else
            {
                if (runKey.GetValue(AppValueName) is not null)
                {
                    runKey.DeleteValue(AppValueName, false);
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ERROR] Failed to set auto-start value: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查应用程序当前是否已配置为开机自启。
    /// </summary>
    /// <returns>如果已正确配置自启，返回 true；否则返回 false。</returns>
    public static bool Check()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        using var runKey = OpenRunKey(false);
        if (runKey is null) return false;

        try
        {
            var storedPath = runKey.GetValue(AppValueName) as string;
            return !string.IsNullOrEmpty(storedPath) && exePath.Equals(storedPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ERROR] Failed to check auto-start value: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 打开 `CurrentVersion\Run` 注册表项的辅助方法，用于减少代码重复。
    /// </summary>
    /// <param name="writable">true 表示以可写模式打开，false 表示以只读模式打开。</param>
    private static RegistryKey? OpenRunKey(bool writable)
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey(RunKeyPath, writable);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ERROR] Failed to open registry key '{RunKeyPath}': {e.Message}");
            return null;
        }
    }
}