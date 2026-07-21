using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260629170000_ParserProductCdc")]
    public partial class ParserProductCdc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "brand_id_on_mp",
                table: "ParserCurrentProductRows",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                table: "ParserCurrentProductRows",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feedback_count_source",
                table: "ParserCurrentProductRows",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_count",
                table: "ParserCurrentProductRows",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_urls_json",
                table: "ParserCurrentProductRows",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "media_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rating_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviews_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seller_brand_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "seller_id_on_mp",
                table: "ParserCurrentProductRows",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stock_hash",
                table: "ParserCurrentProductRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rating_rounded",
                table: "ParserCurrentProductRows",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParserCurrentProductDetails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    details_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserCurrentProductDetails", x => x.id));

            migrationBuilder.CreateTable(
                name: "ParserCurrentProductLogistics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    logistics_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    logistics_json = table.Column<string>(type: "jsonb", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserCurrentProductLogistics", x => x.id));

            migrationBuilder.CreateTable(
                name: "ParserCurrentProductRanks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    rank_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rank_json = table.Column<string>(type: "jsonb", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserCurrentProductRanks", x => x.id));

            migrationBuilder.CreateTable(
                name: "ParserCurrentProductReviewsSummaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reviews_count = table.Column<int>(type: "integer", nullable: false),
                    average_rating = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    recent_negative_count = table.Column<int>(type: "integer", nullable: false),
                    last_review_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviews_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reviews_json = table.Column<string>(type: "jsonb", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserCurrentProductReviewsSummaries", x => x.id));

            migrationBuilder.CreateTable(
                name: "ParserCurrentProductReviewEvidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    review_id_on_mp = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    review_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    review_json = table.Column<string>(type: "jsonb", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    created_at_on_mp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserCurrentProductReviewEvidence", x => x.id));

            migrationBuilder.CreateTable(
                name: "ParserProductChangeEvents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    field_group = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    change_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    old_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    new_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ParserProductChangeEvents", x => x.id));

            migrationBuilder.CreateIndex("IX_ParserCurrentProductDetails_source_category_source_subcategory", "ParserCurrentProductDetails", new[] { "source_category", "source_subcategory" });
            migrationBuilder.CreateIndex("IX_ParserCurrentProductDetails_wb_product_id", "ParserCurrentProductDetails", "wb_product_id", unique: true);
            migrationBuilder.CreateIndex("IX_ParserCurrentProductLogistics_source_category_source_subcategory", "ParserCurrentProductLogistics", new[] { "source_category", "source_subcategory" });
            migrationBuilder.CreateIndex("IX_ParserCurrentProductLogistics_wb_product_id", "ParserCurrentProductLogistics", "wb_product_id", unique: true);
            migrationBuilder.CreateIndex("IX_ParserCurrentProductRanks_source_category_source_subcategory", "ParserCurrentProductRanks", new[] { "source_category", "source_subcategory" });
            migrationBuilder.CreateIndex("IX_ParserCurrentProductRanks_wb_product_id", "ParserCurrentProductRanks", "wb_product_id", unique: true);
            migrationBuilder.CreateIndex("IX_ParserCurrentProductReviewsSummaries_source_category_source_subcategory", "ParserCurrentProductReviewsSummaries", new[] { "source_category", "source_subcategory" });
            migrationBuilder.CreateIndex("IX_ParserCurrentProductReviewsSummaries_wb_product_id", "ParserCurrentProductReviewsSummaries", "wb_product_id", unique: true);
            migrationBuilder.CreateIndex("IX_ParserCurrentProductReviewEvidence_observed_at_utc", "ParserCurrentProductReviewEvidence", "observed_at_utc");
            migrationBuilder.CreateIndex("IX_ParserCurrentProductReviewEvidence_wb_product_id", "ParserCurrentProductReviewEvidence", "wb_product_id");
            migrationBuilder.CreateIndex("IX_ParserCurrentProductReviewEvidence_wb_product_id_review_id_on_mp", "ParserCurrentProductReviewEvidence", new[] { "wb_product_id", "review_id_on_mp" }, unique: true);
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_batch_id_wb_product_id_field_group_new_hash", "ParserProductChangeEvents", new[] { "batch_id", "wb_product_id", "field_group", "new_hash" }, unique: true);
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_observed_at_utc", "ParserProductChangeEvents", "observed_at_utc");
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_source_category_source_subcategory", "ParserProductChangeEvents", new[] { "source_category", "source_subcategory" });
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_source_subcategory_field_group_observed_at_utc", "ParserProductChangeEvents", new[] { "source_subcategory", "field_group", "observed_at_utc" });
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_wb_product_id", "ParserProductChangeEvents", "wb_product_id");
            migrationBuilder.CreateIndex("IX_ParserProductChangeEvents_wb_product_id_field_group_observed_at_utc", "ParserProductChangeEvents", new[] { "wb_product_id", "field_group", "observed_at_utc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ParserCurrentProductDetails");
            migrationBuilder.DropTable(name: "ParserCurrentProductLogistics");
            migrationBuilder.DropTable(name: "ParserCurrentProductRanks");
            migrationBuilder.DropTable(name: "ParserCurrentProductReviewEvidence");
            migrationBuilder.DropTable(name: "ParserCurrentProductReviewsSummaries");
            migrationBuilder.DropTable(name: "ParserProductChangeEvents");

            migrationBuilder.DropColumn(name: "identity_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "brand_id_on_mp", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "discount_percent", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "feedback_count_source", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "image_count", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "image_urls_json", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "media_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "price_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "rating_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "rating_rounded", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "reviews_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "seller_brand_hash", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "seller_id_on_mp", table: "ParserCurrentProductRows");
            migrationBuilder.DropColumn(name: "stock_hash", table: "ParserCurrentProductRows");
        }
    }
}
