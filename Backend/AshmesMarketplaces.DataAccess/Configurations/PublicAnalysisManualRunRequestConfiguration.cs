using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicAnalysisManualRunRequestConfiguration : IEntityTypeConfiguration<PublicAnalysisManualRunRequest>
{
    public void Configure(EntityTypeBuilder<PublicAnalysisManualRunRequest> builder)
    {
        builder.ToTable("PublicAnalysisManualRunRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ScheduleKey).IsRequired().HasMaxLength(160).HasColumnName("schedule_key");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.RequestedByUserId).IsRequired().HasColumnName("requested_by_user_id");
        builder.Property(x => x.RequestedAtUtc).IsRequired().HasColumnName("requested_at_utc");
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.Error).HasColumnType("text").HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        builder.HasIndex(x => new { x.ScheduleKey, x.RequestedAtUtc });
        builder.HasIndex(x => x.ScheduleKey)
            .IsUnique()
            .HasDatabaseName("IX_PublicAnalysisManualRunRequests_active_schedule_key")
            .HasFilter("status IN ('queued', 'running')");
    }
}
