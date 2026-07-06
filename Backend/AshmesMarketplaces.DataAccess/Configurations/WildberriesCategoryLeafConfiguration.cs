using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WildberriesCategoryLeafConfiguration : IEntityTypeConfiguration<WildberriesCategoryLeaf>
{
    public void Configure(EntityTypeBuilder<WildberriesCategoryLeaf> builder)
    {
        builder.ToTable("WildberriesCategoryLeaves");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbCategoryId).IsRequired().HasColumnName("wb_category_id");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(500).HasColumnName("name");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(500).HasColumnName("source_subcategory");
        builder.Property(x => x.SourcePath).IsRequired().HasMaxLength(1200).HasColumnName("source_path");
        builder.Property(x => x.SearchQuery).HasMaxLength(1000).HasColumnName("search_query");
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.IsLeaf).IsRequired().HasColumnName("is_leaf");
        builder.Property(x => x.Level).IsRequired().HasColumnName("level");
        builder.Property(x => x.FetchedAtUtc).IsRequired().HasColumnName("fetched_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.WbCategoryId).IsUnique();
        builder.HasIndex(x => x.SourceCategory);
        builder.HasIndex(x => x.SourceSubcategory);
        builder.HasIndex(x => x.SourcePath);
    }
}
