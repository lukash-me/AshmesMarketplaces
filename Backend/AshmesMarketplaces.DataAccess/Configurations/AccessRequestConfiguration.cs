using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("AccessRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.Contact).IsRequired().HasMaxLength(320).HasColumnName("contact");
        builder.Property(x => x.Comment).IsRequired().HasMaxLength(2000).HasColumnName("comment");
        builder.Property(x => x.IpAddress).HasMaxLength(96).HasColumnName("ip_address");
        builder.Property(x => x.UserAgent).HasMaxLength(512).HasColumnName("user_agent");
        builder.Property(x => x.SourcePath).HasMaxLength(512).HasColumnName("source_path");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.Status);
    }
}
