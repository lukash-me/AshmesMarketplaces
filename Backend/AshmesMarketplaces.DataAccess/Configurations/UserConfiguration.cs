using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdRole)
            .IsRequired()
            .HasColumnName("id_role");

        builder.Property(x => x.Login)
            .IsRequired()
            .HasMaxLength(Constants.LOGIN_MAX_LENGTH)
            .HasColumnName("login");

        builder.Property(x => x.Password)
            .IsRequired()
            .HasMaxLength(Constants.PASSWORD_MAX_LENGTH)
            .HasColumnName("password");

        builder.Property(x => x.Email)
            .HasMaxLength(Constants.EMAIL_MAX_LENGTH)
            .HasColumnName("email");

        builder.Property(x => x.Phone)
            .IsRequired()
            .HasMaxLength(Constants.PHONE_MAX_LENGTH)
            .HasColumnName("phone");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateLogin)
            .IsRequired()
            .HasColumnName("date_login");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.IdRole)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
