using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserIngestionStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserRuns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manifest_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_scope = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    counters = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_registered_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRuns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserFiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    row_count = table.Column<long>(type: "bigint", nullable: true),
                    date_registered_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserFiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserFiles_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserImportExecutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: true),
                    mode = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rows_read = table.Column<long>(type: "bigint", nullable: false),
                    rows_written = table.Column<long>(type: "bigint", nullable: false),
                    rows_skipped = table.Column<long>(type: "bigint", nullable: false),
                    error_count = table.Column<long>(type: "bigint", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserImportExecutions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserImportExecutions_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserProductRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    parsed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sku_product = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    entity = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    brand_id_on_mp = table.Column<long>(type: "bigint", nullable: true),
                    brand_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    seller_id_on_mp = table.Column<long>(type: "bigint", nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    price_regular = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_discounted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_wb_wallet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: true),
                    total_quantity = table.Column<int>(type: "integer", nullable: true),
                    rating_rounded = table.Column<int>(type: "integer", nullable: true),
                    review_rating = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    feedback_count = table.Column<int>(type: "integer", nullable: true),
                    feedback_count_source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    image_urls = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    image_count = table.Column<int>(type: "integer", nullable: true),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    subject_parent_id = table.Column<long>(type: "bigint", nullable: true),
                    subject_id = table.Column<long>(type: "bigint", nullable: true),
                    raw_observed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProductRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserProductRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserProductRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserReviewRootFetches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    source_wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    selected_wb_product_ids = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    retries = table.Column<int>(type: "integer", nullable: false),
                    backoff_seconds_total = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    payload_feedback_count = table.Column<int>(type: "integer", nullable: true),
                    payload_feedback_rows_seen = table.Column<int>(type: "integer", nullable: false),
                    selected_review_rows_seen = table.Column<int>(type: "integer", nullable: false),
                    reviews_written = table.Column<int>(type: "integer", nullable: false),
                    replies_written = table.Column<int>(type: "integer", nullable: false),
                    raw_payload_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    is_partial_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    is_capped_root_payload = table.Column<bool>(type: "boolean", nullable: false),
                    is_full_history_unknown = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserReviewRootFetches", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserReviewRootFetches_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserReviewRootFetches_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserImportErrors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_import_execution = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: true),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: true),
                    source_line_number = table.Column<long>(type: "bigint", nullable: true),
                    phase = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserImportErrors", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserImportErrors_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserImportErrors_ParserImportExecutions_id_import_executi~",
                        column: x => x.id_import_execution,
                        principalTable: "ParserImportExecutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserImportErrors_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserReviewReplyRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    id_review_root_fetch = table.Column<Guid>(type: "uuid", nullable: true),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    parsed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_products_parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    input_products_jsonl = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    source_wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    review_attribution_mode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    review_id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reply_id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reply_fallback_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    created_at_on_mp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_on_mp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reply_author = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reply_state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    raw_observed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    is_partial_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    is_capped_root_payload = table.Column<bool>(type: "boolean", nullable: false),
                    is_full_history_unknown = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserReviewReplyRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserReviewReplyRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserReviewReplyRows_ParserReviewRootFetches_id_review_roo~",
                        column: x => x.id_review_root_fetch,
                        principalTable: "ParserReviewRootFetches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserReviewReplyRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParserReviewRows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parser_file = table.Column<Guid>(type: "uuid", nullable: false),
                    id_review_root_fetch = table.Column<Guid>(type: "uuid", nullable: true),
                    source_line_number = table.Column<long>(type: "bigint", nullable: false),
                    row_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    parsed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_products_parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    input_products_jsonl = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    source_wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    review_attribution_mode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    review_id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    pros = table.Column<string>(type: "text", nullable: true),
                    cons = table.Column<string>(type: "text", nullable: true),
                    created_at_on_mp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewer_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reviewer_country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reviewer_has_photo = table.Column<bool>(type: "boolean", nullable: true),
                    helpful_plus = table.Column<int>(type: "integer", nullable: true),
                    helpful_minus = table.Column<int>(type: "integer", nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    raw_observed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    is_partial_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    is_capped_root_payload = table.Column<bool>(type: "boolean", nullable: false),
                    is_full_history_unknown = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserReviewRows", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserReviewRows_ParserFiles_id_parser_file",
                        column: x => x.id_parser_file,
                        principalTable: "ParserFiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserReviewRows_ParserReviewRootFetches_id_review_root_fet~",
                        column: x => x.id_review_root_fetch,
                        principalTable: "ParserReviewRootFetches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParserReviewRows_ParserRuns_id_parser_run",
                        column: x => x.id_parser_run,
                        principalTable: "ParserRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserFiles_id_parser_run_kind",
                table: "ParserFiles",
                columns: new[] { "id_parser_run", "kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserImportErrors_id_import_execution",
                table: "ParserImportErrors",
                column: "id_import_execution");

            migrationBuilder.CreateIndex(
                name: "IX_ParserImportErrors_id_parser_file",
                table: "ParserImportErrors",
                column: "id_parser_file");

            migrationBuilder.CreateIndex(
                name: "IX_ParserImportErrors_id_parser_run",
                table: "ParserImportErrors",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserImportExecutions_id_parser_run",
                table: "ParserImportExecutions",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_id_parser_file_source_line_number",
                table: "ParserProductRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_id_parser_run",
                table: "ParserProductRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_parsed_at_utc",
                table: "ParserProductRows",
                column: "parsed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_parser_run_id",
                table: "ParserProductRows",
                column: "parser_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_parser_run_id_parsed_at_utc",
                table: "ParserProductRows",
                columns: new[] { "parser_run_id", "parsed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_wb_product_id",
                table: "ParserProductRows",
                column: "wb_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductRows_wb_root_id",
                table: "ParserProductRows",
                column: "wb_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewReplyRows_id_parser_file_source_line_number",
                table: "ParserReviewReplyRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewReplyRows_id_parser_run_review_id_on_mp",
                table: "ParserReviewReplyRows",
                columns: new[] { "id_parser_run", "review_id_on_mp" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewReplyRows_id_review_root_fetch",
                table: "ParserReviewReplyRows",
                column: "id_review_root_fetch");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewReplyRows_source_wb_root_id",
                table: "ParserReviewReplyRows",
                column: "source_wb_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRootFetches_id_parser_file_source_line_number",
                table: "ParserReviewRootFetches",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRootFetches_id_parser_run_source_wb_root_id",
                table: "ParserReviewRootFetches",
                columns: new[] { "id_parser_run", "source_wb_root_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_created_at_on_mp",
                table: "ParserReviewRows",
                column: "created_at_on_mp");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_id_parser_file_source_line_number",
                table: "ParserReviewRows",
                columns: new[] { "id_parser_file", "source_line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_id_parser_run",
                table: "ParserReviewRows",
                column: "id_parser_run");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_id_review_root_fetch",
                table: "ParserReviewRows",
                column: "id_review_root_fetch");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_parser_run_id",
                table: "ParserReviewRows",
                column: "parser_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_parser_run_id_created_at_on_mp",
                table: "ParserReviewRows",
                columns: new[] { "parser_run_id", "created_at_on_mp" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_review_id_on_mp",
                table: "ParserReviewRows",
                column: "review_id_on_mp");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_source_wb_root_id",
                table: "ParserReviewRows",
                column: "source_wb_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserReviewRows_wb_product_id",
                table: "ParserReviewRows",
                column: "wb_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRuns_parser_run_id_marketplace_kind",
                table: "ParserRuns",
                columns: new[] { "parser_run_id", "marketplace", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserImportErrors");

            migrationBuilder.DropTable(
                name: "ParserProductRows");

            migrationBuilder.DropTable(
                name: "ParserReviewReplyRows");

            migrationBuilder.DropTable(
                name: "ParserReviewRows");

            migrationBuilder.DropTable(
                name: "ParserImportExecutions");

            migrationBuilder.DropTable(
                name: "ParserReviewRootFetches");

            migrationBuilder.DropTable(
                name: "ParserFiles");

            migrationBuilder.DropTable(
                name: "ParserRuns");
        }
    }
}
