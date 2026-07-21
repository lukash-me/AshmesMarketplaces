using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicAnalysisScheduleConfiguration : IEntityTypeConfiguration<PublicAnalysisSchedule>
{
    public void Configure(EntityTypeBuilder<PublicAnalysisSchedule> builder)
    {
        builder.ToTable("PublicAnalysisSchedules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ScheduleKey).IsRequired().HasMaxLength(128).HasColumnName("schedule_key");
        builder.Property(x => x.TimezoneId).IsRequired().HasMaxLength(128).HasColumnName("timezone_id");
        builder.Property(x => x.LocalTime).IsRequired().HasColumnName("local_time");
        builder.Property(x => x.NextRunAtUtc).IsRequired().HasColumnName("next_run_at_utc");
        builder.Property(x => x.LastStartedAtUtc).HasColumnName("last_started_at_utc");
        builder.Property(x => x.LastCompletedAtUtc).HasColumnName("last_completed_at_utc");
        builder.Property(x => x.LastStatus).HasMaxLength(64).HasColumnName("last_status");
        builder.Property(x => x.LastError).HasColumnName("last_error");
        builder.Property(x => x.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(x => x.LockedBy).HasMaxLength(128).HasColumnName("locked_by");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ScheduleKey).IsUnique();
        builder.HasIndex(x => x.NextRunAtUtc);
    }
}
