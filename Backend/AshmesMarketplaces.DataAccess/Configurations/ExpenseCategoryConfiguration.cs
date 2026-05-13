using AshmesMarketplaces.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("Categories_Expenses");

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

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");
    }
}
