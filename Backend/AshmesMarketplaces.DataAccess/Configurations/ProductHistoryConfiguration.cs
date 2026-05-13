using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ProductHistoryConfiguration : IEntityTypeConfiguration<ProductHistory>
{
    public void Configure(EntityTypeBuilder<ProductHistory> builder)
    {
        builder.ToTable("Products_Historical");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.Cost)
            .HasPrecision(18, 2)
            .HasColumnName("cost");

        builder.Property(x => x.Price)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnName("price");

        builder.Property(x => x.Discount)
            .IsRequired()
            .HasColumnName("discount");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(x => x.IsAutoDiscountActive)
            .HasColumnName("is_auto_discount_active");

        builder.Property(x => x.Date)
            .IsRequired()
            .HasColumnName("date");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
