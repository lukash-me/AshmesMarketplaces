using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProductPresenceEventConfiguration : IEntityTypeConfiguration<ParserProductPresenceEvent>
{
    public void Configure(EntityTypeBuilder<ParserProductPresenceEvent> builder)
    {
        builder.ToTable("ParserProductPresenceEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(80).HasColumnName("wb_product_id");
        builder.Property(x => x.ParserCycleId).IsRequired().HasMaxLength(200).HasColumnName("parser_cycle_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.OldStatus).IsRequired().HasMaxLength(64).HasColumnName("old_status");
        builder.Property(x => x.NewStatus).IsRequired().HasMaxLength(64).HasColumnName("new_status");
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(160).HasColumnName("reason");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.ParserCycleId, x.SourceCategory, x.SourceSubcategory });
        builder.HasIndex(x => new { x.WbProductId, x.CreatedAtUtc });
    }
}
