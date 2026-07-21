using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WorkspaceMarketProductAnalysisConfiguration : IEntityTypeConfiguration<WorkspaceMarketProductAnalysis>
{
    public void Configure(EntityTypeBuilder<WorkspaceMarketProductAnalysis> builder)
    {
        builder.ToTable("WorkspaceMarketProductAnalyses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdAnalysisRun).IsRequired().HasColumnName("id_analysis_run");
        builder.Property(x => x.IdWorkspaceMarketProduct).IsRequired().HasColumnName("id_workspace_market_product");
        builder.Property(x => x.CurrentPrice).HasPrecision(18, 2).HasColumnName("current_price");
        builder.Property(x => x.PreviousPrice).HasPrecision(18, 2).HasColumnName("previous_price");
        builder.Property(x => x.PriceDelta).HasPrecision(18, 2).HasColumnName("price_delta");
        builder.Property(x => x.CurrentPosition).HasColumnName("current_position");
        builder.Property(x => x.PreviousPosition).HasColumnName("previous_position");
        builder.Property(x => x.PositionDelta).HasColumnName("position_delta");
        builder.Property(x => x.CurrentStock).HasColumnName("current_stock");
        builder.Property(x => x.PreviousStock).HasColumnName("previous_stock");
        builder.Property(x => x.StockDelta).HasColumnName("stock_delta");
        builder.Property(x => x.CurrentFeedbackCount).HasColumnName("current_feedback_count");
        builder.Property(x => x.PreviousFeedbackCount).HasColumnName("previous_feedback_count");
        builder.Property(x => x.FeedbackDelta).HasColumnName("feedback_delta");
        builder.Property(x => x.CurrentReviewRating).HasPrecision(5, 2).HasColumnName("current_review_rating");
        builder.Property(x => x.PreviousReviewRating).HasPrecision(5, 2).HasColumnName("previous_review_rating");
        builder.Property(x => x.ReviewRatingDelta).HasPrecision(5, 2).HasColumnName("review_rating_delta");
        builder.Property(x => x.LatestObservedAtUtc).HasColumnName("latest_observed_at_utc");
        builder.Property(x => x.Signals).IsRequired().HasColumnType("jsonb").HasColumnName("signals");
        builder.Property(x => x.SimilarProducts).IsRequired().HasColumnType("jsonb").HasColumnName("similar_products");
        builder.Property(x => x.SimilarProductGroups).IsRequired().HasColumnType("jsonb").HasColumnName("similar_product_groups");
        builder.Property(x => x.ComputedAtUtc).IsRequired().HasColumnName("computed_at_utc");

        builder.HasIndex(x => x.IdAnalysisRun);
        builder.HasIndex(x => x.IdWorkspaceMarketProduct);
        builder.HasIndex(x => new { x.IdAnalysisRun, x.IdWorkspaceMarketProduct }).IsUnique();
        builder.HasOne<WorkspaceMarketProductAnalysisRun>().WithMany().HasForeignKey(x => x.IdAnalysisRun).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkspaceMarketProduct>().WithMany().HasForeignKey(x => x.IdWorkspaceMarketProduct).OnDelete(DeleteBehavior.Restrict);
    }
}
