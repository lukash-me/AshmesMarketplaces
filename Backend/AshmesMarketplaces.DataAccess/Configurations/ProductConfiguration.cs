using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.IdMp)
            .IsRequired()
            .HasColumnName("id_mp");

        builder.Property(x => x.IdSetPrice)
            .HasColumnName("id_set_price");
        
        builder.Property(x => x.IdWorkspace)
            .HasColumnName("id_workspace");

        builder.Property(x => x.IdBrand)
            .HasColumnName("id_brand");

        builder.Property(x => x.IdUser)
            .HasColumnName("id_user");

        builder.Property(x => x.IdCategory)
            .HasColumnName("id_category");
        
        builder.Property(x => x.SkuProduct)
            .HasColumnName("sku_product");

        builder.Property(x => x.SkuSeller)
            .IsRequired()
            .HasColumnName("sku_seller");
        
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasMaxLength(Constants.PRODUCT_DESCRIPTION_MAX_LENGTH)
            .HasColumnName("description");

        builder.Property(x => x.Barcode)
            .HasMaxLength(Constants.BARCODE_MAX_LENGTH)
            .HasColumnName("barcode");
        
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("status");
        
        builder.Property(x => x.CharacteristicsJson)
            .HasColumnType("jsonb")
            .HasColumnName("characteristics_json");
        
        builder.Property(x => x.DateCreated)
            .HasColumnName("date_created");

        builder.Property(x => x.DateUpdated)
            .IsRequired()
            .HasColumnName("date_updated");
        
        builder.OwnsMany(
            x => x.Images,
            b =>
            {
                b.ToTable("ProductImages");

                b.WithOwner()
                    .HasForeignKey("ProductId");
                
                b.Property<Guid>("ProductId")
                    .HasColumnName("id_product");

                b.HasKey("Id");

                b.Property(x => x.Url)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnName("url");

                b.Property(x => x.SortOrder)
                    .IsRequired()
                    .HasColumnName("sort_order");

                b.Property(x => x.Id)
                    .HasColumnName("id");
            });
        
        builder.Navigation(x => x.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        
        builder.OwnsMany(
            x => x.Videos,
            b =>
            {
                b.ToTable("ProductVideos");

                b.WithOwner()
                    .HasForeignKey("id_product");
                
                b.Property<Guid>("ProductId")
                    .HasColumnName("product_id");

                b.HasKey("Id");

                b.Property(x => x.Url)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnName("url");

                b.Property(x => x.Id)
                    .HasColumnName("id");
            });

        builder.Navigation(x => x.Videos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        
        builder.ToTable("Products");
    }
}