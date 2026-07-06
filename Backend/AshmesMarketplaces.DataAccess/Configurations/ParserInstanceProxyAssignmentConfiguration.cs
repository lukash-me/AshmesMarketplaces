using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserInstanceProxyAssignmentConfiguration : IEntityTypeConfiguration<ParserInstanceProxyAssignment>
{
    public void Configure(EntityTypeBuilder<ParserInstanceProxyAssignment> builder)
    {
        builder.ToTable("ParserInstanceProxyAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceConfigurationId).IsRequired().HasColumnName("parser_instance_configuration_id");
        builder.Property(x => x.ProxyId).IsRequired().HasColumnName("proxy_id");
        builder.Property(x => x.Enabled).IsRequired().HasColumnName("enabled");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasOne(x => x.Proxy)
            .WithMany()
            .HasForeignKey(x => x.ProxyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ParserInstanceConfigurationId);
        builder.HasIndex(x => x.ProxyId)
            .IsUnique()
            .HasFilter("enabled = true");
        builder.HasIndex(x => x.Enabled);
    }
}
