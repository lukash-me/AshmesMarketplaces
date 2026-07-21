using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserRunRollbackConfiguration : IEntityTypeConfiguration<ParserRunRollback>
{
    public void Configure(EntityTypeBuilder<ParserRunRollback> builder)
    {
        builder.ToTable("ParserRunRollbacks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ParserProxyRunId).IsRequired().HasColumnName("parser_proxy_run_id");
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasColumnName("status");
        builder.Property(x => x.CreatedProductsCount).IsRequired().HasColumnName("created_products_count");
        builder.Property(x => x.UpdatedProductsCount).IsRequired().HasColumnName("updated_products_count");
        builder.Property(x => x.DeletedProductsCount).IsRequired().HasColumnName("deleted_products_count");
        builder.Property(x => x.RestoredProductsCount).IsRequired().HasColumnName("restored_products_count");
        builder.Property(x => x.ConflictProductsCount).IsRequired().HasColumnName("conflict_products_count");
        builder.Property(x => x.Message).HasColumnType("text").HasColumnName("message");
        builder.Property(x => x.Error).HasColumnType("text").HasColumnName("error");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ParserProxyRunId);
        builder.HasIndex(x => x.Status);
    }
}
