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
        builder.Property(x => x.ProxyKey).IsRequired().HasMaxLength(120).HasColumnName("proxy_key");
        builder.Property(x => x.SourceCategory).IsRequired().HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).IsRequired().HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.PlannedProductsCount).IsRequired().HasColumnName("planned_products_count");
        builder.Property(x => x.DownloadedProductsCount).IsRequired().HasColumnName("downloaded_products_count");
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
        builder.HasIndex(x => new { x.ProxyKey, x.Status });
    }
}
