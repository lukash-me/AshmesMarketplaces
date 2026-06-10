using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WorkspaceMarketProductConfiguration : IEntityTypeConfiguration<WorkspaceMarketProduct>
{
    public void Configure(EntityTypeBuilder<WorkspaceMarketProduct> builder)
    {
        builder.ToTable("WorkspaceMarketProducts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdWorkspace).IsRequired().HasColumnName("id_workspace");
        builder.Property(x => x.IdCreatedByUser).IsRequired().HasColumnName("id_created_by_user");
        builder.Property(x => x.ParserProductRowId).IsRequired().HasColumnName("parser_product_row_id");
        builder.Property(x => x.WbProductId).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_product_id");
        builder.Property(x => x.WbRootId).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("wb_root_id");
        builder.Property(x => x.SourceCategory).HasMaxLength(512).HasColumnName("source_category");
        builder.Property(x => x.SourceSubcategory).HasMaxLength(512).HasColumnName("source_subcategory");
        builder.Property(x => x.SourceRegionDest).HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest");
        builder.Property(x => x.SourceSubcategoryKey).IsRequired().HasMaxLength(512).HasColumnName("source_subcategory_key");
        builder.Property(x => x.SourceRegionDestKey).IsRequired().HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH).HasColumnName("source_region_dest_key");
        builder.Property(x => x.SourceQuery).HasMaxLength(512).HasColumnName("source_query");
        builder.Property(x => x.TagKey).IsRequired().HasMaxLength(32).HasColumnName("tag_key");
        builder.Property(x => x.Note).HasMaxLength(Constants.PRODUCT_DESCRIPTION_MAX_LENGTH).HasColumnName("note");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(Constants.PRODUCT_NAME_MAX_LENGTH).HasColumnName("name");
        builder.Property(x => x.BrandName).HasMaxLength(512).HasColumnName("brand_name");
        builder.Property(x => x.SellerName).HasMaxLength(512).HasColumnName("seller_name");
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(Constants.URL_MAX_LENGTH).HasColumnName("thumbnail_url");
        builder.Property(x => x.PriceRegular).HasPrecision(18, 2).HasColumnName("price_regular");
        builder.Property(x => x.PriceDiscounted).HasPrecision(18, 2).HasColumnName("price_discounted");
        builder.Property(x => x.PriceWbWallet).HasPrecision(18, 2).HasColumnName("price_wb_wallet");
        builder.Property(x => x.ReviewRating).HasPrecision(18, 6).HasColumnName("review_rating");
        builder.Property(x => x.FeedbackCount).HasColumnName("feedback_count");
        builder.Property(x => x.PositionAbsolute).HasColumnName("position_absolute");
        builder.Property(x => x.TotalQuantity).HasColumnName("total_quantity");
        builder.Property(x => x.DateCreate).IsRequired().HasColumnName("date_create");
        builder.Property(x => x.DateUpdate).IsRequired().HasColumnName("date_update");

        builder.HasIndex(x => x.IdWorkspace);
        builder.HasIndex(x => x.TagKey);
        builder.HasIndex(x => x.WbProductId);
        builder.HasIndex(x => x.SourceSubcategory);
        builder.HasIndex(x => x.DateUpdate);
        builder.HasIndex(x => new { x.IdWorkspace, x.WbProductId, x.SourceSubcategoryKey, x.SourceRegionDestKey }).IsUnique();

        builder.HasOne<Workspace>().WithMany().HasForeignKey(x => x.IdWorkspace).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.IdCreatedByUser).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParserProductRow>().WithMany().HasForeignKey(x => x.ParserProductRowId).OnDelete(DeleteBehavior.Restrict);
    }
}
