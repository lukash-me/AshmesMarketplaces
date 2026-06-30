using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;

namespace AshmesMarketplaces.Application.MarketplaceCategories.Services;

public sealed class WildberriesCategoryCatalogService : IWildberriesCategoryCatalogService
{
    private static readonly Uri CatalogUri = new("https://static-basket-01.wbbasket.ru/vol0/data/main-menu-ru-ru-v3.json");
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static WildberriesCategoryCatalogDto? CachedCatalog;
    private static DateTime CachedUntilUtc;
    private readonly HttpClient _httpClient;

    public WildberriesCategoryCatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/147.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.wildberries.ru/");
        }
    }

    public Task<ServiceResult<WildberriesCategoryCatalogDto>> GetTreeAsync(CancellationToken cancellationToken)
    {
        return LoadCatalogAsync(cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> GetLeavesAsync(
        CancellationToken cancellationToken)
    {
        var catalogResult = await LoadCatalogAsync(cancellationToken);
        if (!catalogResult.IsSuccess)
            return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Unavailable(catalogResult.Error!.Message);

        var leaves = catalogResult.Value!.Nodes
            .Where(x => x.IsLeaf)
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(leaves);
    }

    public async Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> SearchAsync(
        WildberriesCategorySearchQuery query,
        CancellationToken cancellationToken)
    {
        var searchText = (query.Query ?? string.Empty).Trim();
        if (searchText.Length < 2)
            return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.BadRequest("Search query must contain at least 2 characters.");

        var catalogResult = await LoadCatalogAsync(cancellationToken);
        if (!catalogResult.IsSuccess)
            return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Unavailable(catalogResult.Error!.Message);

        var normalized = searchText.ToLowerInvariant();
        var items = catalogResult.Value!.Nodes
            .Where(x => x.IsLeaf)
            .Where(x =>
                x.Name.ToLowerInvariant().Contains(normalized)
                || x.Path.ToLowerInvariant().Contains(normalized)
                || (x.SearchQuery?.ToLowerInvariant().Contains(normalized) ?? false))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();

        return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(items);
    }

    private async Task<ServiceResult<WildberriesCategoryCatalogDto>> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (CachedCatalog is not null && CachedUntilUtc > now)
            return ServiceResult<WildberriesCategoryCatalogDto>.Success(CachedCatalog);

        await CacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            if (CachedCatalog is not null && CachedUntilUtc > now)
                return ServiceResult<WildberriesCategoryCatalogDto>.Success(CachedCatalog);

            using var response = await _httpClient.GetAsync(CatalogUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<WildberriesCategoryCatalogDto>.Unavailable(
                    $"Wildberries category catalog is unavailable: HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return ServiceResult<WildberriesCategoryCatalogDto>.Unavailable("Wildberries category catalog has unexpected format.");

            var nodes = WildberriesCategoryTreeParser.Parse(document.RootElement);
            if (nodes.Count == 0)
                return ServiceResult<WildberriesCategoryCatalogDto>.Unavailable("Wildberries category catalog is empty.");

            CachedCatalog = new WildberriesCategoryCatalogDto("wildberries", now, nodes);
            CachedUntilUtc = now.Add(CacheDuration);
            return ServiceResult<WildberriesCategoryCatalogDto>.Success(CachedCatalog);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ServiceResult<WildberriesCategoryCatalogDto>.Unavailable(
                $"Wildberries category catalog cannot be loaded: {exception.Message}");
        }
        finally
        {
            CacheLock.Release();
        }
    }
}

public static class WildberriesCategoryTreeParser
{
    public static IReadOnlyList<WildberriesCategoryNodeDto> Parse(JsonElement root)
    {
        var nodes = new List<WildberriesCategoryNodeDto>();
        if (root.ValueKind != JsonValueKind.Array)
            return nodes;

        foreach (var item in root.EnumerateArray())
            Visit(item, parentId: null, path: [], sourceCategory: null, level: 0, nodes);

        return nodes;
    }

    private static void Visit(
        JsonElement element,
        long? parentId,
        IReadOnlyList<string> path,
        string? sourceCategory,
        int level,
        List<WildberriesCategoryNodeDto> nodes)
    {
        var id = ReadLong(element, "id");
        var name = ReadString(element, "name") ?? ReadString(element, "seo");
        if (id is null || string.IsNullOrWhiteSpace(name))
            return;

        var currentPath = path.Concat([name]).ToArray();
        var rootCategory = sourceCategory ?? name;
        var children = ReadChildren(element);
        var isLeaf = children.Count == 0;
        nodes.Add(new WildberriesCategoryNodeDto(
            id.Value,
            name,
            rootCategory,
            isLeaf ? name : string.Empty,
            string.Join(" / ", currentPath),
            ReadString(element, "searchQuery"),
            parentId,
            isLeaf,
            level));

        foreach (var child in children)
            Visit(child, id, currentPath, rootCategory, level + 1, nodes);
    }

    private static List<JsonElement> ReadChildren(JsonElement element)
    {
        if (!element.TryGetProperty("childs", out var children) || children.ValueKind != JsonValueKind.Array)
            return [];

        return children.EnumerateArray().ToList();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return property.GetString()?.Trim();
    }

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }
}
