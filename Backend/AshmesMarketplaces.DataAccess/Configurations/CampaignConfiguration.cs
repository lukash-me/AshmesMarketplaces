using AshmesMarketplaces.Domain.Entities.Advertising;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Rules;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    private const int NameMaxLength = 255;
    private const int RegionMaxLength = 128;

    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaign");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.IdSetCampaign)
            .HasColumnName("id_set_campaign");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Budget)
            .HasPrecision(18, 2)
            .HasColumnName("budget");

        builder.Property(x => x.Region)
            .HasMaxLength(RegionMaxLength)
            .HasColumnName("region");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.TimeToImpression)
            .HasColumnType("jsonb")
            .HasColumnName("time_to_impression");

        builder.Property(x => x.DateStart)
            .HasColumnName("date_start");

        builder.Property(x => x.DateEnd)
            .HasColumnName("date_end");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RuleSet>()
            .WithMany()
            .HasForeignKey(x => x.IdSetCampaign)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
