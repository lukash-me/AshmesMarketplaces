using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class PublicTopForecastRunConfiguration : IEntityTypeConfiguration<PublicTopForecastRun>
{
    public void Configure(EntityTypeBuilder<PublicTopForecastRun> builder)
    {
        builder.ToTable("PublicTopForecastRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.ModelVersion).IsRequired().HasMaxLength(128).HasColumnName("model_version");
        builder.Property(x => x.ModelArtifactId).IsRequired().HasMaxLength(256).HasColumnName("model_artifact_id");
        builder.Property(x => x.TrainedAtUtc).IsRequired().HasColumnName("trained_at_utc");
        builder.Property(x => x.CalculatedAtUtc).IsRequired().HasColumnName("calculated_at_utc");
        builder.Property(x => x.SampleSize).IsRequired().HasColumnName("sample_size");
        builder.Property(x => x.TrainingSampleSize).IsRequired().HasColumnName("training_sample_size");
        builder.Property(x => x.ValidationSampleSize).IsRequired().HasColumnName("validation_sample_size");
        builder.Property(x => x.TestSampleSize).IsRequired().HasColumnName("test_sample_size");
        builder.Property(x => x.PositiveCount).IsRequired().HasColumnName("positive_count");
        builder.Property(x => x.PredictionsCount).IsRequired().HasColumnName("predictions_count");
        builder.Property(x => x.MinProbability).IsRequired().HasPrecision(9, 4).HasColumnName("min_probability");
        builder.Property(x => x.MetricsJson).IsRequired().HasColumnType("jsonb").HasColumnName("metrics_json");
        builder.Property(x => x.FeatureSchemaJson).IsRequired().HasColumnType("jsonb").HasColumnName("feature_schema_json");
        builder.Property(x => x.WarningsJson).IsRequired().HasColumnType("jsonb").HasColumnName("warnings_json");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasColumnName("status");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");

        builder.HasMany(x => x.Predictions)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.IdRun)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Predictions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.CalculatedAtUtc);
        builder.HasIndex(x => x.Status);
    }
}
