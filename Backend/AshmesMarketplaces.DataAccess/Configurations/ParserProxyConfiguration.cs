using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProxyConfiguration : IEntityTypeConfiguration<ParserProxy>
{
    public void Configure(EntityTypeBuilder<ParserProxy> builder)
    {
        builder.ToTable("ParserProxies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.Key).IsRequired().HasMaxLength(80).HasColumnName("key");
        builder.Property(x => x.Ip).IsRequired().HasMaxLength(255).HasColumnName("ip");
        builder.Property(x => x.HttpPort).IsRequired().HasColumnName("http_port");
        builder.Property(x => x.SocksPort).IsRequired().HasColumnName("socks_port");
        builder.Property(x => x.Login).IsRequired().HasMaxLength(255).HasColumnName("login");
        builder.Property(x => x.EncryptedPassword).IsRequired().HasColumnName("encrypted_password");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
