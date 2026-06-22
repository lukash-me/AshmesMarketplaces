using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260618030000_PublicAnalysisSchedules")]
    public partial class PublicAnalysisSchedules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicAnalysisSchedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    timezone_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    next_run_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicAnalysisSchedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicAnalysisSchedules_next_run_at_utc",
                table: "PublicAnalysisSchedules",
                column: "next_run_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicAnalysisSchedules_schedule_key",
                table: "PublicAnalysisSchedules",
                column: "schedule_key",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "PublicAnalysisSchedules"
                    (id, schedule_key, timezone_id, local_time, next_run_at_utc, last_status, created_at_utc, updated_at_utc)
                SELECT
                    '7beef1e1-98a8-4a87-93bb-4a0bff8d719d'::uuid,
                    'hot_products_public',
                    'Europe/Moscow',
                    TIME '03:00:00',
                    TIMESTAMPTZ '2026-06-19 00:00:00+00',
                    'pending',
                    TIMESTAMPTZ '2026-06-18 00:00:00+00',
                    TIMESTAMPTZ '2026-06-18 00:00:00+00'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "PublicAnalysisSchedules"
                    WHERE schedule_key = 'hot_products_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PublicAnalysisSchedules");
        }
    }
}
