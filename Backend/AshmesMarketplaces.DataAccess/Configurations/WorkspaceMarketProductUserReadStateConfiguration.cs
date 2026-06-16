using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class WorkspaceMarketProductUserReadStateConfiguration : IEntityTypeConfiguration<WorkspaceMarketProductUserReadState>
{
    public void Configure(EntityTypeBuilder<WorkspaceMarketProductUserReadState> builder)
    {
        builder.ToTable("WorkspaceMarketProductUserReadStates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdWorkspaceMarketProduct).IsRequired().HasColumnName("id_workspace_market_product");
        builder.Property(x => x.IdUser).IsRequired().HasColumnName("id_user");
        builder.Property(x => x.LastViewedAtUtc).IsRequired().HasColumnName("last_viewed_at_utc");
        builder.Property(x => x.BaselineObservedAtUtc).HasColumnName("baseline_observed_at_utc");
        builder.Property(x => x.BaselinePrice).HasPrecision(18, 2).HasColumnName("baseline_price");
        builder.Property(x => x.BaselinePosition).HasColumnName("baseline_position");
        builder.Property(x => x.BaselineStock).HasColumnName("baseline_stock");
        builder.Property(x => x.BaselineFeedbackCount).HasColumnName("baseline_feedback_count");
        builder.Property(x => x.BaselineReviewRating).HasPrecision(5, 2).HasColumnName("baseline_review_rating");
        builder.Property(x => x.BaselineLogisticsFactors).HasColumnType("jsonb").HasColumnName("baseline_logistics_factors");

        builder.HasIndex(x => x.IdWorkspaceMarketProduct);
        builder.HasIndex(x => x.IdUser);
        builder.HasIndex(x => new { x.IdWorkspaceMarketProduct, x.IdUser }).IsUnique();

        builder.HasOne<WorkspaceMarketProduct>().WithMany().HasForeignKey(x => x.IdWorkspaceMarketProduct).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.IdUser).OnDelete(DeleteBehavior.Restrict);
    }
}
