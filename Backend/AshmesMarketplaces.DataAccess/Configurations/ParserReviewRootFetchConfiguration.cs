using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserReviewRootFetchConfiguration : IEntityTypeConfiguration<ParserReviewRootFetch>
{
    public void Configure(EntityTypeBuilder<ParserReviewRootFetch> builder)
    {
        builder.ToTable("ParserReviewRootFetches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
        builder.Property(x => x.TimestampUtc).IsRequired().HasColumnName("timestamp_utc");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.SourceWbRootId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_wb_root_id");
        builder.Property(x => x.SelectedWbProductIds).HasColumnType("jsonb").HasColumnName("selected_wb_product_ids");
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048).HasColumnName("endpoint");
        builder.Property(x => x.Attempts).IsRequired().HasColumnName("attempts");
        builder.Property(x => x.Retries).IsRequired().HasColumnName("retries");
        builder.Property(x => x.BackoffSecondsTotal).HasPrecision(18, 3).HasColumnName("backoff_seconds_total");
        builder.Property(x => x.HttpStatus).HasColumnName("http_status");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.ElapsedMs).IsRequired().HasColumnName("elapsed_ms");
        builder.Property(x => x.PayloadFeedbackCount).HasColumnName("payload_feedback_count");
        builder.Property(x => x.PayloadFeedbackRowsSeen).IsRequired().HasColumnName("payload_feedback_rows_seen");
        builder.Property(x => x.SelectedReviewRowsSeen).IsRequired().HasColumnName("selected_review_rows_seen");
        builder.Property(x => x.ReviewsWritten).IsRequired().HasColumnName("reviews_written");
        builder.Property(x => x.RepliesWritten).IsRequired().HasColumnName("replies_written");
        builder.Property(x => x.RawPayloadPath).HasMaxLength(4096).HasColumnName("raw_payload_path");
        builder.Property(x => x.ErrorSummary).HasColumnType("text").HasColumnName("error_summary");
        builder.Property(x => x.IsPartialSnapshot).IsRequired().HasColumnName("is_partial_snapshot");
        builder.Property(x => x.IsCappedRootPayload).IsRequired().HasColumnName("is_capped_root_payload");
        builder.Property(x => x.IsFullHistoryUnknown).IsRequired().HasColumnName("is_full_history_unknown");

        builder.HasIndex(x => new { x.IdParserFile, x.SourceLineNumber }).IsUnique();
        builder.HasIndex(x => new { x.IdParserRun, x.SourceWbRootId });

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
    }
}
