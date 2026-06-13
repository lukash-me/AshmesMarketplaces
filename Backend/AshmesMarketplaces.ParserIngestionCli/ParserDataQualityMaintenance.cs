using System.Data;
using System.Data.Common;
using System.Text.Json;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

internal sealed class ParserDataQualityMaintenance
{
    private const string BackupSchema = "ParserQualityBackups";

    private readonly ApplicationDbContext _dbContext;

    public ParserDataQualityMaintenance(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ParserQualityMaintenanceResult> AuditAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await CreateQualityTempTablesAsync(connection, transaction, cancellationToken);

        var reportPath = Path.Combine(
            outputDirectory,
            $"parser-quality-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        await WriteAuditReportAsync(connection, transaction, reportPath, cancellationToken);

        await transaction.RollbackAsync(cancellationToken);

        var badProducts = await ReadReportCountAsync(reportPath, "badProductRows", cancellationToken);
        return new ParserQualityMaintenanceResult(
            "audit-quality",
            reportPath,
            null,
            badProducts,
            0,
            0,
            Array.Empty<string>());
    }

    public async Task<ParserQualityMaintenanceResult> PurgeAsync(
        string outputDirectory,
        bool confirm,
        CancellationToken cancellationToken)
    {
        if (!confirm)
        {
            throw new InvalidOperationException("purge-quality requires --confirm. Run audit-quality first to inspect candidates.");
        }

        Directory.CreateDirectory(outputDirectory);

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var auditPath = Path.Combine(outputDirectory, $"parser-quality-pre-purge-{timestamp}.json");
        var purgePath = Path.Combine(outputDirectory, $"parser-quality-purge-{timestamp}.json");

        var backupTables = new List<string>();
        var deletedRows = 0L;
        var badProducts = 0L;
        var deleteScopes = 0L;

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await CreateQualityTempTablesAsync(connection, transaction, cancellationToken);
        await WriteAuditReportAsync(connection, transaction, auditPath, cancellationToken);

        badProducts = await ExecuteScalarLongAsync(
            connection,
            transaction,
            """select count(*) from "_parser_quality_bad_product_rows";""",
            cancellationToken);
        deleteScopes = await ExecuteScalarLongAsync(
            connection,
            transaction,
            """select count(*) from "_parser_quality_delete_scopes";""",
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"""create schema if not exists "{BackupSchema}";""",
            cancellationToken);

        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "WorkspaceMarketProductUserReadStates",
            timestamp,
            """
            select r.*
            from "WorkspaceMarketProductUserReadStates" r
            join "WorkspaceMarketProducts" w on w.id = r.id_workspace_market_product
            join "_parser_quality_bad_product_rows" b on b.id = w.parser_product_row_id
            """,
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "WorkspaceMarketProductAnalyses",
            timestamp,
            """
            select a.*
            from "WorkspaceMarketProductAnalyses" a
            join "WorkspaceMarketProducts" w on w.id = a.id_workspace_market_product
            join "_parser_quality_bad_product_rows" b on b.id = w.parser_product_row_id
            """,
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "WorkspaceMarketProducts",
            timestamp,
            """
            select w.*
            from "WorkspaceMarketProducts" w
            join "_parser_quality_bad_product_rows" b on b.id = w.parser_product_row_id
            """,
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "MarketHotProductRecommendations",
            timestamp,
            """
            select r.*
            from "MarketHotProductRecommendations" r
            left join "_parser_quality_bad_product_rows" b on b.id = r.id_parser_product_row
            left join "_parser_quality_delete_scopes" s
              on s.wb_product_id = r.wb_product_id
             and coalesce(s.source_subcategory, '') = coalesce(r.source_subcategory, '')
            where b.id is not null or s.wb_product_id is not null
            """,
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "ParserProductRows",
            timestamp,
            """
            select p.*
            from "ParserProductRows" p
            join "_parser_quality_bad_product_rows" b on b.id = p.id
            """,
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "ParserRankSnapshotRows",
            timestamp,
            ScopeSelect("ParserRankSnapshotRows", "r"),
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "ParserLogisticsSnapshotRows",
            timestamp,
            ScopeSelect("ParserLogisticsSnapshotRows", "l"),
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "ParserWarehouseAvailabilityRows",
            timestamp,
            ScopeSelect("ParserWarehouseAvailabilityRows", "w"),
            cancellationToken));
        backupTables.Add(await BackupAsync(
            connection,
            transaction,
            "ParserProductDetailRows",
            timestamp,
            ScopeSelect("ParserProductDetailRows", "d"),
            cancellationToken));

