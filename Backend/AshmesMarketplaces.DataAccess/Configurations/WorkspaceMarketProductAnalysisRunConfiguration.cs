using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WorkspaceMarketProductAnalysisRunConfiguration : IEntityTypeConfiguration<WorkspaceMarketProductAnalysisRun>
{
    public void Configure(EntityTypeBuilder<WorkspaceMarketProductAnalysisRun> builder)
    {
        builder.ToTable("WorkspaceMarketProductAnalysisRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdWorkspace).IsRequired().HasColumnName("id_workspace");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.StartedAtUtc).IsRequired().HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.Algorithm).IsRequired().HasMaxLength(128).HasColumnName("algorithm");
        builder.Property(x => x.AlgorithmVersion).IsRequired().HasMaxLength(64).HasColumnName("algorithm_version");
        builder.Property(x => x.ModelVersion).IsRequired().HasMaxLength(64).HasColumnName("model_version");
        builder.Property(x => x.ProductCount).IsRequired().HasColumnName("product_count");
        builder.Property(x => x.SignalCount).IsRequired().HasColumnName("signal_count");
        builder.Property(x => x.SimilarProductCount).IsRequired().HasColumnName("similar_product_count");
        builder.Property(x => x.Warnings).IsRequired().HasColumnType("jsonb").HasColumnName("warnings");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");

        builder.HasIndex(x => new { x.IdWorkspace, x.CompletedAtUtc });
        builder.HasIndex(x => new { x.IdWorkspace, x.Status });
        builder.HasOne<Workspace>().WithMany().HasForeignKey(x => x.IdWorkspace).OnDelete(DeleteBehavior.Restrict);
    }
}
