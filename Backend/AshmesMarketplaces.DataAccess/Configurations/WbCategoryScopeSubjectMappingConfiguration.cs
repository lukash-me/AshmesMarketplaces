using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WbCategoryScopeSubjectMappingConfiguration : IEntityTypeConfiguration<WbCategoryScopeSubjectMapping>
{
    public void Configure(EntityTypeBuilder<WbCategoryScopeSubjectMapping> builder)
    {
        builder.ToTable("WbCategoryScopeSubjectMappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbMenuId).IsRequired().HasColumnName("wb_menu_id");
        builder.Property(x => x.MenuToken).IsRequired().HasMaxLength(255).HasColumnName("menu_token");
        builder.Property(x => x.SourcePath).IsRequired().HasMaxLength(1000).HasColumnName("source_path");
        builder.Property(x => x.SubjectId).IsRequired().HasColumnName("subject_id");
        builder.Property(x => x.SubjectName).HasMaxLength(500).HasColumnName("subject_name");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.MappingSource).IsRequired().HasMaxLength(64).HasColumnName("mapping_source");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.WbMenuId, x.SubjectId }).IsUnique();
        builder.HasIndex(x => new { x.WbMenuId, x.Status });
        builder.HasIndex(x => x.SourcePath);
    }
}
