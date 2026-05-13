using AshmesMarketplaces.Domain.Entities.Finance;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdWorkspace)
            .IsRequired()
            .HasColumnName("id_workspace");

        builder.Property(x => x.IdCategory)
            .HasColumnName("id_category");

        builder.Property(x => x.IdCreator)
            .IsRequired()
            .HasColumnName("id_creator");

        builder.Property(x => x.IdResponsible)
            .HasColumnName("id_responsible");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.Cost)
            .HasPrecision(18, 2)
            .HasColumnName("cost");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DatePay)
            .HasColumnName("date_pay");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(x => x.IdWorkspace)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ExpenseCategory>()
            .WithMany()
            .HasForeignKey(x => x.IdCategory)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.IdCreator)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.IdResponsible)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
