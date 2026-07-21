using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateParserReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ParserReviewReplyRows" AS row
                USING (
                    SELECT id,
                        row_number() OVER (
                            PARTITION BY marketplace, wb_product_id, review_id_on_mp, coalesce(reply_id_on_mp, reply_fallback_hash)
                            ORDER BY parsed_at_utc, source_line_number, id
                        ) AS duplicate_number
                    FROM "ParserReviewReplyRows"
                ) AS ranked
                WHERE row.id = ranked.id AND ranked.duplicate_number > 1;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "ParserReviewRows" AS row
                USING (
                    SELECT id,
                        row_number() OVER (
                            PARTITION BY marketplace, wb_product_id, review_id_on_mp
                            ORDER BY parsed_at_utc, source_line_number, id
                        ) AS duplicate_number
                    FROM "ParserReviewRows"
                ) AS ranked
                WHERE row.id = ranked.id AND ranked.duplicate_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_ParserReviewRows_marketplace_product_review",
                table: "ParserReviewRows",
                columns: new[] { "marketplace", "wb_product_id", "review_id_on_mp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ParserReviewReplyRows_marketplace_product_review_fallback",
                table: "ParserReviewReplyRows",
                columns: new[] { "marketplace", "wb_product_id", "review_id_on_mp", "reply_fallback_hash" },
                unique: true,
                filter: "reply_fallback_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ParserReviewReplyRows_marketplace_product_review_reply",
                table: "ParserReviewReplyRows",
                columns: new[] { "marketplace", "wb_product_id", "review_id_on_mp", "reply_id_on_mp" },
                unique: true,
                filter: "reply_id_on_mp IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ParserReviewRows_marketplace_product_review",
                table: "ParserReviewRows");

            migrationBuilder.DropIndex(
                name: "UX_ParserReviewReplyRows_marketplace_product_review_fallback",
                table: "ParserReviewReplyRows");

            migrationBuilder.DropIndex(
                name: "UX_ParserReviewReplyRows_marketplace_product_review_reply",
                table: "ParserReviewReplyRows");
        }
    }
}
