using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddParserProductLatestLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ParserProductRows_wb_product_id_parsed_at_utc_id"
                ON "ParserProductRows" ("wb_product_id", "parsed_at_utc" DESC, "id" DESC);
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX CONCURRENTLY IF EXISTS "IX_ParserProductRows_wb_product_id_parsed_at_utc_id";
                """,
                suppressTransaction: true);
        }
    }
}
