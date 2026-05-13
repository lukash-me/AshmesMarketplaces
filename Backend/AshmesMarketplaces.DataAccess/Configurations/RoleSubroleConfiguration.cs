using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RoleSubroleConfiguration : IEntityTypeConfiguration<RoleSubrole>
{
    public void Configure(EntityTypeBuilder<RoleSubrole> builder)
    {
        builder.ToTable("Role_Subroles");

        builder.HasKey(x => new { x.IdRole, x.IdSubrole });

        builder.Property(x => x.IdRole)
            .HasColumnName("id_role");

        builder.Property(x => x.IdSubrole)
            .HasColumnName("id_subrole");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.IdRole)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.IdSubrole)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
