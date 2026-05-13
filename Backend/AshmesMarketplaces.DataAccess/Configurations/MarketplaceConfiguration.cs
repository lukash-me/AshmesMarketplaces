using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class MarketplaceConfiguration : IEntityTypeConfiguration<Marketplace>
{
    private const int NameMaxLength = 255;
    private const int UrlMaxLength = 2048;
    private const int ShortTextMaxLength = 128;
    private const int CurrencyMaxLength = 32;

    public void Configure(EntityTypeBuilder<Marketplace> builder)
    {
        builder.ToTable("Marketplaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.ApiUrl)
            .IsRequired()
            .HasMaxLength(UrlMaxLength)
            .HasColumnName("api_url");

        builder.Property(x => x.ApiVersion)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("api_version");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(CurrencyMaxLength)
            .HasColumnName("currency");

        builder.Property(x => x.Region)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("region");

        builder.Property(x => x.TypeCommission)
            .IsRequired()
            .HasColumnName("type_commission");

        builder.Property(x => x.SchemeDelivery)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("scheme_delivery");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");
    }
}
