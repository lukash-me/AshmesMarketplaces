using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RuleSetConfiguration : IEntityTypeConfiguration<RuleSet>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<RuleSet> builder)
    {
        builder.ToTable("Sets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");
    }
}
