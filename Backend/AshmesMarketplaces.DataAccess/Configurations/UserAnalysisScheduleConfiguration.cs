using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class UserAnalysisScheduleConfiguration : IEntityTypeConfiguration<UserAnalysisSchedule>
{
    public void Configure(EntityTypeBuilder<UserAnalysisSchedule> builder)
    {
        builder.ToTable("UserAnalysisSchedules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdUser).IsRequired().HasColumnName("id_user");
        builder.Property(x => x.TimezoneId).IsRequired().HasMaxLength(96).HasColumnName("timezone_id");
        builder.Property(x => x.HotProductsLocalTime).IsRequired().HasColumnName("hot_products_local_time");
        builder.Property(x => x.OverviewLocalTime).IsRequired().HasColumnName("overview_local_time");
        builder.Property(x => x.NextHotProductsRunAtUtc).IsRequired().HasColumnName("next_hot_products_run_at_utc");
        builder.Property(x => x.NextOverviewRunAtUtc).IsRequired().HasColumnName("next_overview_run_at_utc");
        builder.Property(x => x.LastHotProductsStartedAtUtc).HasColumnName("last_hot_products_started_at_utc");
        builder.Property(x => x.LastHotProductsCompletedAtUtc).HasColumnName("last_hot_products_completed_at_utc");
        builder.Property(x => x.LastHotProductsStatus).HasMaxLength(64).HasColumnName("last_hot_products_status");
        builder.Property(x => x.LastHotProductsError).HasColumnName("last_hot_products_error");
        builder.Property(x => x.LastOverviewStartedAtUtc).HasColumnName("last_overview_started_at_utc");
        builder.Property(x => x.LastOverviewCompletedAtUtc).HasColumnName("last_overview_completed_at_utc");
        builder.Property(x => x.LastOverviewStatus).HasMaxLength(64).HasColumnName("last_overview_status");
        builder.Property(x => x.LastOverviewError).HasColumnName("last_overview_error");
        builder.Property(x => x.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(x => x.LockedBy).HasMaxLength(128).HasColumnName("locked_by");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.IdUser).IsUnique();
        builder.HasIndex(x => x.NextHotProductsRunAtUtc);
        builder.HasIndex(x => x.NextOverviewRunAtUtc);
        builder.HasIndex(x => x.LockedUntilUtc);

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.IdUser).OnDelete(DeleteBehavior.Cascade);
    }
}
