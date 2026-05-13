using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ProductVideoConfiguration : IEntityTypeConfiguration<ProductVideo>
{
    public void Configure(EntityTypeBuilder<ProductVideo> builder)
    {
        builder.ToTable("ProductVideos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(Constants.URL_MAX_LENGTH)
            .HasColumnName("url");

        builder.Property(x => x.SortOrder)
            .IsRequired()
            .HasColumnName("sort_order");
    }
}
