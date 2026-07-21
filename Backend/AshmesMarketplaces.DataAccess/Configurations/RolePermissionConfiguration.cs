using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("Roles_Permissions");

        builder.HasKey(x => new { x.IdRole, x.IdPermission });

        builder.Property(x => x.IdRole)
            .ValueGeneratedNever()
            .HasColumnName("id_role");

        builder.Property(x => x.IdPermission)
            .ValueGeneratedNever()
            .HasColumnName("id_permission");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.IdRole)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(x => x.IdPermission)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
