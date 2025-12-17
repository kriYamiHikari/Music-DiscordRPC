using System;
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
        var client = new HttpClient
        {
            Timeout = PerformanceConfig.HttpClientTimeout,
        };

        // 启用压缩
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        return client;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 获取共享的 HttpClient 实例
    /// </summary>
    public static HttpClient SharedClient => SharedClientLazy.Value;
}