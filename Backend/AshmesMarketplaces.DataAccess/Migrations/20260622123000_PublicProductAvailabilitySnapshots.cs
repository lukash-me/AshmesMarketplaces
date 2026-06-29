using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260622123000_PublicProductAvailabilitySnapshots")]
    public partial class PublicProductAvailabilitySnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicProductAvailabilitySnapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    items_json = table.Column<string>(type: "jsonb", nullable: false),
                    total_count = table.Column<int>(type: "integer", nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProductAvailabilitySnapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProductAvailabilitySnapshots_calculated_at_utc",
                table: "PublicProductAvailabilitySnapshots",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicProductAvailabilitySnapshots_snapshot_key",
                table: "PublicProductAvailabilitySnapshots",
                column: "snapshot_key",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "PublicAnalysisSchedules"
                    ("id", "schedule_key", "timezone_id", "local_time", "next_run_at_utc", "last_status", "created_at_utc", "updated_at_utc")
                SELECT gen_random_uuid(), 'product_availability_public', 'Europe/Moscow', TIME '05:00', now(), 'pending', now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "PublicAnalysisSchedules" WHERE "schedule_key" = 'product_availability_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "PublicAnalysisSchedules"
                WHERE "schedule_key" = 'product_availability_public';
                """);

            migrationBuilder.DropTable(name: "PublicProductAvailabilitySnapshots");
        }
    }
}
