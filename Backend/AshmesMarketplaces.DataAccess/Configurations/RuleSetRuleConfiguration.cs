using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RuleSetRuleConfiguration : IEntityTypeConfiguration<RuleSetRule>
{
    public void Configure(EntityTypeBuilder<RuleSetRule> builder)
    {
        builder.ToTable("Sets_Rules");

        builder.HasKey(x => new { x.IdSet, x.IdRule });

        builder.Property(x => x.IdSet)
            .ValueGeneratedNever()
            .HasColumnName("id_set");

        builder.Property(x => x.IdRule)
            .ValueGeneratedNever()
            .HasColumnName("id_rule");

        builder.HasOne<RuleSet>()
            .WithMany()
            .HasForeignKey(x => x.IdSet)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Rule>()
            .WithMany()
            .HasForeignKey(x => x.IdRule)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
