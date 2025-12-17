using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MusicRpc.Utils;

/// <summary>
/// 图片缓存管理
/// </summary>
internal static class ImageCacheManager
{
    private static readonly int MaxCacheSize = PerformanceConfig.ImageCacheMaxSize;
    private static readonly Dictionary<string, LinkedListNode<CacheItem>> Cache = new();
    private static readonly LinkedList<CacheItem> LruList = [];
    private static readonly Lock LockObject = new();

    /// <summary>
    /// 缓存项的内部表示。
    /// </summary>
    private class CacheItem(string key, Image image)
    {
        public string Key { get; } = key;
        public Image Image { get; } = image;
        public DateTime LastAccessed { get; set; } = DateTime.Now;
    }

    static ImageCacheManager()
    {
        // 每5分钟执行一次清理
        _ = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// 定期清理不再使用的图像资源
    /// </summary>
    private static void PerformCleanup(object? state)
    {
        lock (LockObject)
        {
            var now = DateTime.Now;
            // 清理超过15分钟未访问的缓存项
            var keysToRemove =
                (from kvp in Cache where (now - kvp.Value.Value.LastAccessed) > TimeSpan.FromMinutes(15) select kvp.Key)
                .ToList();

            // 清理超过最大缓存大小80%的旧项
            if (Cache.Count > MaxCacheSize * 0.8)
            {
                var excessCount = Cache.Count - (int)(MaxCacheSize * 0.6);
                var sortedByTime = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed).Take(excessCount);
                keysToRemove.AddRange(sortedByTime.Select(kvp => kvp.Key));
            }

            foreach (var key in keysToRemove.Distinct())
            {
                RemoveFromCacheInternal(key);
            }

            if (keysToRemove.Count > 0)
            {
                Debug.WriteLine($"[DIAGNOSE] Cleanup removed {keysToRemove.Count} expired cache items.");
            }
        }
    }

