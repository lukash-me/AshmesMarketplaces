using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class MarketRecommendationRunConfiguration : IEntityTypeConfiguration<MarketRecommendationRun>
{
    public void Configure(EntityTypeBuilder<MarketRecommendationRun> builder)
    {
        builder.ToTable("MarketRecommendationRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(64).HasColumnName("kind");
        builder.Property(x => x.IdUser).HasColumnName("id_user");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(128).HasColumnName("marketplace");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategories).IsRequired().HasColumnType("jsonb").HasColumnName("source_subcategories");
        builder.Property(x => x.ProductParserRunId).HasMaxLength(160).HasColumnName("product_parser_run_id");
        builder.Property(x => x.RankParserRunId).HasMaxLength(160).HasColumnName("rank_parser_run_id");
        builder.Property(x => x.ReviewParserRunIds).IsRequired().HasColumnType("jsonb").HasColumnName("review_parser_run_ids");
        builder.Property(x => x.IntelligenceRequestId).IsRequired().HasMaxLength(128).HasColumnName("intelligence_request_id");
        builder.Property(x => x.Algorithm).IsRequired().HasMaxLength(128).HasColumnName("algorithm");
        builder.Property(x => x.AlgorithmVersion).IsRequired().HasMaxLength(64).HasColumnName("algorithm_version");
        builder.Property(x => x.ModelVersion).IsRequired().HasMaxLength(64).HasColumnName("model_version");
        builder.Property(x => x.InputSnapshotHash).IsRequired().HasMaxLength(64).HasColumnName("input_snapshot_hash");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.RequestedAtUtc).IsRequired().HasColumnName("requested_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.ValidUntilUtc).HasColumnName("valid_until_utc");
        builder.Property(x => x.ProductCountSent).IsRequired().HasColumnName("product_count_sent");
        builder.Property(x => x.RecommendationsCount).IsRequired().HasColumnName("recommendations_count");
        builder.Property(x => x.WarningCount).IsRequired().HasColumnName("warning_count");
        builder.Property(x => x.ErrorCode).HasMaxLength(128).HasColumnName("error_code");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.RawWarnings).HasColumnType("jsonb").HasColumnName("raw_warnings");
        builder.Property(x => x.ConfigOptions).IsRequired().HasColumnType("jsonb").HasColumnName("config_options");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.Kind, x.Status, x.ValidUntilUtc, x.CompletedAtUtc });
        builder.HasIndex(x => new { x.IdUser, x.Kind, x.Status, x.ValidUntilUtc, x.CompletedAtUtc })
            .HasDatabaseName("IX_MarketRecommendationRuns_user_latest");
        builder.HasIndex(x => new { x.Algorithm, x.AlgorithmVersion, x.ModelVersion, x.InputSnapshotHash });
        builder.HasIndex(x => x.ProductParserRunId);
        builder.HasIndex(x => x.RankParserRunId);
        builder.HasOne<AshmesMarketplaces.Domain.Entities.Users.User>()
            .WithMany()
            .HasForeignKey(x => x.IdUser)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
