using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserImportExecutionConfiguration : IEntityTypeConfiguration<ParserImportExecution>
{
    public void Configure(EntityTypeBuilder<ParserImportExecution> builder)
    {
        builder.ToTable("ParserImportExecutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).HasColumnName("id_parser_run");
        builder.Property(x => x.Mode).IsRequired().HasMaxLength(96).HasColumnName("mode");
        builder.Property(x => x.IsDryRun).IsRequired().HasColumnName("is_dry_run");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.RowsRead).IsRequired().HasColumnName("rows_read");
        builder.Property(x => x.RowsWritten).IsRequired().HasColumnName("rows_written");
        builder.Property(x => x.RowsSkipped).IsRequired().HasColumnName("rows_skipped");
        builder.Property(x => x.ErrorCount).IsRequired().HasColumnName("error_count");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");

        builder.HasOne<ParserRun>()
            .WithMany()
            .HasForeignKey(x => x.IdParserRun)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
