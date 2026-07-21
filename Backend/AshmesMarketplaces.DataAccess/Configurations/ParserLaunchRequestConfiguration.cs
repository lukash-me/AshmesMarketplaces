using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserLaunchRequestConfiguration : IEntityTypeConfiguration<ParserLaunchRequest>
{
    public void Configure(EntityTypeBuilder<ParserLaunchRequest> builder)
    {
        builder.ToTable("ParserLaunchRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ParserInstanceConfigurationId).HasColumnName("parser_instance_configuration_id").IsRequired();
        builder.Property(x => x.ParserInstanceId).HasColumnName("parser_instance_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ParserCycleId).HasColumnName("parser_cycle_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.LaunchMode).HasColumnName("launch_mode").HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProxyKey).HasColumnName("proxy_key").HasMaxLength(128);
        builder.Property(x => x.BatchLimit).HasColumnName("batch_limit");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.Error).HasColumnName("error").HasMaxLength(4000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(x => new { x.ParserInstanceConfigurationId, x.Status });
        builder.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        builder.HasIndex(x => x.ParserCycleId);
    }
}
