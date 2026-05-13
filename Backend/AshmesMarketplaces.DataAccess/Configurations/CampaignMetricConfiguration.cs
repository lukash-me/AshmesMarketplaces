using AshmesMarketplaces.Domain.Entities.Advertising;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class CampaignMetricConfiguration : IEntityTypeConfiguration<CampaignMetric>
{
    public void Configure(EntityTypeBuilder<CampaignMetric> builder)
    {
        builder.ToTable("Metrics_Campaign");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdCampaign)
            .IsRequired()
            .HasColumnName("id_campaign");

        builder.Property(x => x.ImpressionAmount)
            .HasColumnName("impression_amount");

        builder.Property(x => x.ClicksAmount)
            .HasColumnName("clicks_amount");

        builder.Property(x => x.CostDay)
            .HasPrecision(18, 2)
            .HasColumnName("cost_day");

        builder.Property(x => x.Date)
            .IsRequired()
            .HasColumnName("date");

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(x => x.IdCampaign)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
