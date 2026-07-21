using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserInstanceConfiguration : IEntityTypeConfiguration<ParserInstance>
{
    public void Configure(EntityTypeBuilder<ParserInstance> builder)
    {
        builder.ToTable("ParserInstances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.DisplayName).HasMaxLength(255).HasColumnName("display_name");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.LastSeenAtUtc).IsRequired().HasColumnName("last_seen_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ParserInstanceId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.LastSeenAtUtc);
    }
}
