using AshmesMarketplaces.Domain.Entities.Orders;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    private const int LocationMaxLength = 255;

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.Price)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnName("price");

        builder.Property(x => x.Discount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnName("discount");

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnName("amount");

        builder.Property(x => x.LocationSource)
            .HasMaxLength(LocationMaxLength)
            .HasColumnName("location_source");

        builder.Property(x => x.LocationDestination)
            .HasMaxLength(LocationMaxLength)
            .HasColumnName("location_destination");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DateDelivered)
            .HasColumnName("date_delivered");

        builder.Property(x => x.DateOpened)
            .IsRequired()
            .HasColumnName("date_opened");

        builder.Property(x => x.DateClosed)
            .HasColumnName("date_closed");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
