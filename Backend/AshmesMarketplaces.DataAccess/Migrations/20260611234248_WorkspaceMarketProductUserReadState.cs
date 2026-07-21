using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceMarketProductUserReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceMarketProductUserReadStates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace_market_product = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    last_viewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    baseline_observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    baseline_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    baseline_position = table.Column<int>(type: "integer", nullable: true),
                    baseline_stock = table.Column<int>(type: "integer", nullable: true),
                    baseline_feedback_count = table.Column<int>(type: "integer", nullable: true),
                    baseline_review_rating = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMarketProductUserReadStates", x => x.id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductUserReadStates_Users_id_user",
                        column: x => x.id_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductUserReadStates_WorkspaceMarketProduct~",
                        column: x => x.id_workspace_market_product,
                        principalTable: "WorkspaceMarketProducts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductUserReadStates_id_user",
                table: "WorkspaceMarketProductUserReadStates",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductUserReadStates_id_workspace_market_p~1",
                table: "WorkspaceMarketProductUserReadStates",
                columns: new[] { "id_workspace_market_product", "id_user" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductUserReadStates_id_workspace_market_pr~",
                table: "WorkspaceMarketProductUserReadStates",
                column: "id_workspace_market_product");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceMarketProductUserReadStates");
        }
    }
}
