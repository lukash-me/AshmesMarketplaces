using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserLogisticsSnapshotRowConfiguration : IEntityTypeConfiguration<ParserLogisticsSnapshotRow>
{
    public void Configure(EntityTypeBuilder<ParserLogisticsSnapshotRow> builder)
    {
        builder.ToTable("ParserLogisticsSnapshotRows");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).IsRequired().HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).IsRequired().HasColumnName("source_line_number");
        builder.Property(x => x.RowHash).IsRequired().HasMaxLength(64).HasColumnName("row_hash");
        builder.Property(x => x.SchemaVersion).IsRequired().HasColumnName("schema_version");
        builder.Property(x => x.ParserRunId).IsRequired().HasMaxLength(128).HasColumnName("parser_run_id");
        builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(64).HasColumnName("marketplace");
        builder.Property(x => x.ObservedAtUtc).IsRequired().HasColumnName("observed_at_utc");
        builder.Property(x => x.SourceRequestFamily).IsRequired().HasMaxLength(128).HasColumnName("source_request_family");
        builder.Property(x => x.SourceEndpoint).IsRequired().HasColumnType("text").HasColumnName("source_endpoint");
        builder.Property(x => x.RequestFingerprint).IsRequired().HasMaxLength(64).HasColumnName("request_fingerprint");
        builder.Property(x => x.SourceRegionDest).IsRequired().HasMaxLength(128).HasColumnName("source_region_dest");
        builder.Property(x => x.DeliveryProfileKey).HasMaxLength(128).HasColumnName("delivery_profile_key");
        builder.Property(x => x.DeliveryDestinationName).HasMaxLength(255).HasColumnName("delivery_destination_name");
        builder.Property(x => x.DeliveryProfileVersion).HasMaxLength(64).HasColumnName("delivery_profile_version");
        builder.Property(x => x.DeliveryDestinationCity).HasMaxLength(255).HasColumnName("delivery_destination_city");
        builder.Property(x => x.DeliveryDestinationLabel).HasMaxLength(512).HasColumnName("delivery_destination_label");
        builder.Property(x => x.DeliveryDestinationAddress).HasColumnType("text").HasColumnName("delivery_destination_address");
        builder.Property(x => x.DeliveryDestinationLatitude).HasPrecision(10, 7).HasColumnName("delivery_destination_latitude");
        builder.Property(x => x.DeliveryDestinationLongitude).HasPrecision(10, 7).HasColumnName("delivery_destination_longitude");
        builder.Property(x => x.SourceCategory).HasMaxLength(255).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(255).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(128).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(128).HasColumnName("wb_root_id");
        builder.Property(x => x.SellerId).HasMaxLength(128).HasColumnName("seller_id");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.TotalQuantityObserved).HasColumnName("total_quantity_observed");
        builder.Property(x => x.QuantityIsCapped).HasColumnName("quantity_is_capped");
        builder.Property(x => x.QuantityCapObserved).HasColumnName("quantity_cap_observed");
        builder.Property(x => x.QuantitySemantics).IsRequired().HasMaxLength(128).HasColumnName("quantity_semantics");
        builder.Property(x => x.ProductWhRaw).HasMaxLength(128).HasColumnName("product_wh_raw");
        builder.Property(x => x.ProductTime1Raw).HasColumnName("product_time1_raw");
        builder.Property(x => x.ProductTime2Raw).HasColumnName("product_time2_raw");
        builder.Property(x => x.ProductDtypeRaw).HasColumnName("product_dtype_raw");
        builder.Property(x => x.ProductDistRaw).HasColumnName("product_dist_raw");
        builder.Property(x => x.VisibleDeliveryStatus).HasMaxLength(64).HasColumnName("visible_delivery_status");
        builder.Property(x => x.VisibleDeliveryLabel).HasMaxLength(255).HasColumnName("visible_delivery_label");
        builder.Property(x => x.VisibleDeliveryDate).HasColumnName("visible_delivery_date");
        builder.Property(x => x.VisibleDeliverySource).HasMaxLength(255).HasColumnName("visible_delivery_source");
        builder.Property(x => x.VisibleDeliveryObservedAtUtc).HasColumnName("visible_delivery_observed_at_utc");
        builder.Property(x => x.VisibleDeliveryRawPayload).HasColumnType("jsonb").HasColumnName("visible_delivery_raw_payload");
        builder.Property(x => x.RawObservedFields).HasColumnType("jsonb").HasColumnName("raw_observed_fields");

        builder.HasIndex(x => new { x.IdParserFile, x.SourceLineNumber }).IsUnique();
        builder.HasIndex(x => new { x.ParserRunId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.WbProductId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.WbRootId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.SourceRegionDest, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.DeliveryProfileKey, x.SourceRegionDest, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.SourceCategory, x.SourceSubcategory });
        builder.HasIndex(x => x.RequestFingerprint);

        builder.HasOne<ParserRun>().WithMany().HasForeignKey(x => x.IdParserRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserFile>().WithMany().HasForeignKey(x => x.IdParserFile).OnDelete(DeleteBehavior.Restrict);
    }
}
