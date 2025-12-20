using System;

namespace MusicRpc.Utils;

/// <summary>
/// 性能优化配置参数集合
/// 用于控制应用程序的内存使用、缓存策略和网络超时等性能相关行为
/// </summary>
internal static class PerformanceConfig
{
    /// <summary>
    /// 图片缓存最大容量（单位：张）
    /// 控制内存中最多缓存的图片对象数量，超过此数量时将按LRU策略淘汰旧图片
    /// 默认值：20张图片
    /// </summary>
    public static int ImageCacheMaxSize { get; } = 20;

    /// <summary>
    /// 内存模块缓存最大容量（单位：个）
    /// 用于缓存已扫描的进程内存模块，避免重复读取同一模块的内存数据
    /// 默认值：10个模块
    /// </summary>
    public static int ModuleCacheMaxSize { get; } = 10;

    /// <summary>
    /// 内存模块缓存清理间隔（单位：分钟）
    /// 定期清理过期的内存模块缓存，释放不再需要的内存资源
    /// 默认值：5分钟执行一次清理检查
    /// </summary>
    public static TimeSpan ModuleCacheCleanupInterval { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 进程模块地址缓存最大容量（单位：个）
    /// 缓存进程模块的基地址信息，避免重复查询进程模块布局
    /// 默认值：50个进程的模块信息
    /// </summary>
    public static int ProcessModuleCacheMaxSize { get; } = 50;

    /// <summary>
    /// 进程模块地址缓存清理间隔（单位：分钟）
    /// 定期清理进程模块地址缓存，适应进程重启或模块加载变化
    /// 默认值：5分钟执行一次清理检查
    /// </summary>
    public static TimeSpan ProcessModuleCacheCleanupInterval { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// HTTP客户端请求超时时间（单位：秒）
    /// 控制网络请求的最大等待时间，包括DNS解析、连接建立和数据传输
    /// 默认值：5秒
    /// </summary>
    public static TimeSpan HttpClientTimeout { get; } = TimeSpan.FromSeconds(5);
}