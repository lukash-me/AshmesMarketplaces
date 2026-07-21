using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserRankPageFetchConfiguration : IEntityTypeConfiguration<ParserRankPageFetch>
{
    public void Configure(EntityTypeBuilder<ParserRankPageFetch> builder)
    {
        builder.ToTable("ParserRankPageFetches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(128).HasColumnName("parser_run_id");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(64).HasColumnName("marketplace");
        builder.Property(x => x.RankContextId).IsRequired().HasMaxLength(128).HasColumnName("rank_context_id");
        builder.Property(x => x.RankContextType).IsRequired().HasMaxLength(64).HasColumnName("rank_context_type");
        builder.Property(x => x.SourceCategory).HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.Query).IsRequired().HasMaxLength(512).HasColumnName("query");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(128).HasColumnName("source_region_dest");
        builder.Property(x => x.Sort).HasMaxLength(64).HasColumnName("sort");
        builder.Property(x => x.Filters).HasColumnType("jsonb").HasColumnName("filters");
        builder.Property(x => x.RequestFingerprint).IsRequired().HasMaxLength(64).HasColumnName("request_fingerprint");
        builder.Property(x => x.Page).IsRequired().HasColumnName("page");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.ProductCount).IsRequired().HasColumnName("product_count");
        builder.Property(x => x.ResponseTotal).HasColumnName("response_total");
        builder.Property(x => x.RetryCount).IsRequired().HasColumnName("retry_count");
        builder.Property(x => x.HttpStatus).HasColumnName("http_status");
        builder.Property(x => x.WbCode).HasMaxLength(128).HasColumnName("wb_code");
        builder.Property(x => x.Message).HasColumnType("text").HasColumnName("message");

        builder.HasIndex(x => new { x.IdParserFile, x.SourceLineNumber }).IsUnique();
        builder.HasIndex(x => new { x.ParserRunId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.RankContextId, x.Page });
        builder.HasIndex(x => new { x.RequestFingerprint, x.Page });

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
    }
}
