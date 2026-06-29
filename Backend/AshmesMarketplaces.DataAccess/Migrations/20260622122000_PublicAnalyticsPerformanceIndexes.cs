using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260622122000_PublicAnalyticsPerformanceIndexes")]
    public partial class PublicAnalyticsPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserProductRows_market_context_latest"
                ON "ParserProductRows" ("source_category", "source_subcategory", "source_region_dest", "parsed_at_utc" DESC, "id" DESC);
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserLogisticsSnapshotRows_run_product_profile"
                ON "ParserLogisticsSnapshotRows" ("parser_run_id", "wb_product_id", "delivery_profile_key");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserLogisticsSnapshotRows_market_product_observed"
                ON "ParserLogisticsSnapshotRows" ("source_category", "source_subcategory", "source_region_dest", "wb_product_id", "observed_at_utc" DESC);
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserProductDetailRows_product_status_latest"
                ON "ParserProductDetailRows" ("wb_product_id", "status", "parsed_at_utc" DESC, "source_line_number" DESC);
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserWarehouseAvailabilityRows_run_product"
                ON "ParserWarehouseAvailabilityRows" ("parser_run_id", "wb_product_id");
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserWarehouseAvailabilityRows_run_product";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserProductDetailRows_product_status_latest";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserLogisticsSnapshotRows_market_product_observed";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserLogisticsSnapshotRows_run_product_profile";""", suppressTransaction: true);
            migrationBuilder.Sql("""DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserProductRows_market_context_latest";""", suppressTransaction: true);
        }
    }
}
