using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class UserWorkspaceConfiguration : IEntityTypeConfiguration<UserWorkspace>
{
    public void Configure(EntityTypeBuilder<UserWorkspace> builder)
    {
        builder.ToTable("Users_Workspaces");

        builder.HasKey(x => new { x.IdUser, x.IdWorkspace });

        builder.Property(x => x.IdUser)
            .ValueGeneratedNever()
            .HasColumnName("id_user");

        builder.Property(x => x.IdWorkspace)
            .ValueGeneratedNever()
            .HasColumnName("id_workspace");

        builder.Property(x => x.IdRole)
            .IsRequired()
            .HasColumnName("id_role");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.IdUser)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(x => x.IdWorkspace)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.IdRole)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
