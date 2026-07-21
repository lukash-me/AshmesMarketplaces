using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProductDetailsStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserProductDetailRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    parsed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_products_parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    input_products_jsonl = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    source_request_family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_endpoint = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    characteristics = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    grouped_options = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    media_count = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raw_detail_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProductDetailRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserProductDetailRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserProductDetailRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_id_parser_file_source_line_number",
                table: "ParserProductDetailRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_id_parser_run",
                table: "ParserProductDetailRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_input_products_parser_run_id",
                table: "ParserProductDetailRows",
                column: "input_products_parser_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_parser_run_id",
                table: "ParserProductDetailRows",
                column: "parser_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_wb_product_id",
                table: "ParserProductDetailRows",
                column: "wb_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_wb_product_id_parsed_at_utc",
                table: "ParserProductDetailRows",
                columns: new[] { "wb_product_id", "parsed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductDetailRows_wb_root_id",
                table: "ParserProductDetailRows",
                column: "wb_root_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserProductDetailRows");
        }
    }
}
