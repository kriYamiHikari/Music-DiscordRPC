using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace MusicRpc.Utils;

/// <summary>
/// 提供全局共享的 HttpClient 实例
/// </summary>
internal static class HttpClientManager
{
    private static readonly Lazy<HttpClient> SharedClientLazy = new(() =>
    {
        var handler = new HttpClientHandler
        {
            // 启用自动解压，HttpClient 会自动处理 gzip 和 deflate
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = PerformanceConfig.HttpClientTimeout,
        };

        return client;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 获取共享的 HttpClient 实例
    /// </summary>
    public static HttpClient SharedClient => SharedClientLazy.Value;
}