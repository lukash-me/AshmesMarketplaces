using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserRankStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserRankPageFetches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rank_context_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rank_context_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sort = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    filters = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    product_count = table.Column<int>(type: "integer", nullable: false),
                    response_total = table.Column<int>(type: "integer", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    wb_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRankPageFetches", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserRankPageFetches_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserRankPageFetches_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserRankSnapshotRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rank_context_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rank_context_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sort = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    filters = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    position_on_page = table.Column<int>(type: "integer", nullable: false),
                    absolute_position = table.Column<int>(type: "integer", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    response_total = table.Column<int>(type: "integer", nullable: true),
                    fetch_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRankSnapshotRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserRankSnapshotRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserRankSnapshotRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankPageFetches_id_parser_file_source_line_number",
                table: "ParserRankPageFetches",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankPageFetches_id_parser_run",
                table: "ParserRankPageFetches",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankPageFetches_parser_run_id_observed_at_utc",
                table: "ParserRankPageFetches",
                columns: new[] { "parser_run_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankPageFetches_rank_context_id_page",
                table: "ParserRankPageFetches",
                columns: new[] { "rank_context_id", "page" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankPageFetches_request_fingerprint_page",
                table: "ParserRankPageFetches",
                columns: new[] { "request_fingerprint", "page" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_id_parser_file_source_line_number",
                table: "ParserRankSnapshotRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_id_parser_run",
                table: "ParserRankSnapshotRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_parser_run_id_observed_at_utc",
                table: "ParserRankSnapshotRows",
                columns: new[] { "parser_run_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_rank_context_id_absolute_position",
                table: "ParserRankSnapshotRows",
                columns: new[] { "rank_context_id", "absolute_position" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_request_fingerprint_page",
                table: "ParserRankSnapshotRows",
                columns: new[] { "request_fingerprint", "page" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_wb_product_id_observed_at_utc",
                table: "ParserRankSnapshotRows",
                columns: new[] { "wb_product_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRankSnapshotRows_wb_root_id_observed_at_utc",
                table: "ParserRankSnapshotRows",
                columns: new[] { "wb_root_id", "observed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserRankPageFetches");

            migrationBuilder.DropTable(
                name: "ParserRankSnapshotRows");
        }
    }
}
