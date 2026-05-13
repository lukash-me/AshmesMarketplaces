using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdModel)
            .IsRequired()
            .HasColumnName("id_model");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(x => x.TypeObject)
            .IsRequired()
            .HasColumnName("type_object");

        builder.Property(x => x.Score)
            .IsRequired()
            .HasPrecision(18, 6)
            .HasColumnName("score");

        builder.Property(x => x.Explanation)
            .HasColumnType("jsonb")
            .HasColumnName("explanation");

        builder.Property(x => x.Snapshot)
            .HasColumnType("jsonb")
            .HasColumnName("snapshot");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");
    }
}
