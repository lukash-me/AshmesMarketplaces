using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserCurrentProductRankConfiguration : IEntityTypeConfiguration<ParserCurrentProductRank>
{
    public void Configure(EntityTypeBuilder<ParserCurrentProductRank> builder)
    {
        builder.ToTable("ParserCurrentProductRanks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.RankHash).IsRequired().HasMaxLength(128).HasColumnName("rank_hash");
        builder.Property(x => x.RankJson).IsRequired().HasColumnType("jsonb").HasColumnName("rank_json");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(200).HasColumnName("batch_id");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.WbProductId).IsUnique();
    }
}
