using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserBatchSubmissionConfiguration : IEntityTypeConfiguration<ParserBatchSubmission>
{
    public void Configure(EntityTypeBuilder<ParserBatchSubmission> builder)
    {
        builder.ToTable("ParserBatchSubmissions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.ExternalBatchId).IsRequired().HasMaxLength(200).HasColumnName("external_batch_id");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.ProxyKey).HasMaxLength(160).HasColumnName("proxy_key");
        builder.Property(x => x.ParserCycleId).HasMaxLength(200).HasColumnName("parser_cycle_id");
        builder.Property(x => x.ExternalProxyRunId).HasMaxLength(260).HasColumnName("external_proxy_run_id");
        builder.Property(x => x.ParserProxyRunId).HasColumnName("parser_proxy_run_id");
        builder.Property(x => x.BatchKind).IsRequired().HasMaxLength(80).HasColumnName("batch_kind");
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(128).HasColumnName("content_hash");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.AttemptsCount).IsRequired().HasColumnName("attempts_count");
        builder.Property(x => x.AcceptedAtUtc).IsRequired().HasColumnName("accepted_at_utc");
        builder.Property(x => x.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.Error).HasColumnType("text").HasColumnName("error");
        builder.Property(x => x.AcknowledgedAtUtc).HasColumnName("acknowledged_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.ParserInstanceId, x.ExternalBatchId }).IsUnique();
        builder.HasIndex(x => x.ExternalBatchId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory });
        builder.HasIndex(x => x.AcceptedAtUtc);
        builder.HasIndex(x => x.ParserProxyRunId);
        builder.HasIndex(x => new { x.ParserInstanceId, x.ExternalProxyRunId });
    }
}
