using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicParserObservedLogisticsSnapshotConfiguration : IEntityTypeConfiguration<PublicParserObservedLogisticsSnapshot>
{
    public void Configure(EntityTypeBuilder<PublicParserObservedLogisticsSnapshot> builder)
    {
        builder.ToTable("PublicParserObservedLogisticsSnapshots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.SnapshotKey).IsRequired().HasMaxLength(64).HasColumnName("snapshot_key");
        builder.Property(x => x.CurrentLogisticsRunId).HasMaxLength(256).HasColumnName("current_logistics_run_id");
        builder.Property(x => x.PreviousLogisticsRunId).HasMaxLength(256).HasColumnName("previous_logistics_run_id");
        builder.Property(x => x.CurrentObservedAtUtc).HasColumnName("current_observed_at_utc");
        builder.Property(x => x.PreviousObservedAtUtc).HasColumnName("previous_observed_at_utc");
        builder.Property(x => x.MarketEventsJson).IsRequired().HasColumnType("jsonb").HasColumnName("market_events_json");
        builder.Property(x => x.MarketEventSummaryJson).IsRequired().HasColumnType("jsonb").HasColumnName("market_event_summary_json");
        builder.Property(x => x.StockDecreaseItemsJson).IsRequired().HasColumnType("jsonb").HasColumnName("stock_decrease_items_json");
        builder.Property(x => x.StockDecreaseSummaryJson).IsRequired().HasColumnType("jsonb").HasColumnName("stock_decrease_summary_json");
        builder.Property(x => x.MarketEventsCount).IsRequired().HasColumnName("market_events_count");
        builder.Property(x => x.StockDecreasesCount).IsRequired().HasColumnName("stock_decreases_count");
        builder.Property(x => x.CalculatedAtUtc).HasColumnName("calculated_at_utc");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.SnapshotKey).IsUnique();
        builder.HasIndex(x => x.CalculatedAtUtc);
    }
}
