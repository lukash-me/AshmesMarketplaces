using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class MarketHotProductRecommendationConfiguration : IEntityTypeConfiguration<MarketHotProductRecommendation>
{
    public void Configure(EntityTypeBuilder<MarketHotProductRecommendation> builder)
    {
        builder.ToTable("MarketHotProductRecommendations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdMarketRecommendationRun).IsRequired().HasColumnName("id_market_recommendation_run");
        builder.Property(x => x.RecommendationKey).IsRequired().HasMaxLength(128).HasColumnName("recommendation_key");
        builder.Property(x => x.ProductKey).IsRequired().HasMaxLength(256).HasColumnName("product_key");
        builder.Property(x => x.WbProductId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.IdParserProductRow).HasColumnName("id_parser_product_row");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH).HasColumnName("product_name");
        builder.Property(x => x.BrandName).HasMaxLength(512).HasColumnName("brand_name");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.PriceSnapshot).HasPrecision(18, 2).HasColumnName("price_snapshot");
        builder.Property(x => x.PriceWithoutDiscountSnapshot).HasPrecision(18, 2).HasColumnName("price_without_discount_snapshot");
        builder.Property(x => x.WalletPriceSnapshot).HasPrecision(18, 2).HasColumnName("wallet_price_snapshot");
        builder.Property(x => x.RatingSnapshot).HasPrecision(18, 6).HasColumnName("rating_snapshot");
        builder.Property(x => x.FeedbackCountSnapshot).HasColumnName("feedback_count_snapshot");
        builder.Property(x => x.ParsedReviewCountSnapshot).HasColumnName("parsed_review_count_snapshot");
        builder.Property(x => x.ParsedReplyCountSnapshot).HasColumnName("parsed_reply_count_snapshot");
        builder.Property(x => x.PositionSnapshot).HasColumnName("position_snapshot");
        builder.Property(x => x.PositionState).HasMaxLength(64).HasColumnName("position_state");
        builder.Property(x => x.ObservedRangeLimit).HasColumnName("observed_range_limit");
        builder.Property(x => x.TotalQuantitySnapshot).HasColumnName("total_quantity_snapshot");
        builder.Property(x => x.Score).IsRequired().HasPrecision(18, 6).HasColumnName("score");
        builder.Property(x => x.Confidence).IsRequired().HasPrecision(18, 6).HasColumnName("confidence");
        builder.Property(x => x.Title).IsRequired().HasMaxLength(255).HasColumnName("title");
        builder.Property(x => x.Reason).IsRequired().HasColumnName("reason");
        builder.Property(x => x.InputSnapshotHash).IsRequired().HasMaxLength(64).HasColumnName("input_snapshot_hash");
        builder.Property(x => x.ValidUntilUtc).IsRequired().HasColumnName("valid_until_utc");
        builder.Property(x => x.RankOrder).IsRequired().HasColumnName("rank_order");
        builder.Property(x => x.Factors).IsRequired().HasColumnType("jsonb").HasColumnName("factors");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.IdMarketRecommendationRun, x.RankOrder });
        builder.HasIndex(x => new { x.IdMarketRecommendationRun, x.RecommendationKey }).IsUnique();
        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => x.WbRootId);
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory, x.Score });

        builder.HasOne<MarketRecommendationRun>()
            .WithMany()
            .HasForeignKey(x => x.IdMarketRecommendationRun)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParserProductRow>()
            .WithMany()
            .HasForeignKey(x => x.IdParserProductRow)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
