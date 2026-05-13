using AshmesMarketplaces.Domain.Entities.Logistics;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class LogisticConfiguration : IEntityTypeConfiguration<Logistic>
{
    public void Configure(EntityTypeBuilder<Logistic> builder)
    {
        builder.ToTable("Logistics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.IdWarehouse)
            .HasColumnName("id_warehouse");

        builder.Property(x => x.StockAmount)
            .HasColumnName("stock_amount");

        builder.Property(x => x.StockAmountStatistic)
            .IsRequired()
            .HasColumnName("stock_amount_statistic");

        builder.Property(x => x.StockInTransit)
            .HasColumnName("stock_in_transit");

        builder.Property(x => x.CostStorage)
            .HasPrecision(18, 2)
            .HasColumnName("cost_storage");

        builder.Property(x => x.CostLogistic)
            .HasPrecision(18, 2)
            .HasColumnName("cost_logistic");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(x => x.Date)
            .IsRequired()
            .HasColumnName("date");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.IdWarehouse)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
