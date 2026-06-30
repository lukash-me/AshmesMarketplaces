using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserCurrentProductReviewsSummaryConfiguration : IEntityTypeConfiguration<ParserCurrentProductReviewsSummary>
{
    public void Configure(EntityTypeBuilder<ParserCurrentProductReviewsSummary> builder)
    {
        builder.ToTable("ParserCurrentProductReviewsSummaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.ReviewsCount).HasColumnName("reviews_count");
        builder.Property(x => x.AverageRating).HasPrecision(18, 6).HasColumnName("average_rating");
        builder.Property(x => x.RecentNegativeCount).HasColumnName("recent_negative_count");
        builder.Property(x => x.LastReviewDateUtc).HasColumnName("last_review_date_utc");
        builder.Property(x => x.ReviewsHash).IsRequired().HasMaxLength(128).HasColumnName("reviews_hash");
        builder.Property(x => x.ReviewsJson).IsRequired().HasColumnType("jsonb").HasColumnName("reviews_json");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(200).HasColumnName("batch_id");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.WbProductId).IsUnique();
    }
}
