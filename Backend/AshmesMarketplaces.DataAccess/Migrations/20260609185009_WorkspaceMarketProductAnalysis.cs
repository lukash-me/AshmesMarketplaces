using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceMarketProductAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceMarketProductAnalysisRuns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    algorithm = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    product_count = table.Column<int>(type: "integer", nullable: false),
                    signal_count = table.Column<int>(type: "integer", nullable: false),
                    similar_product_count = table.Column<int>(type: "integer", nullable: false),
                    warnings = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMarketProductAnalysisRuns", x => x.id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductAnalysisRuns_Workspaces_id_workspace",
                        column: x => x.id_workspace,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceMarketProductAnalyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_analysis_run = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace_market_product = table.Column<Guid>(type: "uuid", nullable: false),
                    current_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    previous_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_delta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    current_position = table.Column<int>(type: "integer", nullable: true),
                    previous_position = table.Column<int>(type: "integer", nullable: true),
                    position_delta = table.Column<int>(type: "integer", nullable: true),
                    current_stock = table.Column<int>(type: "integer", nullable: true),
                    previous_stock = table.Column<int>(type: "integer", nullable: true),
                    stock_delta = table.Column<int>(type: "integer", nullable: true),
                    current_feedback_count = table.Column<int>(type: "integer", nullable: true),
                    previous_feedback_count = table.Column<int>(type: "integer", nullable: true),
                    feedback_delta = table.Column<int>(type: "integer", nullable: true),
                    latest_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    signals = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    similar_products = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    computed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMarketProductAnalyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductAnalyses_WorkspaceMarketProductAnalys~",
                        column: x => x.id_analysis_run,
                        principalTable: "WorkspaceMarketProductAnalysisRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductAnalyses_WorkspaceMarketProducts_id_w~",
                        column: x => x.id_workspace_market_product,
                        principalTable: "WorkspaceMarketProducts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductAnalyses_id_analysis_run",
                table: "WorkspaceMarketProductAnalyses",
                column: "id_analysis_run");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductAnalyses_id_analysis_run_id_workspace~",
                table: "WorkspaceMarketProductAnalyses",
                columns: new[] { "id_analysis_run", "id_workspace_market_product" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductAnalyses_id_workspace_market_product",
                table: "WorkspaceMarketProductAnalyses",
                column: "id_workspace_market_product");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductAnalysisRuns_id_workspace_completed_a~",
                table: "WorkspaceMarketProductAnalysisRuns",
                columns: new[] { "id_workspace", "completed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductAnalysisRuns_id_workspace_status",
                table: "WorkspaceMarketProductAnalysisRuns",
                columns: new[] { "id_workspace", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceMarketProductAnalyses");

            migrationBuilder.DropTable(
                name: "WorkspaceMarketProductAnalysisRuns");
        }
    }
}
