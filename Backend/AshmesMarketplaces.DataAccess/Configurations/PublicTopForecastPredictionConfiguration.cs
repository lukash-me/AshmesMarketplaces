using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicTopForecastPredictionConfiguration : IEntityTypeConfiguration<PublicTopForecastPrediction>
{
    public void Configure(EntityTypeBuilder<PublicTopForecastPrediction> builder)
    {
        builder.ToTable("PublicTopForecastPredictions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdRun).IsRequired().HasColumnName("id_run");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.Query).IsRequired().HasMaxLength(512).HasColumnName("query");
        builder.Property(x => x.SourceRegionDest).IsRequired().HasMaxLength(128).HasColumnName("source_region_dest");
        builder.Property(x => x.Sort).IsRequired().HasMaxLength(64).HasColumnName("sort");
        builder.Property(x => x.TopN).IsRequired().HasColumnName("top_n");
        builder.Property(x => x.ProductRowId).HasColumnName("product_row_id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(64).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(64).HasColumnName("wb_root_id");
        builder.Property(x => x.ProductName).HasMaxLength(1024).HasColumnName("product_name");
        builder.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(x => x.Price).HasPrecision(18, 2).HasColumnName("price");
        builder.Property(x => x.Rating).HasPrecision(6, 2).HasColumnName("rating");
        builder.Property(x => x.FeedbackCount).HasColumnName("feedback_count");
        builder.Property(x => x.Stock).HasColumnName("stock");
        builder.Property(x => x.CurrentPosition).HasColumnName("current_position");
        builder.Property(x => x.CurrentPositionState).HasMaxLength(64).HasColumnName("current_position_state");
        builder.Property(x => x.ObservedRangeLimit).HasColumnName("observed_range_limit");
        builder.Property(x => x.PredictedPosition).HasColumnName("predicted_position");
        builder.Property(x => x.Top100Probability).IsRequired().HasPrecision(9, 4).HasColumnName("top100_probability");
        builder.Property(x => x.Confidence).IsRequired().HasPrecision(9, 4).HasColumnName("confidence");
        builder.Property(x => x.FeatureCoveragePercent).IsRequired().HasPrecision(9, 2).HasColumnName("feature_coverage_percent");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.BrandName).HasMaxLength(512).HasColumnName("brand_name");
        builder.Property(x => x.ReasonsJson).IsRequired().HasColumnType("jsonb").HasColumnName("reasons_json");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory, x.Query, x.SourceRegionDest, x.Sort, x.TopN });
        builder.HasIndex(x => new { x.IdRun, x.Top100Probability });
        builder.HasIndex(x => x.ProductRowId);
        builder.HasIndex(x => x.WbProductId);
    }
}
