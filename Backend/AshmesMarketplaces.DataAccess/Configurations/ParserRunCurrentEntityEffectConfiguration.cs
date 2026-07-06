using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserRunCurrentEntityEffectConfiguration : IEntityTypeConfiguration<ParserRunCurrentEntityEffect>
{
    public void Configure(EntityTypeBuilder<ParserRunCurrentEntityEffect> builder)
    {
        builder.ToTable("ParserRunCurrentEntityEffects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserProxyRunId).IsRequired().HasColumnName("parser_proxy_run_id");
        builder.Property(x => x.ParserBatchSubmissionId).IsRequired().HasColumnName("parser_batch_submission_id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(80).HasColumnName("wb_product_id");
        builder.Property(x => x.EntityKind).IsRequired().HasMaxLength(64).HasColumnName("entity_kind");
        builder.Property(x => x.EntityKey).IsRequired().HasMaxLength(240).HasColumnName("entity_key");
        builder.Property(x => x.EffectType).IsRequired().HasMaxLength(32).HasColumnName("effect_type");
        builder.Property(x => x.OldValueJson).HasColumnType("jsonb").HasColumnName("old_value_json");
        builder.Property(x => x.NewValueJson).HasColumnType("jsonb").HasColumnName("new_value_json");
        builder.Property(x => x.RollbackStatus).IsRequired().HasMaxLength(32).HasColumnName("rollback_status");
        builder.Property(x => x.RollbackId).HasColumnName("rollback_id");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.RolledBackAtUtc).HasColumnName("rolled_back_at_utc");

        builder.HasIndex(x => new { x.ParserProxyRunId, x.WbProductId });
        builder.HasIndex(x => new { x.WbProductId, x.EntityKind, x.EntityKey, x.CreatedAtUtc });
        builder.HasIndex(x => x.ParserBatchSubmissionId);
        builder.HasIndex(x => x.RollbackStatus);
    }
}
