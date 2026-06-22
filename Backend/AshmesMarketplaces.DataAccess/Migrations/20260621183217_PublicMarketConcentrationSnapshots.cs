using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PublicMarketConcentrationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicMarketConcentrationSnapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    top_n = table.Column<int>(type: "integer", nullable: false),
                    market_concentration_json = table.Column<string>(type: "jsonb", nullable: false),
                    price_quality_points_json = table.Column<string>(type: "jsonb", nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    latest_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicMarketConcentrationSnapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicMarketConcentrationSnapshots_calculated_at_utc",
                table: "PublicMarketConcentrationSnapshots",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicMarketConcentrationSnapshots_context",
                table: "PublicMarketConcentrationSnapshots",
                columns: new[] { "source_category", "source_subcategory", "query", "source_region_dest", "sort", "top_n" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "PublicAnalysisSchedules"
                    (id, schedule_key, timezone_id, local_time, next_run_at_utc, last_status, created_at_utc, updated_at_utc)
                SELECT
                    'a8705b94-5d23-4708-9b9b-1894b4d31710'::uuid,
                    'market_concentration_public',
                    'Europe/Moscow',
                    TIME '04:00:00',
                    TIMESTAMPTZ '2026-06-21 01:00:00+00',
                    'pending',
                    TIMESTAMPTZ '2026-06-21 00:00:00+00',
                    TIMESTAMPTZ '2026-06-21 00:00:00+00'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "PublicAnalysisSchedules"
                    WHERE schedule_key = 'market_concentration_public'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "PublicAnalysisSchedules"
                WHERE schedule_key = 'market_concentration_public';
                """);

            migrationBuilder.DropTable(
                name: "PublicMarketConcentrationSnapshots");
        }
    }
}
