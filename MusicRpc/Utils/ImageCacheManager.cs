using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

    // 记录当前正在 UI 中显示的图片键，防止被清理释放
    private static readonly HashSet<string> ActiveKeys = [];

    // 记录已记录过日志的缓存键，避免重复刷屏
    private static readonly HashSet<string> LoggedCacheKeys = [];

    /// <summary>
    /// 缓存项的内部表示。
    /// </summary>
    private class CacheItem(string key, Image image)
    {
        public string Key { get; } = key;
        public Image Image { get; } = image;
        public DateTime LastAccessed { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 设置当前活跃的图片键列表，这些图片将不会被清理释放
    /// </summary>
    public static void SetActiveKeys(IEnumerable<string> keys)
    {
        lock (LockObject)
        {
            ActiveKeys.Clear();
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    ActiveKeys.Add(key);
                }
            }
        }
    }

    /// <summary>
    /// 积极的内存清理，更频繁地检查和释放内存
    /// </summary>
    public static void PerformAggressiveCleanup()
    {
        lock (LockObject)
        {
            var currentMemory = MemoryPressureMonitor.GetCurrentMemoryUsage();
            List<string> keysToRemove;

            // 如果内存使用超过30MB，执行激进清理
            if (currentMemory > 30 * 1024 * 1024)
            {
                var targetCount = Math.Max(5, Cache.Count / 2); // 保留一半或至少5个
                keysToRemove = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed)
                    .Take(Cache.Count - targetCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    RemoveFromCacheInternal(key);
                }

                Logger.Diagnose(
                    $"Aggressive cleanup removed {keysToRemove.Count} items. Memory before: {currentMemory / 1024 / 1024:F1}MB, after: {MemoryPressureMonitor.GetCurrentMemoryUsage() / 1024 / 1024:F1}MB");
            }

            // 如果缓存项超过15个，清理最旧的项
            if (Cache.Count <= 15) return;
            keysToRemove = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed)
                .Take(Cache.Count - 15)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                RemoveFromCacheInternal(key);
            }

            Logger.Diagnose($"Cache size cleanup removed {keysToRemove.Count} items to maintain 15 items max");
        }
    }

    /// <summary>
    /// 定期清理不再使用的图像资源
    /// </summary>
    public static void PerformCleanup()
    {
        lock (LockObject)
        {
            var now = DateTime.Now;
            // 清理超过5分钟未访问的缓存项
            var keysToRemove =
                (from kvp in Cache where (now - kvp.Value.Value.LastAccessed) > TimeSpan.FromMinutes(5) select kvp.Key)
                .ToList();

            // 清理超过最大缓存大小70%的旧项
            if (Cache.Count > MaxCacheSize * 0.7)
            {
                var targetCount = (int)(MaxCacheSize * 0.5); // 目标是保留50%的缓存
                var excessCount = Cache.Count - targetCount;
                var sortedByTime = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed).Take(excessCount);
                keysToRemove.AddRange(sortedByTime.Select(kvp => kvp.Key));
            }

            // 如果内存使用超过50MB，额外清理最旧的缓存项
            if (MemoryPressureMonitor.GetCurrentMemoryUsage() > 50 * 1024 * 1024)
            {
                var itemsToClean = Math.Min(5, Cache.Count); // 最多清理5个最旧的项
                var oldestItems = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed).Take(itemsToClean);
                keysToRemove.AddRange(oldestItems.Select(kvp => kvp.Key));
            }

            foreach (var key in keysToRemove.Distinct())
            {
                RemoveFromCacheInternal(key);
            }

            if (keysToRemove.Count > 0)
            {
                Logger.Diagnose($"Cleanup removed {keysToRemove.Count} expired cache items.");
            }
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
            }
        }

        // 检查是否已经记录过该key的日志
        var shouldLog = false;
        lock (LoggedCacheKeys)
        {
            if (!LoggedCacheKeys.Contains(cacheKey))
            {
                shouldLog = true;
                LoggedCacheKeys.Add(cacheKey);
            }
        }

        if (shouldLog)
        {
            Logger.Diagnose($"ImageCacheManager received request for key: {cacheKey}");
        }

        if (cachedImage != null)
        {
            if (shouldLog)
            {
                Logger.Diagnose($"CACHE HIT for key: {cacheKey}");
            }

            return cachedImage;
        }

        Logger.Diagnose($"CACHE MISS for key: {cacheKey}. Downloading from URL.");

        const int maxRetries = 3;
        byte[]? downloadedBytes = null;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            var retryDelay = 100 * (1 << retry);

            try
            {
                var httpClient = HttpClientManager.SharedClient;
                using var response = await httpClient.GetAsync(imageUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Diagnose(
                        $"Image download failed for key {cacheKey}: HTTP Status {response.StatusCode} (Retry {retry + 1}/{maxRetries})");
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/"))
                {
                    Logger.Diagnose(
                        $"Downloaded content is not an image for key {cacheKey}. Content-Type: {contentType ?? "N/A"} (Retry {retry + 1}/{maxRetries})");
                    return null; // 内容类型明确错误，不重试
                }

                // 使用ArrayPool优化内存分配
                await using (var httpStream = await response.Content.ReadAsStreamAsync())
                {
                    // 预估图片大小
                    var contentLength = response.Content.Headers.ContentLength ?? 1024 * 1024; // 默认1MB
                    var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(contentLength, 10 * 1024 * 1024)); // 最大10MB

                    try
                    {
                        var totalRead = 0;
                        int bytesRead;

                        while ((bytesRead =
                                   await httpStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead))) >
                               0)
                        {
                            totalRead += bytesRead;
                            if (totalRead != buffer.Length) continue;
                            // 缓冲区不够，需要重新分配
                            var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                            Array.Copy(buffer, 0, newBuffer, 0, totalRead);
                            ArrayPool<byte>.Shared.Return(buffer);
                            buffer = newBuffer;
                        }

                        downloadedBytes = new byte[totalRead];
                        Array.Copy(buffer, 0, downloadedBytes, 0, totalRead);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                if (downloadedBytes is not { Length: > 0 })
                {
                    Logger.Diagnose(
                        $"Downloaded content is empty or invalid for key: {cacheKey}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                // 验证图像数据有效性
                if (!IsValidImageData(downloadedBytes))
                {
                    // 记录文件头字节以便调试
                    var headerHex = downloadedBytes.Length >= 16
                        ? BitConverter.ToString(downloadedBytes, 0, 16).Replace("-", " ")
                        : BitConverter.ToString(downloadedBytes).Replace("-", " ");

                    Logger.Diagnose(
                        $"Downloaded content is not a valid image format for key: {cacheKey}. Data length: {downloadedBytes.Length}. Header: {headerHex}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                Image image;
                try
                {
                    var ms = new MemoryStream(downloadedBytes);
                    image = Image.FromStream(ms);
                    Logger.Diagnose($"Downloaded and created new image for key: {cacheKey}. Caching it.");
                }
                catch (ArgumentException ex)
                {
                    Logger.Diagnose(
                        $"ArgumentException during Image.FromStream for key {cacheKey}. Data length: {downloadedBytes.Length}. Error: {ex.Message}. (Retry {retry + 1}/{maxRetries})");

                    if (retry >= maxRetries - 1) return null;
                    await Task.Delay(retryDelay);
                    continue;
                }

                // 估算新图片的内存大小
                var imageSize = MemoryPressureMonitor.EstimateImageSize(image);

                lock (LockObject)
                {
                    // 主动清理：检查是否需要预先清理一些缓存项
                    if (MemoryPressureMonitor.ShouldProactivelyCleanup())
                    {
                        var cleanupCount = MemoryPressureMonitor.GetSuggestedCleanupCount();
                        var itemsToCleanup = Math.Min(cleanupCount, LruList.Count);

                        for (var i = 0; i < itemsToCleanup && LruList.Last != null; i++)
                        {
                            var lruNode = LruList.Last;
                            var evictedItem = lruNode.Value;
                            var evictedSize = MemoryPressureMonitor.EstimateImageSize(evictedItem.Image);

                            RemoveFromCacheInternal(evictedItem.Key);
                            MemoryPressureMonitor.UnregisterImageSize(evictedSize);

                            Logger.Diagnose(
                                $"Proactive cleanup evicted item with key: {evictedItem.Key}, freed: {evictedSize} bytes");
                        }
                    }

                    // 内存压力清理：确保有足够空间存放新图片
                    while (MemoryPressureMonitor.ShouldEvictItems(imageSize) && LruList.Last != null)
                    {
                        var lruNode = LruList.Last;
                        var evictedItem = lruNode.Value;
                        var evictedSize = MemoryPressureMonitor.EstimateImageSize(evictedItem.Image);

                        RemoveFromCacheInternal(evictedItem.Key);
                        MemoryPressureMonitor.UnregisterImageSize(evictedSize);

                        Logger.Diagnose(
                            $"Memory pressure evicted item with key: {evictedItem.Key}, freed: {evictedSize} bytes");
                    }

                    // 创建新的缓存项并添加到缓存
                    var newItem = new CacheItem(cacheKey, image);
                    var newNode = new LinkedListNode<CacheItem>(newItem);
                    LruList.AddFirst(newNode);
                    Cache[cacheKey] = newNode;

                    // 注册新图片的内存大小
                    MemoryPressureMonitor.RegisterImageSize(imageSize);

                    Logger.Diagnose(
                        $"Cached new image with key: {cacheKey}, size: {imageSize} bytes, total cache memory: {MemoryPressureMonitor.GetCurrentMemoryUsage()} bytes");
                }

                return image;
            }
            catch (ArgumentException ex)
            {
                var dataLength = downloadedBytes?.Length.ToString() ?? "N/A";
                Logger.Diagnose(
                    $"ArgumentException during image creation for key {cacheKey}. Image data length: {dataLength}. Error: {ex.Message}. Attempt {retry + 1}/{maxRetries}.");
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(retryDelay);
                }
            }
            catch (Exception ex)
            {
                var dataLength = downloadedBytes?.Length.ToString() ?? "N/A";
                Logger.Diagnose(
                    $"General Exception during image download/creation for key {cacheKey}. Image data length: {dataLength}. Error: {ex.Message}.");
                return null;
            }
        }

        Logger.Diagnose($"Failed to download and create image for key {cacheKey} after {maxRetries} attempts.");
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

            // 清理日志记录，允许下次重新记录
            lock (LoggedCacheKeys)
            {
                LoggedCacheKeys.Clear();
            }

            Logger.Diagnose("[DIAGNOSE] All caches have been cleared.");
        }
    }

    /// <summary>
    /// 强制清理缓存以释放内存，保留最近使用的指定数量的图片
    /// </summary>
    /// <param name="keepCount">要保留的最近图片数量，默认为10</param>
    public static void ForceCleanupCache(int keepCount = 10)
    {
        lock (LockObject)
        {
            if (Cache.Count <= keepCount) return;

            var itemsToRemove = Cache.Count - keepCount;
            var keysToRemove = new List<string>();

            // 获取最旧的缓存项（除了最近使用的keepCount个）
            var sortedByTime = Cache.OrderBy(kvp => kvp.Value.Value.LastAccessed).Take(itemsToRemove);
            keysToRemove.AddRange(sortedByTime.Select(kvp => kvp.Key));

            foreach (var key in keysToRemove)
            {
                RemoveFromCacheInternal(key);
            }

            Logger.Diagnose(
                $"Force cleanup removed {keysToRemove.Count} cache items, kept {keepCount} most recent items.");
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

            Logger.Diagnose($"Removed {keysToRemove.Count} cache items with prefix '{prefix}'.");
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

        // 检查是否是当前正在使用的图片，如果是则跳过清理，防止 UI 崩溃
        if (ActiveKeys.Contains(cacheKey))
        {
            return;
        }

        LruList.Remove(node);
        Cache.Remove(cacheKey);

        // 从已记录日志的集合中移除，以便下次重新加载时可以再次记录
        lock (LoggedCacheKeys)
        {
            LoggedCacheKeys.Remove(cacheKey);
        }

        // 从内存监控中移除图片大小
        var imageSize = MemoryPressureMonitor.EstimateImageSize(node.Value.Image);
        MemoryPressureMonitor.UnregisterImageSize(imageSize);

        try
        {
            node.Value.Image.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Diagnose($"[WARNING] Failed to dispose image for key {cacheKey}: {ex.Message}");
        }
    }
}