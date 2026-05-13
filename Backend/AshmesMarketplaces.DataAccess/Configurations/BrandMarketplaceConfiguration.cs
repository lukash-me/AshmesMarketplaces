using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class BrandMarketplaceConfiguration : IEntityTypeConfiguration<BrandMarketplace>
{
    private const int ExternalIdMaxLength = 128;

    public void Configure(EntityTypeBuilder<BrandMarketplace> builder)
    {
        builder.ToTable("Brands_Marketplaces");

        builder.HasKey(x => new { x.IdMp, x.IdBrand });

        builder.Property(x => x.IdMp)
            .HasColumnName("id_mp");

        builder.Property(x => x.IdBrand)
            .HasColumnName("id_brand");

        builder.Property(x => x.IdOnMp)
            .IsRequired()
            .HasMaxLength(ExternalIdMaxLength)
            .HasColumnName("id_on_mp");

        builder.HasOne<Marketplace>()
            .WithMany()
            .HasForeignKey(x => x.IdMp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(x => x.IdBrand)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
