using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserRunReadService : IParserRunReadService
{
    private readonly ApplicationDbContext _dbContext;

    public ParserRunReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ParserRunSummaryDto>>> GetListAsync(
        ParserRunListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = ApplyFilters(_dbContext.ParserRuns.AsNoTracking(), query);
        rows = ApplySort(rows, query.Sort);

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(MapToSummary).ToList();

        return ServiceResult<PagedResponse<ParserRunSummaryDto>>.Success(
            new PagedResponse<ParserRunSummaryDto>(items, page, pageSize, totalCount));
    }

    private static IQueryable<ParserRun> ApplyFilters(
        IQueryable<ParserRun> rows,
        ParserRunListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Kind))
            rows = rows.Where(x => x.Kind == query.Kind.Trim());

        if (!string.IsNullOrWhiteSpace(query.ManifestStatus))
            rows = rows.Where(x => x.ManifestStatus == query.ManifestStatus.Trim());

        if (!string.IsNullOrWhiteSpace(query.ParserRunId))
            rows = rows.Where(x => x.ParserRunId == query.ParserRunId.Trim());

        return rows;
    }

    private static IOrderedQueryable<ParserRun> ApplySort(
        IQueryable<ParserRun> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "startedAtUtc" => rows.OrderBy(x => x.StartedAtUtc).ThenBy(x => x.Id),
            "-startedAtUtc" => rows.OrderByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.Id),
            "finishedAtUtc" => rows.OrderBy(x => x.FinishedAtUtc).ThenBy(x => x.Id),
            "-finishedAtUtc" => rows.OrderByDescending(x => x.FinishedAtUtc).ThenByDescending(x => x.Id),
            "dateRegisteredUtc" => rows.OrderBy(x => x.DateRegisteredUtc).ThenBy(x => x.Id),
            _ => rows.OrderByDescending(x => x.DateRegisteredUtc).ThenByDescending(x => x.Id)
        };
    }

    private static ParserRunSummaryDto MapToSummary(ParserRun row)
    {
        return new ParserRunSummaryDto(
            row.Id,
            row.ParserRunId,
            row.Kind,
            row.Marketplace,
            row.ManifestStatus,
            row.ParserVersion,
            row.StartedAtUtc,
            row.FinishedAtUtc,
            row.DateRegisteredUtc,
            Clone(row.RequestedScope),
            Clone(row.Counters));
    }

    private static JsonElement? Clone(JsonDocument? json)
    {
        return json is null
            ? null
            : json.RootElement.Clone();
    }
}
