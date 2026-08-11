using System.Text.Json;
using JetlagBot.App.Configuration;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Services;

/// <summary>
/// In-memory store catalog for Discord autocomplete.
/// Discord requires autocomplete responses within 3 seconds, so we never wait on slow
/// upstream paths during keystrokes — we filter a cached list and refresh in the background.
/// </summary>
public interface IBonusStoreCatalogCache
{
    /// <summary>Filter the cached catalog by name (instant, no network).</summary>
    IReadOnlyList<BonusStoreOption> Search(string? query, int take = 25);

    /// <summary>
    /// Refresh from Bonus Tracker if the cache is empty or older than <paramref name="maxAge"/>.
    /// Honors <paramref name="timeout"/> so callers can stay under Discord's 3s limit.
    /// </summary>
    Task RefreshIfNeededAsync(TimeSpan maxAge, TimeSpan timeout, CancellationToken cancellationToken = default);

    bool HasData { get; }
}

public sealed class BonusStoreCatalogCache(
    IHttpClientFactory httpClientFactory,
    IOptions<BonusAlertOptions> options,
    ILogger<BonusStoreCatalogCache> logger) : IBonusStoreCatalogCache
{
    private readonly object _gate = new();
    private IReadOnlyList<BonusStoreOption> _stores = [];
    private DateTimeOffset _fetchedAtUtc = DateTimeOffset.MinValue;
    private int _refreshing;

    public bool HasData
    {
        get
        {
            lock (_gate)
            {
                return _stores.Count > 0;
            }
        }
    }

    public IReadOnlyList<BonusStoreOption> Search(string? query, int take = 25)
    {
        take = Math.Clamp(take, 1, 25);
        IReadOnlyList<BonusStoreOption> snapshot;
        lock (_gate)
        {
            snapshot = _stores;
        }

        if (snapshot.Count == 0)
        {
            return [];
        }

        var q = query?.Trim() ?? string.Empty;
        IEnumerable<BonusStoreOption> filtered = snapshot;
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = snapshot.Where(store =>
                store.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || store.StoreKey.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderBy(store => store.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(take)
            .ToArray();
    }

    public async Task RefreshIfNeededAsync(
        TimeSpan maxAge,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset fetchedAt;
        int count;
        lock (_gate)
        {
            fetchedAt = _fetchedAtUtc;
            count = _stores.Count;
        }

        var age = DateTimeOffset.UtcNow - fetchedAt;
        if (count > 0 && age < maxAge)
        {
            return;
        }

        // Only one refresh at a time.
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            var stores = await FetchCatalogAsync(timeoutCts.Token).ConfigureAwait(false);
            if (stores.Count == 0)
            {
                logger.LogWarning("Bonus store catalog refresh returned 0 stores; keeping previous cache.");
                return;
            }

            lock (_gate)
            {
                _stores = stores;
                _fetchedAtUtc = DateTimeOffset.UtcNow;
            }

            logger.LogInformation("Bonus store catalog refreshed with {Count} stores.", stores.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Bonus store catalog refresh timed out after {TimeoutMs}ms (upstream may be returning 502).",
                timeout.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Bonus store catalog refresh failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private async Task<IReadOnlyList<BonusStoreOption>> FetchCatalogAsync(CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.BonusTrackerBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("BonusAlert:BonusTrackerBaseUrl is not set; cannot refresh store catalog.");
            return [];
        }

        var client = httpClientFactory.CreateClient(nameof(BonusAlertService));
        foreach (var (path, requiresApiKey) in ResolveCatalogPaths(baseUrl))
        {
            if (requiresApiKey && string.IsNullOrWhiteSpace(options.Value.ApiKey))
            {
                continue;
            }

            var url = $"{baseUrl}{path}";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (requiresApiKey)
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", options.Value.ApiKey.Trim());
                }

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug(
                        "Catalog fetch {Url} returned {StatusCode}.",
                        url,
                        (int)response.StatusCode);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body)
                    || body.TrimStart().StartsWith('<')
                    || !body.TrimStart().StartsWith('{'))
                {
                    logger.LogDebug("Catalog fetch {Url} returned non-JSON body.", url);
                    continue;
                }

                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("items", out var items)
                    && !document.RootElement.TryGetProperty("Items", out items))
                {
                    continue;
                }

                var parsed = ParseStoreOptions(items);
                if (parsed.Count > 0)
                {
                    return parsed
                        .GroupBy(store => store.StoreKey, StringComparer.Ordinal)
                        .Select(group => group.First())
                        .OrderBy(store => store.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Catalog fetch failed for {Url}.", url);
            }
        }

        return [];
    }

    private IEnumerable<(string Path, bool RequiresApiKey)> ResolveCatalogPaths(string baseUrl)
    {
        var configured = options.Value.BonusTrackerStoresPath?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = configured.StartsWith('/') ? configured : "/" + configured;
            var requiresKey = path.Contains("/internal/jetlag", StringComparison.OrdinalIgnoreCase);
            // Catalog loads the full list; append take for unified endpoints.
            if (path.Contains("unified", StringComparison.OrdinalIgnoreCase))
            {
                path = AppendQuery(path, "take=200&activeOnly=false");
            }

            yield return (path, requiresKey);
            yield break;
        }

        if (LooksLikePublicFrontendBaseUrl(baseUrl))
        {
            // One path only — public nginx proxies /api/bff/* to the BFF.
            yield return ("/api/bff/stores/unified?take=200&activeOnly=false", false);
            yield break;
        }

        // Direct BFF.
        yield return ("/api/internal/jetlag/stores", true);
        yield return ("/api/stores/unified?take=200&activeOnly=false", false);
    }

    private static string AppendQuery(string path, string query)
    {
        return path.Contains('?', StringComparison.Ordinal) ? $"{path}&{query}" : $"{path}?{query}";
    }

    private static bool LooksLikePublicFrontendBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               && !uri.IsLoopback
               && uri.Port is 443 or 80;
    }

    private static List<BonusStoreOption> ParseStoreOptions(JsonElement items)
    {
        var results = new List<BonusStoreOption>();
        foreach (var item in items.EnumerateArray())
        {
            var storeKey = ReadString(item, "storeKey") ?? ReadString(item, "StoreKey");
            var simpleName = ReadString(item, "displayName") ?? ReadString(item, "DisplayName");
            if (!string.IsNullOrWhiteSpace(storeKey) && !string.IsNullOrWhiteSpace(simpleName))
            {
                results.Add(new BonusStoreOption { StoreKey = storeKey, DisplayName = simpleName });
                continue;
            }

            var displayName = simpleName
                ?? FirstNestedStoreName(item)
                ?? "Ukjent butikk";

            var mappingId = ReadGuid(item, "storeMappingId") ?? ReadGuid(item, "StoreMappingId");
            if (mappingId is Guid mapped)
            {
                results.Add(new BonusStoreOption
                {
                    StoreKey = mapped.ToString("D"),
                    DisplayName = displayName,
                });
                continue;
            }

            foreach (var storeId in NestedStoreIds(item))
            {
                results.Add(new BonusStoreOption
                {
                    StoreKey = storeId.ToString("D"),
                    DisplayName = displayName,
                });
            }
        }

        return results;
    }

    private static IEnumerable<Guid> NestedStoreIds(JsonElement item)
    {
        foreach (var propertyName in new[]
                 {
                     "trumfStores", "TrumfStores",
                     "sasStores", "SasStores",
                     "trumfFordelStores", "TrumfFordelStores",
                 })
        {
            if (!item.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var store in array.EnumerateArray())
            {
                var id = ReadGuid(store, "id") ?? ReadGuid(store, "Id");
                if (id is Guid storeId)
                {
                    yield return storeId;
                }
            }
        }

        foreach (var propertyName in new[] { "trumf", "Trumf", "sas", "Sas", "trumfFordel", "TrumfFordel" })
        {
            if (!item.TryGetProperty(propertyName, out var store) || store.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadGuid(store, "id") ?? ReadGuid(store, "Id");
            if (id is Guid storeId)
            {
                yield return storeId;
            }
        }
    }

    private static string? FirstNestedStoreName(JsonElement item)
    {
        foreach (var propertyName in new[]
                 {
                     "trumfStores", "TrumfStores",
                     "sasStores", "SasStores",
                     "trumfFordelStores", "TrumfFordelStores",
                 })
        {
            if (!item.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var store in array.EnumerateArray())
            {
                var name = ReadString(store, "name") ?? ReadString(store, "Name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }

        foreach (var propertyName in new[] { "trumf", "Trumf", "sas", "Sas", "trumfFordel", "TrumfFordel" })
        {
            if (!item.TryGetProperty(propertyName, out var store) || store.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(store, "name") ?? ReadString(store, "Name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String
            && Guid.TryParse(property.GetString(), out var guid))
        {
            return guid;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
