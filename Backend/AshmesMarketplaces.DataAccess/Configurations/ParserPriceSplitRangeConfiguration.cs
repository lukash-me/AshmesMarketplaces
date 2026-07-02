using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserPriceSplitRangeConfiguration : IEntityTypeConfiguration<ParserPriceSplitRange>
{
    public void Configure(EntityTypeBuilder<ParserPriceSplitRange> builder)
    {
        builder.ToTable("ParserPriceSplitRanges");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.JobId).IsRequired().HasColumnName("job_id");
        builder.Property(x => x.ParentRangeId).HasColumnName("parent_range_id");
        builder.Property(x => x.MinPriceU).IsRequired().HasColumnName("min_price_u");
        builder.Property(x => x.MaxPriceU).IsRequired().HasColumnName("max_price_u");
        builder.Property(x => x.ExpectedTotal).HasColumnName("expected_total");
        builder.Property(x => x.NextPage).IsRequired().HasColumnName("next_page");
        builder.Property(x => x.NextItemOffset).IsRequired().HasColumnName("next_item_offset");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.AttemptsCount).IsRequired().HasColumnName("attempts_count");
        builder.Property(x => x.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        builder.Property(x => x.CooldownUntilUtc).HasColumnName("cooldown_until_utc");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.JobId, x.MinPriceU, x.MaxPriceU }).IsUnique();
        builder.HasIndex(x => new { x.JobId, x.Status, x.CooldownUntilUtc });
        builder.HasIndex(x => x.ParentRangeId);
    }
}
