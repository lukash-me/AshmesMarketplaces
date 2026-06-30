using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserCurrentProductLogisticsConfiguration : IEntityTypeConfiguration<ParserCurrentProductLogistics>
{
    public void Configure(EntityTypeBuilder<ParserCurrentProductLogistics> builder)
    {
        builder.ToTable("ParserCurrentProductLogistics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.LogisticsHash).IsRequired().HasMaxLength(128).HasColumnName("logistics_hash");
        builder.Property(x => x.LogisticsJson).IsRequired().HasColumnType("jsonb").HasColumnName("logistics_json");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(200).HasColumnName("batch_id");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.WbProductId).IsUnique();
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory });
    }
}
