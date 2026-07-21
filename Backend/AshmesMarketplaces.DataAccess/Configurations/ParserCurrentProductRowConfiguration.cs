using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserCurrentProductRowConfiguration : IEntityTypeConfiguration<ParserCurrentProductRow>
{
    public void Configure(EntityTypeBuilder<ParserCurrentProductRow> builder)
    {
        builder.ToTable("ParserCurrentProductRows");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ProductRowId).IsRequired().HasColumnName("product_row_id");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.ParsedAtUtc).IsRequired().HasColumnName("parsed_at_utc");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH).HasColumnName("name");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest");
        builder.Property(x => x.BrandIdOnMp).HasColumnName("brand_id_on_mp");
        builder.Property(x => x.BrandName).HasMaxLength(512).HasColumnName("brand_name");
        builder.Property(x => x.SellerIdOnMp).HasColumnName("seller_id_on_mp");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.PriceRegular).HasPrecision(18, 2).HasColumnName("price_regular");
        builder.Property(x => x.PriceDiscounted).HasPrecision(18, 2).HasColumnName("price_discounted");
        builder.Property(x => x.PriceWbWallet).HasPrecision(18, 2).HasColumnName("price_wb_wallet");
        builder.Property(x => x.DiscountPercent).HasPrecision(18, 6).HasColumnName("discount_percent");
        builder.Property(x => x.TotalQuantity).HasColumnName("total_quantity");
        builder.Property(x => x.RatingRounded).HasPrecision(18, 6).HasColumnName("rating_rounded");
        builder.Property(x => x.ReviewRating).HasPrecision(18, 6).HasColumnName("review_rating");
        builder.Property(x => x.FeedbackCount).HasColumnName("feedback_count");
        builder.Property(x => x.FeedbackCountSource).HasMaxLength(80).HasColumnName("feedback_count_source");
        builder.Property(x => x.ImageCount).HasColumnName("image_count");
        builder.Property(x => x.ImageUrlsJson).HasColumnType("jsonb").HasColumnName("image_urls_json");
        builder.Property(x => x.PositionState).HasMaxLength(64).HasColumnName("position_state");
        builder.Property(x => x.PositionAbsolute).HasColumnName("position_absolute");
        builder.Property(x => x.PositionObservedRangeLimit).HasColumnName("position_observed_range_limit");
        builder.Property(x => x.PositionQuery).HasMaxLength(512).HasColumnName("position_query");
        builder.Property(x => x.PositionObservedAtUtc).HasColumnName("position_observed_at_utc");
        builder.Property(x => x.IdentityHash).HasMaxLength(128).HasColumnName("identity_hash");
        builder.Property(x => x.PriceHash).HasMaxLength(128).HasColumnName("price_hash");
        builder.Property(x => x.StockHash).HasMaxLength(128).HasColumnName("stock_hash");
        builder.Property(x => x.RatingHash).HasMaxLength(128).HasColumnName("rating_hash");
        builder.Property(x => x.ReviewsHash).HasMaxLength(128).HasColumnName("reviews_hash");
        builder.Property(x => x.MediaHash).HasMaxLength(128).HasColumnName("media_hash");
        builder.Property(x => x.SellerBrandHash).HasMaxLength(128).HasColumnName("seller_brand_hash");
        builder.Property(x => x.MarketplacePresenceStatus).IsRequired().HasMaxLength(64).HasColumnName("marketplace_presence_status");
        builder.Property(x => x.LastSeenParserCycleId).HasMaxLength(200).HasColumnName("last_seen_parser_cycle_id");
        builder.Property(x => x.LastSeenParserProxyRunId).HasMaxLength(80).HasColumnName("last_seen_parser_proxy_run_id");
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(x => x.LastPresenceCheckedParserCycleId).HasMaxLength(200).HasColumnName("last_presence_checked_parser_cycle_id");
        builder.Property(x => x.LastMissingDetectedAtUtc).HasColumnName("last_missing_detected_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ProductRowId).IsUnique();
        builder.HasIndex(x => x.WbProductId).IsUnique();
        builder.HasIndex(x => x.WbRootId);
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory });
        builder.HasIndex(x => x.BrandName);
        builder.HasIndex(x => x.SellerName);
        builder.HasIndex(x => x.ParsedAtUtc);
        builder.HasIndex(x => x.PriceDiscounted);
        builder.HasIndex(x => x.ReviewRating);
        builder.HasIndex(x => x.FeedbackCount);
        builder.HasIndex(x => new { x.PositionState, x.PositionAbsolute, x.PositionObservedRangeLimit });
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory, x.MarketplacePresenceStatus, x.LastSeenParserCycleId });
        builder.HasIndex(x => x.LastSeenAtUtc);
    }
}
