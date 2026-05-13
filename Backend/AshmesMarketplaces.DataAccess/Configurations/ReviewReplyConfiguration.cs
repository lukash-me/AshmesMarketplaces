using AshmesMarketplaces.Domain.Entities.Reviews;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ReviewReplyConfiguration : IEntityTypeConfiguration<ReviewReply>
{
    public void Configure(EntityTypeBuilder<ReviewReply> builder)
    {
        builder.ToTable("Reply");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdReview)
            .IsRequired()
            .HasColumnName("id_review");

        builder.Property(x => x.IdOnMp)
            .IsRequired()
            .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
            .HasColumnName("id_on_mp");

        builder.Property(x => x.Text)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("text");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateUpdate)
            .IsRequired()
            .HasColumnName("date_update");

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(x => x.IdReview)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
