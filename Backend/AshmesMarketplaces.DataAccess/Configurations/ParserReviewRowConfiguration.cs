using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserReviewRowConfiguration : IEntityTypeConfiguration<ParserReviewRow>
{
    public void Configure(EntityTypeBuilder<ParserReviewRow> builder)
    {
        builder.ToTable("ParserReviewRows");
        builder.HasKey(x => x.Id);

        ConfigureSourceColumns(builder);
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.ParsedAtUtc).IsRequired().HasColumnName("parsed_at_utc");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(128).HasColumnName("marketplace");
        builder.Property(x => x.InputProductsParserRunId).HasMaxLength(160).HasColumnName("input_products_parser_run_id");
        builder.Property(x => x.InputProductsJsonl).HasMaxLength(4096).HasColumnName("input_products_jsonl");
        builder.Property(x => x.SourceWbRootId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_wb_root_id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.ReviewAttributionMode).IsRequired().HasMaxLength(128).HasColumnName("review_attribution_mode");
        builder.Property(x => x.ReviewIdOnMp).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("review_id_on_mp");
        builder.Property(x => x.Rating).HasColumnName("rating");
        builder.Property(x => x.Text).HasColumnType("text").HasColumnName("text");
        builder.Property(x => x.Pros).HasColumnType("text").HasColumnName("pros");
        builder.Property(x => x.Cons).HasColumnType("text").HasColumnName("cons");
        builder.Property(x => x.CreatedAtOnMp).HasColumnName("created_at_on_mp");
        builder.Property(x => x.ReviewerName).HasMaxLength(512).HasColumnName("reviewer_name");
        builder.Property(x => x.ReviewerCountry).HasMaxLength(128).HasColumnName("reviewer_country");
        builder.Property(x => x.ReviewerHasPhoto).HasColumnName("reviewer_has_photo");
        builder.Property(x => x.HelpfulPlus).HasColumnName("helpful_plus");
        builder.Property(x => x.HelpfulMinus).HasColumnName("helpful_minus");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest");
        builder.Property(x => x.RawObservedFields).HasColumnType("jsonb").HasColumnName("raw_observed_fields");
        builder.Property(x => x.IsPartialSnapshot).IsRequired().HasColumnName("is_partial_snapshot");
        builder.Property(x => x.IsCappedRootPayload).IsRequired().HasColumnName("is_capped_root_payload");
        builder.Property(x => x.IsFullHistoryUnknown).IsRequired().HasColumnName("is_full_history_unknown");

        builder.HasIndex(x => new { x.IdParserFile, x.SourceLineNumber }).IsUnique();
        builder.HasIndex(x => x.ParserRunId);
        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => x.SourceWbRootId);
        builder.HasIndex(x => x.ReviewIdOnMp);
        builder.HasIndex(x => x.CreatedAtOnMp);
        builder.HasIndex(x => new { x.ParserRunId, x.CreatedAtOnMp });

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserReviewRootFetch>().WithMany().HasForeignKey(x => x.IdReviewRootFetch).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSourceColumns(EntityTypeBuilder<ParserReviewRow> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.IdReviewRootFetch).HasColumnName("id_review_root_fetch");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
    }
}
