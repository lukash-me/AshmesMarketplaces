using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private const int NameMaxLength = 255;
    private const int ExternalIdMaxLength = 128;

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdParentCategory)
            .HasColumnName("id_parent_category");

        builder.Property(x => x.IdOnMp)
            .HasMaxLength(ExternalIdMaxLength)
            .HasColumnName("id_on_mp");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Level)
            .IsRequired()
            .HasColumnName("level");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.IdParentCategory)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
