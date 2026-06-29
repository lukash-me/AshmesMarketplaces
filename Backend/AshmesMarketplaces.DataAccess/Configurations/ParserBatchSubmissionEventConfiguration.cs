using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserBatchSubmissionEventConfiguration : IEntityTypeConfiguration<ParserBatchSubmissionEvent>
{
    public void Configure(EntityTypeBuilder<ParserBatchSubmissionEvent> builder)
    {
        builder.ToTable("ParserBatchSubmissionEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.BatchSubmissionId).IsRequired().HasColumnName("batch_submission_id");
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(80).HasColumnName("event_type");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.Message).HasColumnType("text").HasColumnName("message");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => x.BatchSubmissionId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
