using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserRunConfiguration : IEntityTypeConfiguration<ParserRun>
{
    public void Configure(EntityTypeBuilder<ParserRun> builder)
    {
        builder.ToTable("ParserRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(128).HasColumnName("marketplace");
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(64).HasColumnName("kind");
        builder.Property(x => x.ManifestStatus).IsRequired().HasMaxLength(64).HasColumnName("manifest_status");
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.ParserVersion).HasMaxLength(128).HasColumnName("parser_version");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        builder.Property(x => x.RequestedScope).HasColumnType("jsonb").HasColumnName("requested_scope");
        builder.Property(x => x.Counters).HasColumnType("jsonb").HasColumnName("counters");
        builder.Property(x => x.DateRegisteredUtc).IsRequired().HasColumnName("date_registered_utc");
    }
}
