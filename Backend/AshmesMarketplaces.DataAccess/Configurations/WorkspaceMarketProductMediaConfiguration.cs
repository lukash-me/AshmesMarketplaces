using AshmesMarketplaces.Domain.Entities.Workspaces;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WorkspaceMarketProductMediaConfiguration : IEntityTypeConfiguration<WorkspaceMarketProductMedia>
{
    public void Configure(EntityTypeBuilder<WorkspaceMarketProductMedia> builder)
    {
        builder.ToTable("WorkspaceMarketProductMedia");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdWorkspaceMarketProduct).IsRequired().HasColumnName("id_workspace_market_product");
        builder.Property(x => x.Url).IsRequired().HasMaxLength(Constants.URL_MAX_LENGTH).HasColumnName("url");
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(1024).HasColumnName("storage_key");
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(512).HasColumnName("file_name");
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(128).HasColumnName("content_type");
        builder.Property(x => x.SortOrder).IsRequired().HasColumnName("sort_order");
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(32).HasColumnName("kind");
        builder.Property(x => x.UploadedAtUtc).IsRequired().HasColumnName("uploaded_at_utc");

        builder.HasIndex(x => x.IdWorkspaceMarketProduct);
        builder.HasIndex(x => new { x.IdWorkspaceMarketProduct, x.SortOrder }).IsUnique();

        builder
            .HasOne<WorkspaceMarketProduct>()
            .WithMany()
            .HasForeignKey(x => x.IdWorkspaceMarketProduct)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
