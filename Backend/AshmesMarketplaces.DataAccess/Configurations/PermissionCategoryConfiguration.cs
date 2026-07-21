using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class PermissionCategoryConfiguration : IEntityTypeConfiguration<PermissionCategory>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<PermissionCategory> builder)
    {
        builder.ToTable("Permissions_Categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");
    }
}
