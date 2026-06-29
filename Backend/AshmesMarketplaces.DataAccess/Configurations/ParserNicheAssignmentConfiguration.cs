using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserNicheAssignmentConfiguration : IEntityTypeConfiguration<ParserNicheAssignment>
{
    public void Configure(EntityTypeBuilder<ParserNicheAssignment> builder)
    {
        builder.ToTable("ParserNicheAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.ProxyKey).IsRequired().HasMaxLength(160).HasColumnName("proxy_key");
        builder.Property(x => x.IsEnabled).IsRequired().HasColumnName("is_enabled");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.ParserInstanceId, x.SourceCategory, x.SourceSubcategory }).IsUnique();
        builder.HasIndex(x => x.ProxyKey);
        builder.HasIndex(x => x.IsEnabled);
    }
}
