using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParserInstanceConfigurationEntity = AshmesMarketplaces.Domain.Entities.ParserIngestion.ParserInstanceConfiguration;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserInstanceConfigurationConfiguration : IEntityTypeConfiguration<ParserInstanceConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<ParserInstanceConfigurationEntity> builder)
    {
        builder.ToTable("ParserInstanceConfigurations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(255).HasColumnName("display_name");
        builder.Property(x => x.HostKind).IsRequired().HasMaxLength(32).HasColumnName("host_kind");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasMany(x => x.ProxyAssignments)
            .WithOne(x => x.ParserInstanceConfiguration)
            .HasForeignKey(x => x.ParserInstanceConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ParserInstanceId).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
