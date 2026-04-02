using Avalonia.Media.Imaging;
using SSMM_UI.Enums;
using SSMM_UI.Helpers;
using SSMM_UI.Interfaces;
using SSMM_UI.MetaData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class TwitchCategoryCacheService : ITwitchCategoryCacheService
{
    private readonly ILogService _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);
    private static readonly HttpClient HttpClient = new();

    private readonly string _cacheRootDir;
    private readonly string _queryCachePath;
    private readonly string _imagesDir;

    public TwitchCategoryCacheService(ILogService logger)
    {
        _logger = logger;
        _cacheRootDir = StorageHelper.GetOrCreateDirectory(StorageScope.Roaming, @"Metadata\TwitchCache");
        _imagesDir = Path.Combine(_cacheRootDir, "Images");
        Directory.CreateDirectory(_imagesDir);
        _queryCachePath = Path.Combine(_cacheRootDir, "query-cache.json");
    }

    public async Task<(IReadOnlyList<TwitchCategory> Results, bool FromCache)> SearchAsync(
        string query,
        string accessToken,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (Array.Empty<TwitchCategory>(), false);
        }

        var store = await LoadStoreAsync(cancellationToken);
        if (TryGetCachedQuery(store, normalized, out var cached))
        {
            var hydrated = await HydrateCategoriesAsync(cached, cancellationToken);
            return (hydrated, true);
        }

        var apiResults = await FetchFromApiAsync(normalized, accessToken, clientId, cancellationToken);
        foreach (var item in apiResults)
        {
            item.BoxArt = await GetOrFetchBoxArtAsync(item.Id, item.BoxArtUrl, cancellationToken);
        }

        store.Queries[normalized] = new QueryCacheEntry
        {
            CachedAtUtc = DateTime.UtcNow,
            Results = apiResults.Select(x => new CachedCategory
            {
                Id = x.Id,
                Name = x.Name,
                BoxArtUrl = x.BoxArtUrl
            }).ToList()
        };

        await SaveStoreAsync(store, cancellationToken);
        return (apiResults, false);
    }

    public async Task<Bitmap?> GetOrFetchBoxArtAsync(
        string? categoryId,
        string? boxArtUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(boxArtUrl))
        {
            return null;
        }

        var resolvedUrl = ResolveBoxArtUrl(boxArtUrl);
        var imagePath = GetImagePath(categoryId, resolvedUrl);
        if (File.Exists(imagePath))
        {
            try
            {
                return await LoadBitmapFromFileAsync(imagePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to load cached Twitch box art: {ex.Message}");
            }
        }

        try
        {
            await using var stream = await HttpClient.GetStreamAsync(resolvedUrl, cancellationToken);
            await using var file = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file, cancellationToken);
            return await LoadBitmapFromFileAsync(imagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to fetch Twitch box art: {ex.Message}");
            return null;
        }
    }

    private async Task<CacheStore> LoadStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_queryCachePath))
            {
                return new CacheStore();
            }

            var json = await File.ReadAllTextAsync(_queryCachePath, cancellationToken);
            var data = JsonSerializer.Deserialize<CacheStore>(json, _jsonOptions);
            return data ?? new CacheStore();
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to load Twitch query cache: {ex.Message}");
            return new CacheStore();
        }
    }

    private async Task SaveStoreAsync(CacheStore store, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(store, _jsonOptions);
            await File.WriteAllTextAsync(_queryCachePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to save Twitch query cache: {ex.Message}");
        }
    }

    private static bool TryGetCachedQuery(CacheStore store, string query, out IReadOnlyList<CachedCategory> results)
    {
        results = Array.Empty<CachedCategory>();
        if (!store.Queries.TryGetValue(query, out var entry) || entry?.Results is null)
        {
            return false;
        }

        if (DateTime.UtcNow - entry.CachedAtUtc > CacheTtl)
        {
            return false;
        }

        results = entry.Results;
        return true;
    }

    private async Task<List<TwitchCategory>> HydrateCategoriesAsync(IReadOnlyList<CachedCategory> cached, CancellationToken cancellationToken)
    {
        var output = new List<TwitchCategory>(cached.Count);
        foreach (var item in cached)
        {
            var category = new TwitchCategory
            {
                Id = item.Id,
                Name = item.Name,
                BoxArtUrl = item.BoxArtUrl
            };

            category.BoxArt = await GetOrFetchBoxArtAsync(category.Id, category.BoxArtUrl, cancellationToken);
            output.Add(category);
        }

        return output;
    }

    private async Task<List<TwitchCategory>> FetchFromApiAsync(string query, string accessToken, string clientId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.twitch.tv/helix/search/categories?query={Uri.EscapeDataString(query)}&first=20");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", clientId);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = new List<TwitchCategory>();
        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            foreach (var item in data.EnumerateArray())
            {
                results.Add(new TwitchCategory
                {
                    Id = item.GetProperty("id").GetString(),
                    Name = item.GetProperty("name").GetString(),
                    BoxArtUrl = ResolveBoxArtUrl(item.GetProperty("box_art_url").GetString())
                });
            }
        }

        return results;
    }

    private static string NormalizeQuery(string query) => query.Trim().ToLowerInvariant();

    private static string ResolveBoxArtUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return string.Empty;
        }

        return rawUrl
            .Replace("{width}", "52", StringComparison.OrdinalIgnoreCase)
            .Replace("{height}", "72", StringComparison.OrdinalIgnoreCase);
    }

    private string GetImagePath(string? categoryId, string boxArtUrl)
    {
        var safeId = string.IsNullOrWhiteSpace(categoryId) ? ComputeSha1(boxArtUrl) : ComputeSha1(categoryId);
        return Path.Combine(_imagesDir, $"{safeId}.img");
    }

    private static async Task<Bitmap?> LoadBitmapFromFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return new Bitmap(memory);
    }

    private static string ComputeSha1(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class CacheStore
    {
        [JsonPropertyName("queries")]
        public Dictionary<string, QueryCacheEntry> Queries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class QueryCacheEntry
    {
        [JsonPropertyName("cachedAtUtc")]
        public DateTime CachedAtUtc { get; set; }

        [JsonPropertyName("results")]
        public List<CachedCategory> Results { get; set; } = [];
    }

    private sealed class CachedCategory
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("boxArtUrl")]
        public string? BoxArtUrl { get; set; }
    }
}
