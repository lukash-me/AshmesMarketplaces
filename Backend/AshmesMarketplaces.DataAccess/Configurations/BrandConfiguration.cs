using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    private const int NameMaxLength = 255;
    private const int ShortTextMaxLength = 128;

    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.IsVerified)
            .IsRequired()
            .HasColumnName("is_verified");

        builder.Property(x => x.Country)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("country");

        builder.Property(x => x.Manufacturer)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("manufacturer");

        builder.Property(x => x.SalesAmount)
            .IsRequired()
            .HasColumnName("sales_amount");

        builder.Property(x => x.RateRedemption)
            .IsRequired()
            .HasColumnName("rate_redemption");

        builder.Property(x => x.Level)
            .IsRequired()
            .HasColumnName("level");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(x => x.DateMpRegistration)
            .IsRequired()
            .HasColumnName("date_mp_registration");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");
    }
}
