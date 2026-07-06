using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WildberriesCategoryLeaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WildberriesCategoryLeaves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_path = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    search_query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    fetched_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WildberriesCategoryLeaves", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WildberriesCategoryLeaves_source_category",
                table: "WildberriesCategoryLeaves",
                column: "source_category");

            migrationBuilder.CreateIndex(
                name: "IX_WildberriesCategoryLeaves_source_path",
                table: "WildberriesCategoryLeaves",
                column: "source_path");

            migrationBuilder.CreateIndex(
                name: "IX_WildberriesCategoryLeaves_source_subcategory",
                table: "WildberriesCategoryLeaves",
                column: "source_subcategory");

            migrationBuilder.CreateIndex(
                name: "IX_WildberriesCategoryLeaves_wb_category_id",
                table: "WildberriesCategoryLeaves",
                column: "wb_category_id",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "PublicAnalysisSchedules"
                    (id, schedule_key, timezone_id, local_time, next_run_at_utc, last_status, created_at_utc, updated_at_utc)
                SELECT gen_random_uuid(), 'wb_category_catalog_refresh', 'Europe/Moscow', TIME '02:15', now(), 'pending', now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "PublicAnalysisSchedules" WHERE schedule_key = 'wb_category_catalog_refresh'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "PublicAnalysisSchedules"
                WHERE schedule_key = 'wb_category_catalog_refresh';
                """);

            migrationBuilder.DropTable(
                name: "WildberriesCategoryLeaves");
        }
    }
}
