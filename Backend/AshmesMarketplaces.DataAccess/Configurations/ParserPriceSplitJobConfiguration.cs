using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserPriceSplitJobConfiguration : IEntityTypeConfiguration<ParserPriceSplitJob>
{
    public void Configure(EntityTypeBuilder<ParserPriceSplitJob> builder)
    {
        builder.ToTable("ParserPriceSplitJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.ProxyKey).IsRequired().HasMaxLength(120).HasColumnName("proxy_key");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.MinPriceU).IsRequired().HasColumnName("min_price_u");
        builder.Property(x => x.MaxPriceU).IsRequired().HasColumnName("max_price_u");
        builder.Property(x => x.TotalRangesCount).IsRequired().HasColumnName("total_ranges_count");
        builder.Property(x => x.CompletedRangesCount).IsRequired().HasColumnName("completed_ranges_count");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.ParserInstanceId, x.ProxyKey, x.SourceCategory, x.SourceSubcategory, x.Status });
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory, x.ProxyKey, x.Status });
    }
}
