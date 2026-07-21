using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserReviewCoverageSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "coverage_source",
                table: "ParserCurrentProductReviewsSummaries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "root_capped_fallback");

            migrationBuilder.AddColumn<string>(
                name: "coverage_status",
                table: "ParserCurrentProductReviewsSummaries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<int>(
                name: "fetched_reviews_count",
                table: "ParserCurrentProductReviewsSummaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_coverage_error",
                table: "ParserCurrentProductReviewsSummaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "marketplace_feedback_count",
                table: "ParserCurrentProductReviewsSummaries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "oldest_review_date_utc",
                table: "ParserCurrentProductReviewsSummaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ParserCurrentProductReviewsSummaries"
                SET "fetched_reviews_count" = "reviews_count"
                WHERE "fetched_reviews_count" = 0 AND "reviews_count" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coverage_source",
                table: "ParserCurrentProductReviewsSummaries");

            migrationBuilder.DropColumn(
                name: "coverage_status",
                table: "ParserCurrentProductReviewsSummaries");

            migrationBuilder.DropColumn(
                name: "fetched_reviews_count",
                table: "ParserCurrentProductReviewsSummaries");

            migrationBuilder.DropColumn(
                name: "last_coverage_error",
                table: "ParserCurrentProductReviewsSummaries");

            migrationBuilder.DropColumn(
                name: "marketplace_feedback_count",
                table: "ParserCurrentProductReviewsSummaries");

            migrationBuilder.DropColumn(
                name: "oldest_review_date_utc",
                table: "ParserCurrentProductReviewsSummaries");
        }
    }
}
