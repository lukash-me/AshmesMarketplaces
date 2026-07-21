using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260623100000_ParserCurrentProductRows")]
    public partial class ParserCurrentProductRows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserCurrentProductRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    parsed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    price_regular = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    price_discounted = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    price_wb_wallet = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_quantity = table.Column<int>(type: "integer", nullable: true),
                    review_rating = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    feedback_count = table.Column<int>(type: "integer", nullable: true),
                    position_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    position_absolute = table.Column<int>(type: "integer", nullable: true),
                    position_observed_range_limit = table.Column<int>(type: "integer", nullable: true),
                    position_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    position_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserCurrentProductRows", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_brand_name",
                table: "ParserCurrentProductRows",
                column: "brand_name");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_feedback_count",
                table: "ParserCurrentProductRows",
                column: "feedback_count");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_parsed_at_utc",
                table: "ParserCurrentProductRows",
                column: "parsed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_position",
                table: "ParserCurrentProductRows",
                columns: new[] { "position_state", "position_absolute", "position_observed_range_limit" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_price_discounted",
                table: "ParserCurrentProductRows",
                column: "price_discounted");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_product_row_id",
                table: "ParserCurrentProductRows",
                column: "product_row_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_review_rating",
                table: "ParserCurrentProductRows",
                column: "review_rating");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_seller_name",
                table: "ParserCurrentProductRows",
                column: "seller_name");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_source_category_source_subcategory",
                table: "ParserCurrentProductRows",
                columns: new[] { "source_category", "source_subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_wb_product_id",
                table: "ParserCurrentProductRows",
                column: "wb_product_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_wb_root_id",
                table: "ParserCurrentProductRows",
                column: "wb_root_id");

            migrationBuilder.Sql(
                """
                INSERT INTO "PublicAnalysisSchedules"
                    ("id", "schedule_key", "timezone_id", "local_time", "next_run_at_utc", "last_status", "created_at_utc", "updated_at_utc")
                SELECT gen_random_uuid(), 'parser_current_products_public', 'Europe/Moscow', TIME '02:20', now(), 'pending', now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "PublicAnalysisSchedules" WHERE "schedule_key" = 'parser_current_products_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "PublicAnalysisSchedules"
                WHERE "schedule_key" = 'parser_current_products_public';
                """);

            migrationBuilder.DropTable(name: "ParserCurrentProductRows");
        }
    }
}
