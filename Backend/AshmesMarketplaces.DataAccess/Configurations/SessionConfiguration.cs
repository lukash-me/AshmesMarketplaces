using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdUser)
            .IsRequired()
            .HasColumnName("id_user");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(Constants.IP_ADDRESS_MAX_LENGTH)
            .HasColumnName("ip_address");

        builder.Property(x => x.Agent)
            .HasMaxLength(Constants.USER_AGENT_MAX_LENGTH)
            .HasColumnName("agent");

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(Constants.TOKEN_MAX_LENGTH)
            .HasColumnName("token");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateRefreshed)
            .HasColumnName("date_refreshed");

        builder.Property(x => x.DateExpires)
            .IsRequired()
            .HasColumnName("date_expires");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.IdUser)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