    /// <summary>
    /// 尝试解压缩GZIP数据，如果数据不是GZIP格式则返回原始数据
    /// </summary>
    /// <param name="data">可能被压缩的数据</param>
    /// <returns>解压缩后的数据或原始数据</returns>
    private static byte[] TryDecompressGzip(byte[] data)
    {
        if (data.Length < 2)
        {
            return [];
        }

        // 检查是否是GZIP格式 (1F 8B)
        if (data[0] != 0x1F || data[1] != 0x8B) return data;
        try
        {
            using var compressedStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            Debug.WriteLine(
                $"[DIAGNOSE] Successfully decompressed GZIP data. Original size: {data.Length}, Decompressed size: {resultStream.Length}");
            return resultStream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DIAGNOSE] Failed to decompress GZIP data: {ex.Message}. Falling back to original data.");
            return data;
        }
    }

    /// <summary>
    /// 验证字节数据是否包含有效的图像文件头。
    /// </summary>
    /// <param name="data">图像字节数据。</param>
    /// <returns>如果数据看起来是有效的图像格式（JPEG、PNG、GIF、BMP、WebP）则返回true，否则返回false。</returns>
    private static bool IsValidImageData(byte[] data)
    {
        if (data.Length < 8)
        {
            return false;
        }

        // JPEG: FF D8 (SOI marker)
        if (data is [0xFF, 0xD8, ..])
        {
            return true;
        }

        switch (data.Length)
        {
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            case >= 8 when
                data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A:
            // GIF: "GIF8"
            case >= 4 when
                (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38) && // GIF8
                (data[4] == 0x37 || data[4] == 0x39) && // 7 or 9 (GIF87a or GIF89a)
                data[5] == 0x61:
            // BMP: "BM"
            case >= 2 when data[0] == 0x42 && data[1] == 0x4D:
            // WebP: "RIFF" (0x52 0x49 0x46 0x46) followed by "WEBP" at offset 8
            // WEBP
            case >= 12 when
                data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 && // RIFF
                data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 异步加载图片，并使用自定义的缓存键。
    /// </summary>
    /// <param name="cacheKey">用于在缓存中标识图片的唯一键。</param>
    /// <param name="imageUrl">图片的URL。</param>
    /// <returns>加载的图片对象，如果加载失败则返回 null。</returns>
    public static async Task<Image?> LoadImageAsync(string cacheKey, string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || string.IsNullOrEmpty(cacheKey))
        {
            return null;
        }

        Debug.WriteLine($"[DIAGNOSE] ImageCacheManager received request for key: {cacheKey}");

        Image? cachedImage = null;
        lock (LockObject)
        {
            if (Cache.TryGetValue(cacheKey, out var node))
            {
                // 缓存命中，将节点移动到LRU列表的头部并更新访问时间
                LruList.Remove(node);
                LruList.AddFirst(node);
                node.Value.LastAccessed = DateTime.Now;
                cachedImage = node.Value.Image;
                Debug.WriteLine($"[DIAGNOSE] CACHE HIT for key: {cacheKey}");
            }
        }

        if (cachedImage != null)
        {
            return cachedImage;
        }

        Debug.WriteLine($"[DIAGNOSE] CACHE MISS for key: {cacheKey}. Downloading from URL.");

        const int maxRetries = 3;
        byte[]? imageBytes = null;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            var retryDelay = 100 * (1 << retry);

            try
            {
                var httpClient = HttpClientManager.SharedClient;
                using var response = await httpClient.GetAsync(imageUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine(
                        $"[DIAGNOSE] Image download failed for key {cacheKey}: HTTP Status {response.StatusCode} (Retry {retry + 1}/{maxRetries})");
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/"))
                {
                    Debug.WriteLine(
                        $"[DIAGNOSE] Downloaded content is not an image for key {cacheKey}. Content-Type: {contentType ?? "N/A"} (Retry {retry + 1}/{maxRetries})");
                    return null; // 内容类型明确错误，不重试
                }

                imageBytes = await response.Content.ReadAsByteArrayAsync();

                if (imageBytes is not { Length: > 0 })
                {
                    Debug.WriteLine(
                        $"[DIAGNOSE] Downloaded content is empty or invalid for key: {cacheKey}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                // 尝试解压缩GZIP数据（处理酷我音乐等返回压缩内容的情况）
                var originalLength = imageBytes.Length;
                imageBytes = TryDecompressGzip(imageBytes);
                if (imageBytes.Length != originalLength)
                {
                    Debug.WriteLine(
                        $"[DIAGNOSE] Data was decompressed. Original: {originalLength} bytes, Decompressed: {imageBytes.Length} bytes");
                }

                // 验证图像数据有效性
                if (!IsValidImageData(imageBytes))
                {
                    // 记录文件头字节以便调试
                    var headerHex = imageBytes.Length >= 16
                        ? BitConverter.ToString(imageBytes, 0, 16).Replace("-", " ")
                        : BitConverter.ToString(imageBytes).Replace("-", " ");

                    Debug.WriteLine(
                        $"[DIAGNOSE] Downloaded content is not a valid image format for key: {cacheKey}. Data length: {imageBytes.Length}. Header: {headerHex}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                Image image;
                try
                {
                    var ms = new MemoryStream(imageBytes);
                    image = Image.FromStream(ms);
                    Debug.WriteLine($"[DIAGNOSE] Downloaded and created new image for key: {cacheKey}. Caching it.");
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine(
                        $"[DIAGNOSE] ArgumentException during Image.FromStream for key {cacheKey}. Data length: {imageBytes.Length}. Error: {ex.Message}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                // 估算新图片的内存大小
                var imageSize = MemoryPressureMonitor.EstimateImageSize(image);
                
                lock (LockObject)
                {
                    while (MemoryPressureMonitor.ShouldEvictItems(imageSize) && LruList.Last != null)
                    {
                        var lruNode = LruList.Last;
                        var evictedItem = lruNode.Value;
                        var evictedSize = MemoryPressureMonitor.EstimateImageSize(evictedItem.Image);
                        
                        RemoveFromCacheInternal(evictedItem.Key);
                        MemoryPressureMonitor.UnregisterImageSize(evictedSize);
                        
                        Debug.WriteLine($"[DIAGNOSE] Memory pressure evicted item with key: {evictedItem.Key}, freed: {evictedSize} bytes");
                    }

                    // 创建新的缓存项并添加到缓存
                    var newItem = new CacheItem(cacheKey, image);
                    var newNode = new LinkedListNode<CacheItem>(newItem);
                    LruList.AddFirst(newNode);
                    Cache[cacheKey] = newNode;
                    
                    // 注册新图片的内存大小
                    MemoryPressureMonitor.RegisterImageSize(imageSize);
                    
                    Debug.WriteLine($"[DIAGNOSE] Cached new image with key: {cacheKey}, size: {imageSize} bytes, total cache memory: {MemoryPressureMonitor.GetCurrentMemoryUsage()} bytes");
                }

                return image;
            }
            catch (ArgumentException ex)
            {
                var dataLength = imageBytes?.Length.ToString() ?? "N/A";
                Debug.WriteLine(
                    $"[DIAGNOSE] ArgumentException during image creation for key {cacheKey}. Image data length: {dataLength}. Error: {ex.Message}. Attempt {retry + 1}/{maxRetries}.");
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(retryDelay);
                }
            }
            catch (Exception ex)
            {
                var dataLength = imageBytes?.Length.ToString() ?? "N/A";
                Debug.WriteLine(
                    $"[DIAGNOSE] General Exception during image download/creation for key {cacheKey}. Image data length: {dataLength}. Error: {ex.Message}.");
                return null;
            }
        }

        Debug.WriteLine(
            $"[DIAGNOSE] Failed to download and create image for key {cacheKey} after {maxRetries} attempts.");
        return null;
    }

    /// <summary>
    /// 清理所有缓存并释放内存。
    /// </summary>
    public static void ClearAllCache()
    {
        lock (LockObject)
        {
            var keysToRemove = Cache.Keys.ToList();
            foreach (var key in keysToRemove)
            {
                RemoveFromCacheInternal(key);
            }

            Debug.WriteLine("[DIAGNOSE] All caches have been cleared.");
        }
    }

    /// <summary>
    /// 根据指定的缓存键，从缓存中移除一个项。
    /// </summary>
    /// <param name="cacheKey">要移除的项的键。</param>
    public static void RemoveFromCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey)) return;

        lock (LockObject)
        {
            RemoveFromCacheInternal(cacheKey);
        }
    }

    /// <summary>
    /// 根据指定的前缀，从缓存中移除所有匹配的项。
    /// </summary>
    /// <param name="prefix">要匹配的缓存键前缀。</param>
    public static void RemoveCacheByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;

        lock (LockObject)
        {
            // 使用 ToList 创建一个副本，以避免在迭代时修改集合
            var keysToRemove = Cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                RemoveFromCacheInternal(key);
            }

            Debug.WriteLine($"[DIAGNOSE] Removed {keysToRemove.Count} cache items with prefix '{prefix}'.");
        }
    }

    /// <summary>
    /// 内部方法，用于从缓存中移除项并释放资源。
    /// 此方法不包含锁，必须在调用方确保线程安全。
    /// </summary>
    /// <param name="cacheKey">要移除的项的键。</param>
    private static void RemoveFromCacheInternal(string cacheKey)
    {
        if (!Cache.TryGetValue(cacheKey, out var node)) return;

        LruList.Remove(node);
        Cache.Remove(cacheKey);
        
        // 从内存监控中移除图片大小
        var imageSize = MemoryPressureMonitor.EstimateImageSize(node.Value.Image);
        MemoryPressureMonitor.UnregisterImageSize(imageSize);
        
        try
        {
            node.Value.Image.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] Failed to dispose image for key {cacheKey}: {ex.Message}");
        }
    }
}