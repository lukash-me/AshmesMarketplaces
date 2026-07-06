using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserRunProductEffectConfiguration : IEntityTypeConfiguration<ParserRunProductEffect>
{
    public void Configure(EntityTypeBuilder<ParserRunProductEffect> builder)
    {
        builder.ToTable("ParserRunProductEffects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserProxyRunId).IsRequired().HasColumnName("parser_proxy_run_id");
        builder.Property(x => x.ParserBatchSubmissionId).IsRequired().HasColumnName("parser_batch_submission_id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(80).HasColumnName("wb_product_id");
        builder.Property(x => x.ServiceProductId).IsRequired().HasColumnName("service_product_id");
        builder.Property(x => x.EffectType).IsRequired().HasMaxLength(32).HasColumnName("effect_type");
        builder.Property(x => x.RollbackStatus).IsRequired().HasMaxLength(32).HasColumnName("rollback_status");
        builder.Property(x => x.RollbackId).HasColumnName("rollback_id");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.RolledBackAtUtc).HasColumnName("rolled_back_at_utc");

        builder.HasIndex(x => new { x.ParserProxyRunId, x.WbProductId });
        builder.HasIndex(x => new { x.ParserProxyRunId, x.RollbackStatus });
        builder.HasIndex(x => new { x.WbProductId, x.CreatedAtUtc });
        builder.HasIndex(x => x.ParserBatchSubmissionId);
    }
}
