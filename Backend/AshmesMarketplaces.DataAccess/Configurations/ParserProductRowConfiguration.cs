using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserProductRowConfiguration : IEntityTypeConfiguration<ParserProductRow>
{
    public void Configure(EntityTypeBuilder<ParserProductRow> builder)
    {
        builder.ToTable("ParserProductRows");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(128).HasColumnName("marketplace");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(160).HasColumnName("parser_run_id");
        builder.Property(x => x.ParsedAtUtc).IsRequired().HasColumnName("parsed_at_utc");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.SkuProduct).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("sku_product");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH).HasColumnName("name");
        builder.Property(x => x.Entity).HasMaxLength(512).HasColumnName("entity");
        builder.Property(x => x.BrandIdOnMp).HasColumnName("brand_id_on_mp");
        builder.Property(x => x.BrandName).HasMaxLength(512).HasColumnName("brand_name");
        builder.Property(x => x.SellerIdOnMp).HasColumnName("seller_id_on_mp");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.PriceRegular).HasPrecision(18, 2).HasColumnName("price_regular");
        builder.Property(x => x.PriceDiscounted).HasPrecision(18, 2).HasColumnName("price_discounted");
        builder.Property(x => x.PriceWbWallet).HasPrecision(18, 2).HasColumnName("price_wb_wallet");
        builder.Property(x => x.DiscountPercent).HasColumnName("discount_percent");
        builder.Property(x => x.TotalQuantity).HasColumnName("total_quantity");
        builder.Property(x => x.RatingRounded).HasColumnName("rating_rounded");
        builder.Property(x => x.ReviewRating).HasPrecision(18, 6).HasColumnName("review_rating");
        builder.Property(x => x.FeedbackCount).HasColumnName("feedback_count");
        builder.Property(x => x.FeedbackCountSource).HasMaxLength(128).HasColumnName("feedback_count_source");
        builder.Property(x => x.ImageUrls).HasColumnType("jsonb").HasColumnName("image_urls");
        builder.Property(x => x.ImageCount).HasColumnName("image_count");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SubjectParentId).HasColumnName("subject_parent_id");
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.RawObservedFields).HasColumnType("jsonb").HasColumnName("raw_observed_fields");

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
    }
}
