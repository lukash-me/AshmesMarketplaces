using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RuleConfiguration : IEntityTypeConfiguration<Rule>
{
    private const int NameMaxLength = 255;
    private const int CodeMaxLength = 128;

    public void Configure(EntityTypeBuilder<Rule> builder)
    {
        builder.ToTable("Rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(CodeMaxLength)
            .HasColumnName("code");

        builder.Property(x => x.Number)
            .HasColumnName("number");

        builder.Property(x => x.Domain)
            .IsRequired()
            .HasColumnName("domain");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");
    }
}
