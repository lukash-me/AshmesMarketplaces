using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserCurrentProductReviewEvidenceConfiguration : IEntityTypeConfiguration<ParserCurrentProductReviewEvidence>
{
    public void Configure(EntityTypeBuilder<ParserCurrentProductReviewEvidence> builder)
    {
        builder.ToTable("ParserCurrentProductReviewEvidence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.ReviewIdOnMp).IsRequired().HasMaxLength(160).HasColumnName("review_id_on_mp");
        builder.Property(x => x.ReviewHash).IsRequired().HasMaxLength(128).HasColumnName("review_hash");
        builder.Property(x => x.ReviewJson).IsRequired().HasColumnType("jsonb").HasColumnName("review_json");
        builder.Property(x => x.Rating).HasColumnName("rating");
        builder.Property(x => x.CreatedAtOnMp).HasColumnName("created_at_on_mp");
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc");
        builder.Property(x => x.BatchId).IsRequired().HasMaxLength(200).HasColumnName("batch_id");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.WbProductId, x.ReviewIdOnMp }).IsUnique();
        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => x.ObservedAtUtc);
    }
}
