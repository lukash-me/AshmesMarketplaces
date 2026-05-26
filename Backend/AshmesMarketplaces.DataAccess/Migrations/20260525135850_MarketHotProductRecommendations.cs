using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MarketHotProductRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketRecommendationRuns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    marketplace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategories = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    product_parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    rank_parser_run_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    review_parser_run_ids = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    intelligence_request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    algorithm = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    input_snapshot_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    product_count_sent = table.Column<int>(type: "integer", nullable: false),
                    recommendations_count = table.Column<int>(type: "integer", nullable: false),
                    warning_count = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    raw_warnings = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    config_options = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRecommendationRuns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MarketHotProductRecommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_market_recommendation_run = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    product_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    id_parser_product_row = table.Column<Guid>(type: "uuid", nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    brand_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_without_discount_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    wallet_price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    rating_snapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    feedback_count_snapshot = table.Column<int>(type: "integer", nullable: true),
                    parsed_review_count_snapshot = table.Column<int>(type: "integer", nullable: true),
                    parsed_reply_count_snapshot = table.Column<int>(type: "integer", nullable: true),
                    position_snapshot = table.Column<int>(type: "integer", nullable: true),
                    position_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    observed_range_limit = table.Column<int>(type: "integer", nullable: true),
                    total_quantity_snapshot = table.Column<int>(type: "integer", nullable: true),
                    score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    input_snapshot_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    valid_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rank_order = table.Column<int>(type: "integer", nullable: false),
                    factors = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketHotProductRecommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_MarketHotProductRecommendations_MarketRecommendationRuns_id~",
                        column: x => x.id_market_recommendation_run,
                        principalTable: "MarketRecommendationRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketHotProductRecommendations_ParserProductRows_id_parser~",
                        column: x => x.id_parser_product_row,
                        principalTable: "ParserProductRows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_id_market_recommendation_r~1",
                table: "MarketHotProductRecommendations",
                columns: new[] { "id_market_recommendation_run", "recommendation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_id_market_recommendation_ru~",
                table: "MarketHotProductRecommendations",
                columns: new[] { "id_market_recommendation_run", "rank_order" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_id_parser_product_row",
                table: "MarketHotProductRecommendations",
                column: "id_parser_product_row");

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_source_category_source_subc~",
                table: "MarketHotProductRecommendations",
                columns: new[] { "source_category", "source_subcategory", "score" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_wb_product_id",
                table: "MarketHotProductRecommendations",
                column: "wb_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_MarketHotProductRecommendations_wb_root_id",
                table: "MarketHotProductRecommendations",
                column: "wb_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_algorithm_algorithm_version_model_~",
                table: "MarketRecommendationRuns",
                columns: new[] { "algorithm", "algorithm_version", "model_version", "input_snapshot_hash" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_kind_status_valid_until_utc_comple~",
                table: "MarketRecommendationRuns",
                columns: new[] { "kind", "status", "valid_until_utc", "completed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_product_parser_run_id",
                table: "MarketRecommendationRuns",
                column: "product_parser_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_rank_parser_run_id",
                table: "MarketRecommendationRuns",
                column: "rank_parser_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketHotProductRecommendations");

            migrationBuilder.DropTable(
                name: "MarketRecommendationRuns");
        }
    }
}
