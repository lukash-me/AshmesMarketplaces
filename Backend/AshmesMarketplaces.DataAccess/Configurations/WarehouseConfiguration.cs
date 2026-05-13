using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    private const int NameMaxLength = 255;
    private const int ShortTextMaxLength = 128;
    private const int AddressMaxLength = 512;
    private const int CoordinateMaxLength = 64;

    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdMp)
            .IsRequired()
            .HasColumnName("id_mp");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("code");

        builder.Property(x => x.Region)
            .IsRequired()
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("region");

        builder.Property(x => x.City)
            .HasMaxLength(ShortTextMaxLength)
            .HasColumnName("city");

        builder.Property(x => x.Address)
            .HasMaxLength(AddressMaxLength)
            .HasColumnName("address");

        builder.Property(x => x.Latitude)
            .HasMaxLength(CoordinateMaxLength)
            .HasColumnName("latitude");

        builder.Property(x => x.Longitude)
            .HasMaxLength(CoordinateMaxLength)
            .HasColumnName("longitude");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Marketplace>()
            .WithMany()
            .HasForeignKey(x => x.IdMp)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
