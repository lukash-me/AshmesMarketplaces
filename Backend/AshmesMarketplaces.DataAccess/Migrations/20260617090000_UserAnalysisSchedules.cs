using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    public partial class UserAnalysisSchedules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "id_user",
                table: "MarketRecommendationRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserAnalysisSchedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    timezone_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    hot_products_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    overview_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    next_hot_products_run_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_overview_run_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_hot_products_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_hot_products_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_hot_products_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_hot_products_error = table.Column<string>(type: "text", nullable: true),
                    last_overview_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_overview_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_overview_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_overview_error = table.Column<string>(type: "text", nullable: true),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAnalysisSchedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserAnalysisSchedules_Users_id_user",
                        column: x => x.id_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_user_latest",
                table: "MarketRecommendationRuns",
                columns: new[] { "id_user", "kind", "status", "valid_until_utc", "completed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRecommendationRuns_id_user",
                table: "MarketRecommendationRuns",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalysisSchedules_id_user",
                table: "UserAnalysisSchedules",
                column: "id_user",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalysisSchedules_locked_until_utc",
                table: "UserAnalysisSchedules",
                column: "locked_until_utc");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalysisSchedules_next_hot_products_run_at_utc",
                table: "UserAnalysisSchedules",
                column: "next_hot_products_run_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnalysisSchedules_next_overview_run_at_utc",
                table: "UserAnalysisSchedules",
                column: "next_overview_run_at_utc");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketRecommendationRuns_Users_id_user",
                table: "MarketRecommendationRuns",
                column: "id_user",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarketRecommendationRuns_Users_id_user",
                table: "MarketRecommendationRuns");

            migrationBuilder.DropTable(
                name: "UserAnalysisSchedules");

            migrationBuilder.DropIndex(
                name: "IX_MarketRecommendationRuns_user_latest",
                table: "MarketRecommendationRuns");

            migrationBuilder.DropIndex(
                name: "IX_MarketRecommendationRuns_id_user",
                table: "MarketRecommendationRuns");

            migrationBuilder.DropColumn(
                name: "id_user",
                table: "MarketRecommendationRuns");
        }
    }
}
