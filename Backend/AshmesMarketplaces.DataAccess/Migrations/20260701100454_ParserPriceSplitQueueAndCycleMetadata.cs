using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserPriceSplitQueueAndCycleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cycle_kind",
                table: "ParserProxyRuns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "parser_cycle_id",
                table: "ParserProxyRuns",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "ParserProxyRuns"
                SET parser_cycle_id = split_part(external_proxy_run_id, ':', 1)
                WHERE parser_cycle_id = '' AND external_proxy_run_id IS NOT NULL AND external_proxy_run_id <> '';
                """);

            migrationBuilder.CreateTable(
                name: "ParserPriceSplitJobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    proxy_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    min_price_u = table.Column<int>(type: "integer", nullable: false),
                    max_price_u = table.Column<int>(type: "integer", nullable: false),
                    total_ranges_count = table.Column<int>(type: "integer", nullable: false),
                    completed_ranges_count = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserPriceSplitJobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserPriceSplitRanges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_range_id = table.Column<Guid>(type: "uuid", nullable: true),
                    min_price_u = table.Column<int>(type: "integer", nullable: false),
                    max_price_u = table.Column<int>(type: "integer", nullable: false),
                    expected_total = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempts_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cooldown_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserPriceSplitRanges", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_cycle_kind_started_at_utc",
                table: "ParserProxyRuns",
                columns: new[] { "parser_instance_id", "cycle_kind", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_parser_cycle_id",
                table: "ParserProxyRuns",
                columns: new[] { "parser_instance_id", "parser_cycle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserPriceSplitJobs_parser_instance_id_proxy_key_source_ca~",
                table: "ParserPriceSplitJobs",
                columns: new[] { "parser_instance_id", "proxy_key", "source_category", "source_subcategory", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserPriceSplitJobs_source_category_source_subcategory_pro~",
                table: "ParserPriceSplitJobs",
                columns: new[] { "source_category", "source_subcategory", "proxy_key", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserPriceSplitRanges_job_id_min_price_u_max_price_u",
                table: "ParserPriceSplitRanges",
                columns: new[] { "job_id", "min_price_u", "max_price_u" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserPriceSplitRanges_job_id_status_cooldown_until_utc",
                table: "ParserPriceSplitRanges",
                columns: new[] { "job_id", "status", "cooldown_until_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserPriceSplitRanges_parent_range_id",
                table: "ParserPriceSplitRanges",
                column: "parent_range_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserPriceSplitJobs");

            migrationBuilder.DropTable(
                name: "ParserPriceSplitRanges");

            migrationBuilder.DropIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_cycle_kind_started_at_utc",
                table: "ParserProxyRuns");

            migrationBuilder.DropIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_parser_cycle_id",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "cycle_kind",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "parser_cycle_id",
                table: "ParserProxyRuns");
        }
    }
}
