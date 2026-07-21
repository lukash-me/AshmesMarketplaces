using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProductDetailRowConfiguration : IEntityTypeConfiguration<ParserProductDetailRow>
{
    public void Configure(EntityTypeBuilder<ParserProductDetailRow> builder)
    {
        builder.ToTable("ParserProductDetailRows");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.ParsedAtUtc).IsRequired().HasColumnName("parsed_at_utc");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(128).HasColumnName("marketplace");
        builder.Property(x => x.InputProductsParserRunId).HasMaxLength(160).HasColumnName("input_products_parser_run_id");
        builder.Property(x => x.InputProductsJsonl).HasMaxLength(4096).HasColumnName("input_products_jsonl");
        builder.Property(x => x.SourceRequestFamily).IsRequired().HasMaxLength(128).HasColumnName("source_request_family");
        builder.Property(x => x.SourceEndpoint).IsRequired().HasMaxLength(1024).HasColumnName("source_endpoint");
        builder.Property(x => x.RequestFingerprint).IsRequired().HasMaxLength(64).HasColumnName("request_fingerprint");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest");
        builder.Property(x => x.Description).HasColumnType("text").HasColumnName("description");
        builder.Property(x => x.Characteristics).HasColumnType("jsonb").HasColumnName("characteristics");
        builder.Property(x => x.GroupedOptions).HasColumnType("jsonb").HasColumnName("grouped_options");
        builder.Property(x => x.MediaCount).HasColumnName("media_count");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.RawDetailFields).HasColumnType("jsonb").HasColumnName("raw_detail_fields");

        builder.HasIndex(x => new { x.IdParserFile, x.SourceLineNumber }).IsUnique();
        builder.HasIndex(x => x.ParserRunId);
        builder.HasIndex(x => x.InputProductsParserRunId);
        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => x.WbRootId);
        builder.HasIndex(x => new { x.WbProductId, x.ParsedAtUtc });

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
    }
}
