using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RecommendationCategoryConfiguration : IEntityTypeConfiguration<RecommendationCategory>
{
    public void Configure(EntityTypeBuilder<RecommendationCategory> builder)
    {
        builder.ToTable("Recommendation_Categories");

        builder.HasKey(x => new { x.IdRecommendation, x.IdCategory });

        builder.Property(x => x.IdRecommendation)
            .ValueGeneratedNever()
            .HasColumnName("id_recommendation");

        builder.Property(x => x.IdCategory)
            .ValueGeneratedNever()
            .HasColumnName("id_category");

        builder.HasOne<Recommendation>()
            .WithMany()
            .HasForeignKey(x => x.IdRecommendation)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.IdCategory)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
