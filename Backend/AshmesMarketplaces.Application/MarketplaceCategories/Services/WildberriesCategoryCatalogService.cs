using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketplaceCategories.Services;

public sealed class WildberriesCategoryCatalogService :
    IWildberriesCategoryCatalogService,
    IWildberriesCategoryCatalogRefreshService
{
    private static readonly Uri CatalogUri = new("https://static-basket-01.wbbasket.ru/vol0/data/main-menu-ru-ru-v3.json");
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public WildberriesCategoryCatalogService(ApplicationDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/147.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.wildberries.ru/");
        }
    }

    public async Task<ServiceResult<WildberriesCategoryCatalogDto>> GetTreeAsync(CancellationToken cancellationToken)
    {
        var leavesResult = await GetLeavesAsync(cancellationToken);
        if (!leavesResult.IsSuccess)
            return ServiceResult<WildberriesCategoryCatalogDto>.Unavailable(leavesResult.Error!.Message);

        var fetchedAtUtc = leavesResult.Value!.Count == 0
            ? DateTime.UtcNow
            : leavesResult.Value.Max(x => x.Id) > 0
                ? await _dbContext.WildberriesCategoryLeaves
                    .AsNoTracking()
                    .MaxAsync(x => x.FetchedAtUtc, cancellationToken)
                : DateTime.UtcNow;

        return ServiceResult<WildberriesCategoryCatalogDto>.Success(
            new WildberriesCategoryCatalogDto("wildberries", fetchedAtUtc, leavesResult.Value));
    }

    public async Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> GetLeavesAsync(
        CancellationToken cancellationToken)
    {
        var leaves = await LoadLeavesFromDatabaseAsync(cancellationToken);
        if (leaves.Count == 0)
        {
            var refresh = await RefreshAsync(cancellationToken);
            if (!refresh.IsSuccess)
                return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Unavailable(refresh.Error!.Message);

            leaves = await LoadLeavesFromDatabaseAsync(cancellationToken);
        }

        return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(leaves);
    }

    public async Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> SearchAsync(
        WildberriesCategorySearchQuery query,
        CancellationToken cancellationToken)
    {
        var searchText = (query.Query ?? string.Empty).Trim();
        if (searchText.Length < 2)
            return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.BadRequest("Search query must contain at least 2 characters.");

        if (!await _dbContext.WildberriesCategoryLeaves.AsNoTracking().AnyAsync(cancellationToken))
        {
            var refresh = await RefreshAsync(cancellationToken);
            if (!refresh.IsSuccess)
                return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Unavailable(refresh.Error!.Message);
        }

        var normalized = searchText.ToLowerInvariant();
        var leaves = await _dbContext.WildberriesCategoryLeaves
            .AsNoTracking()
            .OrderBy(x => x.SourcePath)
            .ToListAsync(cancellationToken);

        var items = leaves
            .Select(Map)
            .Where(x =>
                x.Name.ToLowerInvariant().Contains(normalized)
                || x.Path.ToLowerInvariant().Contains(normalized)
                || (x.SearchQuery?.ToLowerInvariant().Contains(normalized) ?? false))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();

        return ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>.Success(items);
    }

    public async Task<ServiceResult<WildberriesCategoryCatalogRefreshResult>> RefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            using var response = await _httpClient.GetAsync(CatalogUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<WildberriesCategoryCatalogRefreshResult>.Unavailable(
                    $"Wildberries category catalog is unavailable: HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return ServiceResult<WildberriesCategoryCatalogRefreshResult>.Unavailable("Wildberries category catalog has unexpected format.");

            var nodes = WildberriesCategoryTreeParser.Parse(document.RootElement);
            var leaves = nodes.Where(x => x.IsLeaf).ToArray();
            if (leaves.Length == 0)
                return ServiceResult<WildberriesCategoryCatalogRefreshResult>.Unavailable("Wildberries category catalog does not contain leaf categories.");

            await UpsertLeavesAsync(leaves, now, cancellationToken);
            return ServiceResult<WildberriesCategoryCatalogRefreshResult>.Success(
                new WildberriesCategoryCatalogRefreshResult(leaves.Length, now));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ServiceResult<WildberriesCategoryCatalogRefreshResult>.Unavailable(
                $"Wildberries category catalog cannot be loaded: {exception.Message}");
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<WildberriesCategoryNodeDto>> LoadLeavesFromDatabaseAsync(
        CancellationToken cancellationToken)
    {
        var leaves = await _dbContext.WildberriesCategoryLeaves
            .AsNoTracking()
            .OrderBy(x => x.SourcePath)
            .ToListAsync(cancellationToken);

        return leaves.Select(Map).ToArray();
    }

    private async Task UpsertLeavesAsync(
        IReadOnlyList<WildberriesCategoryNodeDto> leaves,
        DateTime fetchedAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WildberriesCategoryLeaves
            .ToDictionaryAsync(x => x.WbCategoryId, cancellationToken);
        var actualIds = leaves.Select(x => x.Id).ToHashSet();

        foreach (var leaf in leaves)
        {
            if (existing.TryGetValue(leaf.Id, out var stored))
            {
                stored.UpdateFromCatalog(
                    leaf.Name,
                    leaf.SourceCategory,
                    leaf.SourceSubcategory,
                    leaf.Path,
                    leaf.SearchQuery,
                    leaf.ParentId,
                    leaf.Level,
                    fetchedAtUtc,
                    fetchedAtUtc);
                continue;
            }

            _dbContext.WildberriesCategoryLeaves.Add(new WildberriesCategoryLeaf(
                leaf.Id,
                leaf.Name,
                leaf.SourceCategory,
                leaf.SourceSubcategory,
                leaf.Path,
                leaf.SearchQuery,
                leaf.ParentId,
                leaf.Level,
                fetchedAtUtc,
                fetchedAtUtc));
        }

        var removed = existing.Values
            .Where(x => !actualIds.Contains(x.WbCategoryId))
            .ToArray();
        _dbContext.WildberriesCategoryLeaves.RemoveRange(removed);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static WildberriesCategoryNodeDto Map(WildberriesCategoryLeaf leaf) =>
        new(
            leaf.WbCategoryId,
            leaf.Name,
            leaf.SourceCategory,
            leaf.SourceSubcategory,
            leaf.SourcePath,
            leaf.SearchQuery,
            leaf.ParentId,
            leaf.IsLeaf,
            leaf.Level);
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
