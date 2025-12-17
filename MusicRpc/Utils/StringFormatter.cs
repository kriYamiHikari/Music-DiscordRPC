using System.Collections.Concurrent;
using System.Text;
using MusicRpc.Models;

namespace MusicRpc.Utils;

/// <summary>
/// StringBuilder对象池，减少字符串拼接时的内存分配
/// </summary>
internal static class StringBuilderPool
{
    private static readonly ConcurrentQueue<StringBuilder> Pool = new();
    private const int MaxPoolSize = 10;

    /// <summary>
    /// 从池中获取StringBuilder实例
    /// </summary>
    public static StringBuilder Get()
    {
        if (!Pool.TryDequeue(out var sb)) return new StringBuilder(256); // 预分配合理容量
        sb.Clear();
        return sb;
    }
    
    /// <summary>
    /// 将StringBuilder实例归还到池中
    /// </summary>
    public static void Return(StringBuilder sb)
    {
        if (sb.Capacity <= 512 && Pool.Count < MaxPoolSize)
        {
            Pool.Enqueue(sb);
        }
    }
}

/// <summary>
/// 字符串格式化工具，优化字符串处理性能
/// </summary>
internal static class StringFormatter
{
    private const char ZeroWidthSpace = '\u200B';

    /// <summary>
    /// 格式化播放器信息，减少字符串拼接的内存分配
    /// </summary>
    public static (string Title, string Artists, string? Album) FormatPlayerInfo(
        PlayerInfo info, 
        Configurations config)
    {
        var sb = StringBuilderPool.Get();
        try
        {
            // 格式化标题
            var titleIcon = config.Settings.ShowTitleIcon 
                ? (info.Pause ? "⏸️ " : "▶️ ") 
                : string.Empty;
            
            sb.Append(titleIcon)
              .Append(info.Title)
              .Append(ZeroWidthSpace);
              
            var formattedTitle = StringUtils.GetTruncatedStringByMaxByteLength(sb.ToString(), 128);
            
            // 格式化艺术家
            sb.Clear();
            var artistIcon = config.Settings.ShowArtistIcon ? "🎤 " : string.Empty;
            
            sb.Append(artistIcon)
              .Append(info.Artists)
              .Append(ZeroWidthSpace);
              
            var formattedArtists = StringUtils.GetTruncatedStringByMaxByteLength(sb.ToString(), 128);
            
            // 格式化专辑
            string? formattedAlbum = null;
            if (!config.Settings.ShowAlbumInfo || string.IsNullOrEmpty(info.Album))
                return (formattedTitle, formattedArtists, formattedAlbum);
            sb.Clear();
            var albumIcon = config.Settings.ShowAlbumIcon ? "💿 " : string.Empty;
                
            sb.Append(albumIcon)
                .Append(info.Album)
                .Append(ZeroWidthSpace);
                  
            formattedAlbum = StringUtils.GetTruncatedStringByMaxByteLength(sb.ToString(), 128);

            return (formattedTitle, formattedArtists, formattedAlbum);
        }
        finally
        {
            StringBuilderPool.Return(sb);
        }
    }
}