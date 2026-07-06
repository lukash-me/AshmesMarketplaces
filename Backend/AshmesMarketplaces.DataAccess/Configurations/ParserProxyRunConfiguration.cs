using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProxyRunConfiguration : IEntityTypeConfiguration<ParserProxyRun>
{
    public void Configure(EntityTypeBuilder<ParserProxyRun> builder)
    {
        builder.ToTable("ParserProxyRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserInstanceId).IsRequired().HasMaxLength(160).HasColumnName("parser_instance_id");
        builder.Property(x => x.ExternalProxyRunId).IsRequired().HasMaxLength(220).HasColumnName("external_proxy_run_id");
        builder.Property(x => x.ParserCycleId).IsRequired().HasMaxLength(180).HasColumnName("parser_cycle_id");
        builder.Property(x => x.CycleKind).IsRequired().HasMaxLength(32).HasColumnName("cycle_kind");
        builder.Property(x => x.ProxyKey).IsRequired().HasMaxLength(120).HasColumnName("proxy_key");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.EgressIp).HasMaxLength(64).HasColumnName("egress_ip");
        builder.Property(x => x.TokenRef).HasMaxLength(64).HasColumnName("token_ref");
        builder.Property(x => x.SessionStatus).HasMaxLength(32).HasColumnName("session_status");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.Phase).IsRequired().HasMaxLength(32).HasColumnName("phase");
        builder.Property(x => x.PlannedProductsCount).IsRequired().HasColumnName("planned_products_count");
        builder.Property(x => x.DownloadedProductsCount).IsRequired().HasColumnName("downloaded_products_count");
        builder.Property(x => x.PlannedRangesCount).IsRequired().HasColumnName("planned_ranges_count");
        builder.Property(x => x.CompletedRangesCount).IsRequired().HasColumnName("completed_ranges_count");
        builder.Property(x => x.RangeProgressPercent).IsRequired().HasColumnName("range_progress_percent");
        builder.Property(x => x.RangeChecksCount).IsRequired().HasColumnName("range_checks_count");
        builder.Property(x => x.FinalRangesCount).IsRequired().HasColumnName("final_ranges_count");
        builder.Property(x => x.EmptyRangesCount).IsRequired().HasColumnName("empty_ranges_count");
        builder.Property(x => x.SplitRangesCount).IsRequired().HasColumnName("split_ranges_count");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.LastHeartbeatAtUtc).IsRequired().HasColumnName("last_heartbeat_at_utc");
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.ParserInstanceId, x.ExternalProxyRunId }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.FinishedAtUtc);
        builder.HasIndex(x => new { x.ParserInstanceId, x.StartedAtUtc });
        builder.HasIndex(x => new { x.ParserInstanceId, x.ParserCycleId });
        builder.HasIndex(x => new { x.ParserInstanceId, x.CycleKind, x.StartedAtUtc });
        builder.HasIndex(x => new { x.ParserInstanceId, x.ProxyKey })
            .IsUnique()
            .HasFilter("status = 'running'");
        builder.HasIndex(x => new { x.ProxyKey, x.Status });
    }
}
