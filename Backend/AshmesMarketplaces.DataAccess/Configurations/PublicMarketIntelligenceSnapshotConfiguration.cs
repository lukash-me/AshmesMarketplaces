using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicMarketIntelligenceSnapshotConfiguration : IEntityTypeConfiguration<PublicMarketIntelligenceSnapshot>
{
    public void Configure(EntityTypeBuilder<PublicMarketIntelligenceSnapshot> builder)
    {
        builder.ToTable("PublicMarketIntelligenceSnapshots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.Query).IsRequired().HasMaxLength(512).HasColumnName("query");
        builder.Property(x => x.SourceRegionDest).IsRequired().HasMaxLength(128).HasColumnName("source_region_dest");
        builder.Property(x => x.Sort).IsRequired().HasMaxLength(64).HasColumnName("sort");
        builder.Property(x => x.TopN).IsRequired().HasColumnName("top_n");
        builder.Property(x => x.PublicMarketIntelligenceJson).IsRequired().HasColumnType("jsonb").HasColumnName("public_market_intelligence_json");
        builder.Property(x => x.SampleSize).IsRequired().HasColumnName("sample_size");
        builder.Property(x => x.LatestObservedAtUtc).HasColumnName("latest_observed_at_utc");
        builder.Property(x => x.CalculatedAtUtc).HasColumnName("calculated_at_utc");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory, x.Query, x.SourceRegionDest, x.Sort, x.TopN })
            .IsUnique()
            .HasDatabaseName("IX_PublicMarketIntelligenceSnapshots_context");
        builder.HasIndex(x => x.CalculatedAtUtc);
    }
}
