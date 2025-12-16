using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MusicRpc.Utils;

/// <summary>
/// 应用程序资源管理器，提供对应用资源的访问
/// </summary>
internal static class AppResource
{
    /// <summary>
    /// 应用程序图标
    /// </summary>
    public static Icon Icon { get; }

    static AppResource()
    {
        try
        {
            // 从嵌入资源加载图标
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "MusicRpc.Resources.icon.ico";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            // 如果嵌入资源不存在，使用默认的系统图标
            Icon = stream != null ? new Icon(stream) : SystemIcons.Application;
        }
        catch
        {
            // 如果加载失败，使用默认的系统图标
            Icon = SystemIcons.Application;
        }
    }
}