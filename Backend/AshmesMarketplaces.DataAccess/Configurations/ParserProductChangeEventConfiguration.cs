using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProductChangeEventConfiguration : IEntityTypeConfiguration<ParserProductChangeEvent>
{
    public void Configure(EntityTypeBuilder<ParserProductChangeEvent> builder)
    {
        builder.ToTable("ParserProductChangeEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(200).HasColumnName("batch_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.FieldGroup).IsRequired().HasMaxLength(80).HasColumnName("field_group");
        builder.Property(x => x.ChangeType).IsRequired().HasMaxLength(80).HasColumnName("change_type");
        builder.Property(x => x.OldHash).HasMaxLength(128).HasColumnName("old_hash");
        builder.Property(x => x.NewHash).IsRequired().HasMaxLength(128).HasColumnName("new_hash");
        builder.Property(x => x.OldValueJson).HasColumnType("jsonb").HasColumnName("old_value_json");
        builder.Property(x => x.NewValueJson).HasColumnType("jsonb").HasColumnName("new_value_json");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => new { x.WbProductId, x.FieldGroup, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory });
        builder.HasIndex(x => new { x.SourceSubcategory, x.FieldGroup, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.BatchId, x.WbProductId, x.FieldGroup, x.NewHash }).IsUnique();
        builder.HasIndex(x => x.ObservedAtUtc);
    }
}
