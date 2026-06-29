using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserBatchArtifactConfiguration : IEntityTypeConfiguration<ParserBatchArtifact>
{
    public void Configure(EntityTypeBuilder<ParserBatchArtifact> builder)
    {
        builder.ToTable("ParserBatchArtifacts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.BatchSubmissionId).IsRequired().HasColumnName("batch_submission_id");
        builder.Property(x => x.ArtifactKind).IsRequired().HasMaxLength(80).HasColumnName("artifact_kind");
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb").HasColumnName("payload_json");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => x.BatchSubmissionId);
        builder.HasIndex(x => new { x.BatchSubmissionId, x.ArtifactKind }).IsUnique();
    }
}
