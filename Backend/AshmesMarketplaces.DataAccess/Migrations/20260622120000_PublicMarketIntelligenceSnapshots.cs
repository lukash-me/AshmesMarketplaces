using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260622120000_PublicMarketIntelligenceSnapshots")]
    public partial class PublicMarketIntelligenceSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicMarketIntelligenceSnapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    top_n = table.Column<int>(type: "integer", nullable: false),
                    public_market_intelligence_json = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_PublicMarketIntelligenceSnapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicMarketIntelligenceSnapshots_calculated_at_utc",
                table: "PublicMarketIntelligenceSnapshots",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicMarketIntelligenceSnapshots_context",
                table: "PublicMarketIntelligenceSnapshots",
                columns: new[] { "source_category", "source_subcategory", "query", "source_region_dest", "sort", "top_n" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "PublicAnalysisSchedules"
                    ("id", "schedule_key", "timezone_id", "local_time", "next_run_at_utc", "last_status", "created_at_utc", "updated_at_utc")
                SELECT gen_random_uuid(), 'market_intelligence_public', 'Europe/Moscow', TIME '03:30', now(), 'pending', now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "PublicAnalysisSchedules" WHERE "schedule_key" = 'market_intelligence_public'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "PublicAnalysisSchedules"
                WHERE "schedule_key" = 'market_intelligence_public';
                """);

            migrationBuilder.DropTable(name: "PublicMarketIntelligenceSnapshots");
        }
    }
}
