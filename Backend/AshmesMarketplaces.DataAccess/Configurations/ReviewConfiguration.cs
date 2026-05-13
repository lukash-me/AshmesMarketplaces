using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Reviews;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.IdProduct)
            .HasConversion(id => id.Value, value => ProductId.Create(value))
            .IsRequired()
            .HasColumnName("id_product");

        builder.Property(x => x.IdOnMp)
            .IsRequired()
            .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
            .HasColumnName("id_on_mp");

        builder.Property(x => x.Rating)
            .IsRequired()
            .HasColumnName("rating");

        builder.Property(x => x.Text)
            .HasColumnType("text")
            .HasColumnName("text");

        builder.Property(x => x.IsReplied)
            .IsRequired()
            .HasColumnName("is_replied");

        builder.Property(x => x.DateCreate)
            .IsRequired()
            .HasColumnName("date_create");

        builder.Property(x => x.DateReply)
            .HasColumnName("date_reply");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.IdProduct)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
