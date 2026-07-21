using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserLogisticsStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserLogisticsSnapshotRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    marketplace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_request_family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_endpoint = table.Column<string>(type: "text", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    seller_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    total_quantity_observed = table.Column<int>(type: "integer", nullable: true),
                    quantity_is_capped = table.Column<bool>(type: "boolean", nullable: true),
                    quantity_cap_observed = table.Column<int>(type: "integer", nullable: true),
                    quantity_semantics = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    product_wh_raw = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    product_time1_raw = table.Column<int>(type: "integer", nullable: true),
                    product_time2_raw = table.Column<int>(type: "integer", nullable: true),
                    product_dtype_raw = table.Column<long>(type: "bigint", nullable: true),
                    product_dist_raw = table.Column<int>(type: "integer", nullable: true),
                    raw_observed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserLogisticsSnapshotRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserLogisticsSnapshotRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserLogisticsSnapshotRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserWarehouseAvailabilityRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    marketplace = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_request_family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_endpoint = table.Column<string>(type: "text", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    seller_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    option_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    size_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    size_orig_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    size_rank = table.Column<int>(type: "integer", nullable: true),
                    warehouse_id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    quantity_observed = table.Column<int>(type: "integer", nullable: true),
                    quantity_is_capped = table.Column<bool>(type: "boolean", nullable: true),
                    quantity_cap_observed = table.Column<int>(type: "integer", nullable: true),
                    quantity_semantics = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stock_priority_raw = table.Column<int>(type: "integer", nullable: true),
                    stock_time1_raw = table.Column<int>(type: "integer", nullable: true),
                    stock_time2_raw = table.Column<int>(type: "integer", nullable: true),
                    stock_dtype_raw = table.Column<long>(type: "bigint", nullable: true),
                    stock_dist_raw = table.Column<int>(type: "integer", nullable: true),
                    price_basic = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_product = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_logistics_raw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_return_raw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    raw_stock = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    raw_size_observed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserWarehouseAvailabilityRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserWarehouseAvailabilityRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserWarehouseAvailabilityRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_id_parser_file_source_line_numb~",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_id_parser_run",
                table: "ParserLogisticsSnapshotRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_parser_run_id_observed_at_utc",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "parser_run_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_request_fingerprint",
                table: "ParserLogisticsSnapshotRows",
                column: "request_fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_source_category_source_subcateg~",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "source_category", "source_subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_source_region_dest_observed_at_~",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "source_region_dest", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_wb_product_id_observed_at_utc",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "wb_product_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_wb_root_id_observed_at_utc",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "wb_root_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_id_parser_file_source_line_~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_id_parser_run",
                table: "ParserWarehouseAvailabilityRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_option_id_observed_at_utc",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "option_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_parser_run_id_observed_at_u~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "parser_run_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_request_fingerprint",
                table: "ParserWarehouseAvailabilityRows",
                column: "request_fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_source_category_source_subc~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "source_category", "source_subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_source_region_dest_observed~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "source_region_dest", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_warehouse_id_on_mp_observed~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "warehouse_id_on_mp", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_wb_product_id_observed_at_u~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "wb_product_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_wb_root_id_observed_at_utc",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "wb_root_id", "observed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropTable(
                name: "ParserWarehouseAvailabilityRows");
        }
    }
}
