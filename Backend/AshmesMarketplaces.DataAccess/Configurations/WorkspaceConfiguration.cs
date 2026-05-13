using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    private const int NameMaxLength = 255;

    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.IdBrand)
            .HasColumnName("id_brand");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength)
            .HasColumnName("name");

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.Property(x => x.UrlInvite)
            .HasMaxLength(Constants.URL_MAX_LENGTH)
            .HasColumnName("url_invite");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(x => x.IdBrand)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