        deletedRows += await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            delete from "WorkspaceMarketProductUserReadStates" r
            using "WorkspaceMarketProducts" w, "_parser_quality_bad_product_rows" b
            where w.id = r.id_workspace_market_product and b.id = w.parser_product_row_id;
            """,
            cancellationToken);
        deletedRows += await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            delete from "WorkspaceMarketProductAnalyses" a
            using "WorkspaceMarketProducts" w, "_parser_quality_bad_product_rows" b
            where w.id = a.id_workspace_market_product and b.id = w.parser_product_row_id;
            """,
            cancellationToken);
        deletedRows += await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            delete from "WorkspaceMarketProducts" w
            using "_parser_quality_bad_product_rows" b
            where b.id = w.parser_product_row_id;
            """,
            cancellationToken);
        deletedRows += await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            delete from "MarketHotProductRecommendations" r
            using "_parser_quality_bad_product_rows" b
            where b.id = r.id_parser_product_row;

            delete from "MarketHotProductRecommendations" r
            using "_parser_quality_delete_scopes" s
            where s.wb_product_id = r.wb_product_id
              and coalesce(s.source_subcategory, '') = coalesce(r.source_subcategory, '');
            """,
            cancellationToken);
        deletedRows += await DeleteByScopeAsync(connection, transaction, "ParserProductDetailRows", "d", cancellationToken);
        deletedRows += await DeleteByScopeAsync(connection, transaction, "ParserWarehouseAvailabilityRows", "w", cancellationToken);
        deletedRows += await DeleteByScopeAsync(connection, transaction, "ParserLogisticsSnapshotRows", "l", cancellationToken);
        deletedRows += await DeleteByScopeAsync(connection, transaction, "ParserRankSnapshotRows", "r", cancellationToken);
        deletedRows += await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            delete from "ParserProductRows" p
            using "_parser_quality_bad_product_rows" b
            where b.id = p.id;
            """,
            cancellationToken);

        await WritePurgeReportAsync(
            purgePath,
            auditPath,
            backupTables,
            badProducts,
            deleteScopes,
            deletedRows,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ParserQualityMaintenanceResult(
            "purge-quality",
            auditPath,
            purgePath,
            badProducts,
            deleteScopes,
            deletedRows,
            backupTables.ToArray());
    }

    private static string ScopeSelect(string tableName, string alias) =>
        $$"""
        select {{alias}}.*
        from "{{tableName}}" {{alias}}
        join "_parser_quality_delete_scopes" s
          on s.marketplace_key = case when lower(coalesce({{alias}}.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce({{alias}}.marketplace, '')) end
         and s.wb_product_id = {{alias}}.wb_product_id
         and s.source_subcategory_key = coalesce({{alias}}.source_subcategory, '')
         and s.source_region_dest_key = coalesce({{alias}}.source_region_dest, '')
        """;

    private static async Task<long> DeleteByScopeAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        string alias,
        CancellationToken cancellationToken) =>
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $$"""
            delete from "{{tableName}}" {{alias}}
            using "_parser_quality_delete_scopes" s
            where s.marketplace_key = case when lower(coalesce({{alias}}.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce({{alias}}.marketplace, '')) end
              and s.wb_product_id = {{alias}}.wb_product_id
              and s.source_subcategory_key = coalesce({{alias}}.source_subcategory, '')
              and s.source_region_dest_key = coalesce({{alias}}.source_region_dest, '');
            """,
            cancellationToken);

    private static async Task<string> BackupAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        string timestamp,
        string selectSql,
        CancellationToken cancellationToken)
    {
        var backupTable = $"{tableName}_{timestamp}";
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"""
            drop table if exists "{BackupSchema}"."{backupTable}";
            create table "{BackupSchema}"."{backupTable}" as
            {selectSql};
            """,
            cancellationToken);

        return $"{BackupSchema}.{backupTable}";
    }

    private static async Task CreateQualityTempTablesAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            drop table if exists "_parser_quality_good_logistics_scopes";
            drop table if exists "_parser_quality_detail_scopes";
            drop table if exists "_parser_quality_review_fetch_roots";
            drop table if exists "_parser_quality_bad_product_reasons";
            drop table if exists "_parser_quality_bad_product_rows";
            drop table if exists "_parser_quality_good_product_scopes";
            drop table if exists "_parser_quality_delete_scopes";

            create temp table "_parser_quality_good_logistics_scopes" on commit drop as
            select distinct
                marketplace,
                case when lower(coalesce(marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(marketplace, '')) end as marketplace_key,
                wb_product_id,
                source_subcategory,
                source_region_dest,
                coalesce(source_subcategory, '') as source_subcategory_key,
                coalesce(source_region_dest, '') as source_region_dest_key
            from "ParserLogisticsSnapshotRows"
            where wb_product_id is not null and wb_product_id <> '';

            create index on "_parser_quality_good_logistics_scopes" (marketplace_key, wb_product_id, source_subcategory_key, source_region_dest_key);

            create temp table "_parser_quality_detail_scopes" on commit drop as
            select
                marketplace,
                case when lower(coalesce(marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(marketplace, '')) end as marketplace_key,
                wb_product_id,
                source_subcategory,
                source_region_dest,
                coalesce(source_subcategory, '') as source_subcategory_key,
                coalesce(source_region_dest, '') as source_region_dest_key,
                true as has_detail_row,
                bool_or(
                    lower(coalesce(status, '')) in ('success', 'succeeded')
                    and nullif(trim(coalesce(description, '')), '') is not null
                ) as has_description,
                bool_or(
                    lower(coalesce(status, '')) in ('success', 'succeeded')
                    and (
                        (jsonb_typeof(characteristics) = 'array' and jsonb_array_length(characteristics) > 0)
                        or (jsonb_typeof(grouped_options) = 'array' and jsonb_array_length(grouped_options) > 0)
                        or (jsonb_typeof(grouped_options) = 'object' and grouped_options <> '{}'::jsonb)
                    )
                ) as has_characteristics
            from "ParserProductDetailRows"
            where wb_product_id is not null and wb_product_id <> ''
            group by marketplace, wb_product_id, source_subcategory, source_region_dest;

            create index on "_parser_quality_detail_scopes" (marketplace_key, wb_product_id, source_subcategory_key, source_region_dest_key);

            create temp table "_parser_quality_review_fetch_roots" on commit drop as
            with fetch_counts as (
                select
                    f.id,
                    f.source_wb_root_id,
                    f.status,
                    f.selected_review_rows_seen,
                    f.replies_written,
                    coalesce(rv.stored_review_rows, 0)::int as stored_review_rows,
                    coalesce(rp.stored_reply_rows, 0)::int as stored_reply_rows
                from "ParserReviewRootFetches" f
                left join (
                    select id_review_root_fetch, count(*) as stored_review_rows
                    from "ParserReviewRows"
                    group by id_review_root_fetch
                ) rv on rv.id_review_root_fetch = f.id
                left join (
                    select id_review_root_fetch, count(*) as stored_reply_rows
                    from "ParserReviewReplyRows"
                    group by id_review_root_fetch
                ) rp on rp.id_review_root_fetch = f.id
                where f.source_wb_root_id is not null and f.source_wb_root_id <> ''
            )
            select
                source_wb_root_id,
                true as has_review_fetch,
                bool_or(
                    lower(coalesce(status, '')) in ('success', 'succeeded', 'empty')
                    and coalesce(selected_review_rows_seen, 0) = stored_review_rows
                    and coalesce(replies_written, 0) = stored_reply_rows
                ) as has_valid_review_fetch
            from fetch_counts
            group by source_wb_root_id;

            create index on "_parser_quality_review_fetch_roots" (source_wb_root_id);

            create temp table "_parser_quality_bad_product_reasons" on commit drop as
            with product_quality as (
                select
                    p.id,
                    p.parser_run_id,
                    p.marketplace,
                    case when lower(coalesce(p.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(p.marketplace, '')) end as marketplace_key,
                    p.wb_product_id,
                    p.wb_root_id,
                    p.source_category,
                    p.source_subcategory,
                    p.source_region_dest,
                    coalesce(p.source_subcategory, '') as source_subcategory_key,
                    coalesce(p.source_region_dest, '') as source_region_dest_key,
                    p.name,
                    (l.wb_product_id is not null) as has_logistics,
                    coalesce(d.has_detail_row, false) as has_detail_row,
                    coalesce(d.has_description, false) as has_description,
                    coalesce(d.has_characteristics, false) as has_characteristics,
                    (
                        coalesce(p.image_count, 0) > 0
                        or (jsonb_typeof(p.image_urls) = 'array' and jsonb_array_length(p.image_urls) > 0)
                    ) as has_images,
                    coalesce(r.has_review_fetch, false) as has_review_fetch,
                    coalesce(r.has_valid_review_fetch, false) as has_valid_review_fetch
                from "ParserProductRows" p
                left join "_parser_quality_good_logistics_scopes" l
                  on l.marketplace_key = case when lower(coalesce(p.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(p.marketplace, '')) end
                 and l.wb_product_id = p.wb_product_id
                 and l.source_subcategory_key = coalesce(p.source_subcategory, '')
                 and l.source_region_dest_key = coalesce(p.source_region_dest, '')
                left join "_parser_quality_detail_scopes" d
                  on d.marketplace_key = case when lower(coalesce(p.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(p.marketplace, '')) end
                 and d.wb_product_id = p.wb_product_id
                 and d.source_subcategory_key = coalesce(p.source_subcategory, '')
                 and d.source_region_dest_key = coalesce(p.source_region_dest, '')
                left join "_parser_quality_review_fetch_roots" r
                  on r.source_wb_root_id = p.wb_root_id
            ), reasons as (
                select q.*, r.reason
                from product_quality q
                cross join lateral (values
                    ('invalid_product_identity', q.wb_product_id is null or q.wb_product_id = '' or q.wb_root_id is null or q.wb_root_id = ''),
                    ('missing_logistics', not q.has_logistics),
                    ('missing_product_details', not q.has_detail_row),
                    ('missing_description', q.has_detail_row and not q.has_description),
                    ('missing_characteristics', q.has_detail_row and not q.has_characteristics),
                    ('missing_images', not q.has_images),
                    ('missing_review_fetch', not q.has_review_fetch),
                    ('invalid_review_fetch', q.has_review_fetch and not q.has_valid_review_fetch)
                ) as r(reason, is_bad)
                where r.is_bad
            )
            select *
            from reasons;

            create index on "_parser_quality_bad_product_reasons" (id);
            create index on "_parser_quality_bad_product_reasons" (reason);

            create temp table "_parser_quality_bad_product_rows" on commit drop as
            select distinct
                id,
                parser_run_id,
                marketplace,
                marketplace_key,
                wb_product_id,
                wb_root_id,
                source_category,
                source_subcategory,
                source_region_dest,
                source_subcategory_key,
                source_region_dest_key,
                name
            from "_parser_quality_bad_product_reasons";

            create index on "_parser_quality_bad_product_rows" (id);
            create index on "_parser_quality_bad_product_rows" (marketplace_key, wb_product_id, source_subcategory_key, source_region_dest_key);

            create temp table "_parser_quality_good_product_scopes" on commit drop as
            select distinct
                p.marketplace,
                case when lower(coalesce(p.marketplace, '')) in ('wb', 'wildberries') then 'wildberries' else lower(coalesce(p.marketplace, '')) end as marketplace_key,
                p.wb_product_id,
                coalesce(p.source_subcategory, '') as source_subcategory_key,
                coalesce(p.source_region_dest, '') as source_region_dest_key
            from "ParserProductRows" p
            left join "_parser_quality_bad_product_rows" b on b.id = p.id
            where b.id is null
              and p.wb_product_id is not null
              and p.wb_product_id <> '';

            create index on "_parser_quality_good_product_scopes" (marketplace_key, wb_product_id, source_subcategory_key, source_region_dest_key);

            create temp table "_parser_quality_delete_scopes" on commit drop as
            select distinct
                b.marketplace,
                b.marketplace_key,
                b.wb_product_id,
                b.source_subcategory,
                b.source_region_dest,
                b.source_subcategory_key,
                b.source_region_dest_key
            from "_parser_quality_bad_product_rows" b
            left join "_parser_quality_good_product_scopes" g
              on g.marketplace_key = b.marketplace_key
             and g.wb_product_id = b.wb_product_id
             and g.source_subcategory_key = b.source_subcategory_key
             and g.source_region_dest_key = b.source_region_dest_key
            where g.wb_product_id is null;

            create index on "_parser_quality_delete_scopes" (marketplace_key, wb_product_id, source_subcategory_key, source_region_dest_key);
            """,
            cancellationToken);
    }

    private static async Task WriteAuditReportAsync(
        DbConnection connection,
        DbTransaction transaction,
        string reportPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(reportPath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString("generatedAtUtc", DateTime.UtcNow);
        writer.WriteString("mode", "audit");
        writer.WriteNumber(
            "badProductRows",
            await ExecuteScalarLongAsync(connection, transaction, """select count(*) from "_parser_quality_bad_product_rows";""", cancellationToken));
        writer.WriteNumber(
            "deleteScopes",
            await ExecuteScalarLongAsync(connection, transaction, """select count(*) from "_parser_quality_delete_scopes";""", cancellationToken));

        await WriteJsonPropertyAsync(
            writer,
            "reasonSummary",
            await QueryJsonAsync(
                connection,
                transaction,
                """
                select coalesce(jsonb_agg(to_jsonb(x) order by x.products desc, x.reason, x.parser_run_id, x.source_subcategory), '[]'::jsonb)::text
                from (
                    select reason, parser_run_id, source_subcategory, count(*) as products
                    from "_parser_quality_bad_product_reasons"
                    group by reason, parser_run_id, source_subcategory
                ) x;
                """,
                cancellationToken));
        await WriteJsonPropertyAsync(
            writer,
            "samples",
            await QueryJsonAsync(
                connection,
                transaction,
                """
                with ranked as (
                    select
                        reason,
                        parser_run_id,
                        source_subcategory,
                        wb_product_id,
                        wb_root_id,
                        left(coalesce(name, ''), 120) as name,
                        row_number() over (partition by reason order by parser_run_id desc, wb_product_id) as rn
                    from "_parser_quality_bad_product_reasons"
                )
                select coalesce(jsonb_object_agg(reason, items), '{}'::jsonb)::text
                from (
                    select reason, jsonb_agg(to_jsonb(ranked) - 'reason' - 'rn' order by rn) as items
                    from ranked
                    where rn <= 20
                    group by reason
                ) x;
                """,
                cancellationToken));
        await WriteJsonPropertyAsync(
            writer,
            "reviewFetchSummary",
            await QueryJsonAsync(
                connection,
                transaction,
                """
                select coalesce(jsonb_agg(to_jsonb(x) order by x.parser_run_id, x.status), '[]'::jsonb)::text
                from (
                    select
                        parser_run_id,
                        status,
                        count(*) as root_fetches,
                        sum(coalesce(payload_feedback_count, 0)) as payload_feedback_count,
                        sum(coalesce(payload_feedback_rows_seen, 0)) as payload_feedback_rows_seen,
                        sum(coalesce(selected_review_rows_seen, 0)) as selected_review_rows_seen,
                        sum(coalesce(reviews_written, 0)) as reviews_written,
                        sum(coalesce(replies_written, 0)) as replies_written,
                        count(*) filter (where is_partial_snapshot) as partial_fetches,
                        count(*) filter (where is_capped_root_payload) as capped_fetches,
                        count(*) filter (where is_full_history_unknown) as full_history_unknown_fetches
                    from "ParserReviewRootFetches"
                    group by parser_run_id, status
                ) x;
                """,
                cancellationToken));
        await WriteJsonPropertyAsync(
            writer,
            "sanitySummary",
            await QueryJsonAsync(
                connection,
                transaction,
                """
                select jsonb_build_object(
                    'productRows', (select count(*) from "ParserProductRows"),
                    'productsWithNoImages', (
                        select count(*)
                        from "ParserProductRows"
                        where coalesce(image_count, 0) = 0
                          and not (jsonb_typeof(image_urls) = 'array' and jsonb_array_length(image_urls) > 0)
                    ),
                    'detailRows', (select count(*) from "ParserProductDetailRows"),
                    'detailRowsWithNullMediaCount', (
                        select count(*)
                        from "ParserProductDetailRows"
                        where media_count is null
                    ),
                    'logisticsRows', (select count(*) from "ParserLogisticsSnapshotRows"),
                    'warehouseRows', (select count(*) from "ParserWarehouseAvailabilityRows"),
                    'productVsLogisticsQuantityMismatches', (
                        select count(*)
                        from "ParserProductRows" p
                        join "ParserLogisticsSnapshotRows" l
                          on l.wb_product_id = p.wb_product_id
                         and coalesce(l.source_subcategory, '') = coalesce(p.source_subcategory, '')
                         and coalesce(l.source_region_dest, '') = coalesce(p.source_region_dest, '')
                        where p.total_quantity is not null
                          and l.total_quantity_observed is not null
                          and p.total_quantity <> l.total_quantity_observed
                    )
                )::text;
                """,
                cancellationToken));

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WritePurgeReportAsync(
        string purgePath,
        string auditPath,
        IReadOnlyCollection<string> backupTables,
        long badProducts,
        long deleteScopes,
        long deletedRows,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(purgePath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("generatedAtUtc", DateTime.UtcNow);
        writer.WriteString("auditReportPath", auditPath);
        writer.WriteNumber("badProductRows", badProducts);
        writer.WriteNumber("deleteScopes", deleteScopes);
        writer.WriteNumber("deletedRows", deletedRows);
        writer.WriteStartArray("backupTables");
        foreach (var table in backupTables)
            writer.WriteStringValue(table);
        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WriteJsonPropertyAsync(Utf8JsonWriter writer, string propertyName, string json)
    {
        writer.WritePropertyName(propertyName);
        using var document = JsonDocument.Parse(json);
        document.RootElement.WriteTo(writer);
        await Task.CompletedTask;
    }

    private static async Task<string> QueryJsonAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        var value = await ExecuteScalarAsync(connection, transaction, sql, cancellationToken);
        return value?.ToString() ?? "null";
    }

    private static async Task<long> ReadReportCountAsync(
        string reportPath,
        string propertyName,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(reportPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : 0;
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static async Task<long> ExecuteScalarLongAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        var value = await ExecuteScalarAsync(connection, transaction, sql, cancellationToken);
        return value switch
        {
            null or DBNull => 0,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            _ => Convert.ToInt64(value)
        };
    }

    private static async Task<object?> ExecuteScalarAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 600;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<long> ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 600;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal sealed record ParserQualityMaintenanceResult(
    string Command,
    string? AuditReportPath,
    string? PurgeReportPath,
    long BadProductRows,
    long DeleteScopes,
    long DeletedRows,
    IReadOnlyCollection<string> BackupTables)
{
    public int ErrorCount => 0;
}
