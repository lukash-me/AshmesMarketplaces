using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProxyNicheAssignmentConfiguration : IEntityTypeConfiguration<ParserProxyNicheAssignment>
{
    public void Configure(EntityTypeBuilder<ParserProxyNicheAssignment> builder)
    {
        builder.ToTable("ParserProxyNicheAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ProxyId).IsRequired().HasColumnName("proxy_id");
        builder.Property(x => x.WbCategoryId).IsRequired().HasColumnName("wb_category_id");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.SourcePath).IsRequired().HasMaxLength(1000).HasColumnName("source_path");
        builder.Property(x => x.SearchQuery).IsRequired().HasMaxLength(1000).HasColumnName("search_query");
        builder.Property(x => x.ParserSearchText).IsRequired().HasMaxLength(255).HasColumnName("parser_search_text");
        builder.Property(x => x.Enabled).IsRequired().HasColumnName("enabled");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasOne(x => x.Proxy)
            .WithOne(x => x.Assignment)
            .HasForeignKey<ParserProxyNicheAssignment>(x => x.ProxyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProxyId).IsUnique();
        builder.HasIndex(x => x.WbCategoryId);
        builder.HasIndex(x => x.Enabled);
    }
}
