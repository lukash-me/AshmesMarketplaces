using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [Migration("20260622090000_PublicTopForecast")]
    [DbContext(typeof(ApplicationDbContext))]
    public partial class PublicTopForecast : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicTopForecastRuns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_artifact_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    trained_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    training_sample_size = table.Column<int>(type: "integer", nullable: false),
                    validation_sample_size = table.Column<int>(type: "integer", nullable: false),
                    test_sample_size = table.Column<int>(type: "integer", nullable: false),
                    positive_count = table.Column<int>(type: "integer", nullable: false),
                    predictions_count = table.Column<int>(type: "integer", nullable: false),
                    min_probability = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: false),
                    feature_schema_json = table.Column<string>(type: "jsonb", nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicTopForecastRuns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PublicTopForecastPredictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_run = table.Column<Guid>(type: "uuid", nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    top_n = table.Column<int>(type: "integer", nullable: false),
                    product_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wb_product_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    product_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    feedback_count = table.Column<int>(type: "integer", nullable: true),
                    stock = table.Column<int>(type: "integer", nullable: true),
                    current_position = table.Column<int>(type: "integer", nullable: true),
                    current_position_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    observed_range_limit = table.Column<int>(type: "integer", nullable: true),
                    predicted_position = table.Column<int>(type: "integer", nullable: true),
                    top100_probability = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    feature_coverage_percent = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reasons_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicTopForecastPredictions", x => x.id);
                    table.ForeignKey(
                        name: "FK_PublicTopForecastPredictions_PublicTopForecastRuns_id_run",
                        column: x => x.id_run,
                        principalTable: "PublicTopForecastRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastRuns_calculated_at_utc",
                table: "PublicTopForecastRuns",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastRuns_status",
                table: "PublicTopForecastRuns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastPredictions_context",
                table: "PublicTopForecastPredictions",
                columns: new[] { "source_category", "source_subcategory", "query", "source_region_dest", "sort", "top_n" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastPredictions_id_run_top100_probability",
                table: "PublicTopForecastPredictions",
                columns: new[] { "id_run", "top100_probability" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastPredictions_product_row_id",
                table: "PublicTopForecastPredictions",
                column: "product_row_id");

            migrationBuilder.CreateIndex(
                name: "IX_PublicTopForecastPredictions_wb_product_id",
                table: "PublicTopForecastPredictions",
                column: "wb_product_id");

            migrationBuilder.Sql("""
                INSERT INTO "PublicAnalysisSchedules"
                    (id, schedule_key, timezone_id, local_time, next_run_at_utc, last_status, created_at_utc, updated_at_utc)
                SELECT
                    '4230ad23-545d-4c8a-93b9-31c9833cf3ac'::uuid,
                    'top_forecast_public',
                    'Europe/Moscow',
                    TIME '05:00:00',
                    TIMESTAMPTZ '2026-06-22 02:00:00+00',
                    'pending',
                    TIMESTAMPTZ '2026-06-22 00:00:00+00',
                    TIMESTAMPTZ '2026-06-22 00:00:00+00'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "PublicAnalysisSchedules"
                    WHERE schedule_key = 'top_forecast_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "PublicAnalysisSchedules"
                WHERE schedule_key = 'top_forecast_public';
                """);

            migrationBuilder.DropTable(name: "PublicTopForecastPredictions");
            migrationBuilder.DropTable(name: "PublicTopForecastRuns");
        }
    }
}
