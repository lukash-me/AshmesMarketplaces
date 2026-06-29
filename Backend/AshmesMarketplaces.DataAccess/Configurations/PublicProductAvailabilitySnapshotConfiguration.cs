using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicProductAvailabilitySnapshotConfiguration : IEntityTypeConfiguration<PublicProductAvailabilitySnapshot>
{
    public void Configure(EntityTypeBuilder<PublicProductAvailabilitySnapshot> builder)
    {
        builder.ToTable("PublicProductAvailabilitySnapshots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.SnapshotKey).IsRequired().HasMaxLength(64).HasColumnName("snapshot_key");
        builder.Property(x => x.ItemsJson).IsRequired().HasColumnType("jsonb").HasColumnName("items_json");
        builder.Property(x => x.TotalCount).IsRequired().HasColumnName("total_count");
        builder.Property(x => x.CalculatedAtUtc).HasColumnName("calculated_at_utc");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.SnapshotKey).IsUnique();
        builder.HasIndex(x => x.CalculatedAtUtc);
    }
}
