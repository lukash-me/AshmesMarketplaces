using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class PublicParserCurrentProductRefreshService : IPublicParserCurrentProductRefreshService
{
    private readonly ApplicationDbContext _dbContext;

    public PublicParserCurrentProductRefreshService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicParserCurrentProductRefreshResult>> RefreshAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "ParserCurrentProductRows";""",
            cancellationToken);

        var totalCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH latest_products AS (
                SELECT *
                FROM (
                    SELECT
                        p.*,
                        row_number() OVER (
                            PARTITION BY p.wb_product_id
                            ORDER BY p.parsed_at_utc DESC, p.id DESC
                        ) AS rn
                    FROM "ParserProductRows" p
                    INNER JOIN "ParserRuns" r ON r.id = p.id_parser_run
                    WHERE r.kind = 'products'
                      AND (r.manifest_status = 'succeeded' OR r.manifest_status = 'partial')
                      AND COALESCE(lower(r.requested_scope ->> 'is_test_run'), 'false') <> 'true'
                ) ranked_products
                WHERE rn = 1
            ),
            root_counts AS (
                SELECT wb_root_id, count(*) AS products_count
                FROM latest_products
                WHERE wb_root_id IS NOT NULL AND wb_root_id <> ''
                GROUP BY wb_root_id
            ),
            latest_rank_run AS (
                SELECT r.parser_run_id
                FROM "ParserRuns" r
                WHERE r.kind = 'ranks'
                  AND (r.manifest_status = 'succeeded' OR r.manifest_status = 'partial')
                  AND COALESCE(lower(r.requested_scope ->> 'is_test_run'), 'false') <> 'true'
                ORDER BY
                    (r.finished_at_utc IS NOT NULL) DESC,
                    r.finished_at_utc DESC NULLS LAST,
                    r.date_registered_utc DESC,
                    r.started_at_utc DESC
                LIMIT 1
            ),
            rank_rows AS (
                SELECT rr.*
                FROM "ParserRankSnapshotRows" rr
                WHERE rr.parser_run_id = (SELECT parser_run_id FROM latest_rank_run)
            ),
            product_rank AS (
                SELECT DISTINCT ON (wb_product_id)
                    wb_product_id,
                    absolute_position,
                    query,
                    observed_at_utc
                FROM rank_rows
                ORDER BY wb_product_id, absolute_position ASC, observed_at_utc DESC
            ),
            root_rank AS (
                SELECT DISTINCT ON (wb_root_id)
                    wb_root_id,
                    absolute_position,
                    query,
                    observed_at_utc
                FROM rank_rows
                WHERE wb_root_id IS NOT NULL AND wb_root_id <> ''
                ORDER BY wb_root_id, absolute_position ASC, observed_at_utc DESC
            ),
            coverage AS (
                SELECT
                    source_category,
                    source_subcategory,
                    source_region_dest,
                    max(absolute_position) AS observed_range_limit,
                    max(observed_at_utc) AS observed_at_utc,
                    min(query) AS query
                FROM rank_rows
                GROUP BY source_category, source_subcategory, source_region_dest
            )
            INSERT INTO "ParserCurrentProductRows"
                (
                    id,
                    product_row_id,
                    parser_run_id,
                    parsed_at_utc,
                    wb_product_id,
                    wb_root_id,
                    name,
                    source_category,
                    source_subcategory,
                    source_query,
                    source_region_dest,
                    brand_name,
                    seller_name,
                    price_regular,
                    price_discounted,
                    price_wb_wallet,
                    total_quantity,
                    review_rating,
                    feedback_count,
                    position_state,
                    position_absolute,
                    position_observed_range_limit,
                    position_query,
                    position_observed_at_utc,
                    updated_at_utc
                )
            SELECT
                gen_random_uuid(),
                p.id,
                p.parser_run_id,
                p.parsed_at_utc,
                p.wb_product_id,
                p.wb_root_id,
                p.name,
                p.source_category,
                p.source_subcategory,
                p.source_query,
                p.source_region_dest,
                p.brand_name,
                p.seller_name,
                p.price_regular,
                p.price_discounted,
                p.price_wb_wallet,
                p.total_quantity,
                p.review_rating,
                p.feedback_count,
                CASE
                    WHEN COALESCE(pr.absolute_position, rr.absolute_position) IS NOT NULL THEN 'observed'
                    WHEN c.observed_range_limit IS NOT NULL THEN 'beyondObservedRange'
                    ELSE 'unknown'
                END,
                COALESCE(pr.absolute_position, rr.absolute_position),
                CASE
                    WHEN COALESCE(pr.absolute_position, rr.absolute_position) IS NULL THEN c.observed_range_limit
                    ELSE NULL
                END,
                COALESCE(pr.query, rr.query, c.query),
                COALESCE(pr.observed_at_utc, rr.observed_at_utc, c.observed_at_utc),
                {nowUtc}
            FROM latest_products p
            LEFT JOIN product_rank pr ON pr.wb_product_id = p.wb_product_id
            LEFT JOIN root_counts rc ON rc.wb_root_id = p.wb_root_id
            LEFT JOIN root_rank rr ON rr.wb_root_id = p.wb_root_id AND rc.products_count = 1
            LEFT JOIN coverage c
                ON c.source_category IS NOT DISTINCT FROM p.source_category
               AND c.source_subcategory IS NOT DISTINCT FROM p.source_subcategory
               AND c.source_region_dest IS NOT DISTINCT FROM p.source_region_dest;
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (totalCount == 0)
            return ServiceResult<PublicParserCurrentProductRefreshResult>.NotFound(
                "Карточки parser-а для сопоставления текущих товаров не найдены.");

        return ServiceResult<PublicParserCurrentProductRefreshResult>.Success(
            new PublicParserCurrentProductRefreshResult(totalCount, nowUtc));
    }
}
