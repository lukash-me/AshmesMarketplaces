using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdCategory)
            .IsRequired()
            .HasColumnName("id_category");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.Domain)
            .IsRequired()
            .HasColumnName("domain");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<PermissionCategory>()
            .WithMany()
            .HasForeignKey(x => x.IdCategory)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
