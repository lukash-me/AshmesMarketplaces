using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Rules;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .HasColumnName("id");

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

        builder.Property(x => x.IdOnMp)
            .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
            .HasColumnName("id_on_mp");

        builder.Property(x => x.SkuProduct)
            .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
            .HasColumnName("sku_product");

        builder.Property(x => x.SkuSeller)
            .IsRequired()
            .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
            .HasColumnName("sku_seller");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasMaxLength(Constants.PRODUCT_DESCRIPTION_MAX_LENGTH)
            .HasColumnName("description");

        builder.Property(x => x.Characteristics)
            .HasColumnType("jsonb")
            .HasColumnName("characteristics");

        builder.Property(x => x.Barcode)
            .HasMaxLength(Constants.BARCODE_MAX_LENGTH)
            .HasColumnName("barcode");

        builder.Property(x => x.Commission)
            .HasColumnName("commission");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("status");

        builder.Property(x => x.DateCreated)
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdated)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Marketplace>()
            .WithMany()
            .HasForeignKey(x => x.IdMp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(x => x.IdBrand)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.IdCategory)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RuleSet>()
            .WithMany()
            .HasForeignKey(x => x.IdSetPrice)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Videos)
            .WithOne()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Videos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
