using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RecommendationProductConfiguration : IEntityTypeConfiguration<RecommendationProduct>
{
    public void Configure(EntityTypeBuilder<RecommendationProduct> builder)
    {
        builder.ToTable("Recommendation_Products");

        builder.HasKey(x => new { x.IdRecommendation, x.IdProduct });

        builder.Property(x => x.IdRecommendation)
            .ValueGeneratedNever()
            .HasColumnName("id_recommendation");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .ValueGeneratedNever()
            .HasColumnName("id_product");

        builder.HasOne<Recommendation>()
            .WithMany()
            .HasForeignKey(x => x.IdRecommendation)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
