using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260622121000_PublicParserObservedLogisticsSnapshots")]
    public partial class PublicParserObservedLogisticsSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicParserObservedLogisticsSnapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_logistics_run_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    previous_logistics_run_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    current_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    previous_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    market_events_json = table.Column<string>(type: "jsonb", nullable: false),
                    market_event_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    stock_decrease_items_json = table.Column<string>(type: "jsonb", nullable: false),
                    stock_decrease_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    market_events_count = table.Column<int>(type: "integer", nullable: false),
                    stock_decreases_count = table.Column<int>(type: "integer", nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicParserObservedLogisticsSnapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicParserObservedLogisticsSnapshots_calculated_at_utc",
                table: "PublicParserObservedLogisticsSnapshots",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicParserObservedLogisticsSnapshots_snapshot_key",
                table: "PublicParserObservedLogisticsSnapshots",
                column: "snapshot_key",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "PublicAnalysisSchedules"
                    ("id", "schedule_key", "timezone_id", "local_time", "next_run_at_utc", "last_status", "created_at_utc", "updated_at_utc")
                SELECT gen_random_uuid(), 'market_logistics_events_public', 'Europe/Moscow', TIME '04:30', now(), 'pending', now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "PublicAnalysisSchedules" WHERE "schedule_key" = 'market_logistics_events_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "PublicAnalysisSchedules"
                WHERE "schedule_key" = 'market_logistics_events_public';
                """);

            migrationBuilder.DropTable(name: "PublicParserObservedLogisticsSnapshots");
        }
    }
}
