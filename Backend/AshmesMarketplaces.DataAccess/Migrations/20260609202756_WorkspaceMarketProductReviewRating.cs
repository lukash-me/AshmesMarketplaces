using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceMarketProductReviewRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "current_review_rating",
                table: "WorkspaceMarketProductAnalyses",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "previous_review_rating",
                table: "WorkspaceMarketProductAnalyses",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "review_rating_delta",
                table: "WorkspaceMarketProductAnalyses",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_review_rating",
                table: "WorkspaceMarketProductAnalyses");

            migrationBuilder.DropColumn(
                name: "previous_review_rating",
                table: "WorkspaceMarketProductAnalyses");

            migrationBuilder.DropColumn(
                name: "review_rating_delta",
                table: "WorkspaceMarketProductAnalyses");
        }
    }
}
