using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PublicAnalysisManualRunRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicAnalysisManualRunRequests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicAnalysisManualRunRequests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicAnalysisManualRunRequests_active_schedule_key",
                table: "PublicAnalysisManualRunRequests",
                column: "schedule_key",
                unique: true,
                filter: "status IN ('queued', 'running')");

            migrationBuilder.CreateIndex(
                name: "IX_PublicAnalysisManualRunRequests_schedule_key_requested_at_u~",
                table: "PublicAnalysisManualRunRequests",
                columns: new[] { "schedule_key", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicAnalysisManualRunRequests_status_requested_at_utc",
                table: "PublicAnalysisManualRunRequests",
                columns: new[] { "status", "requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicAnalysisManualRunRequests");
        }
    }
}
