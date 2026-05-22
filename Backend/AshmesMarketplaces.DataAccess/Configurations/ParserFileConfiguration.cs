using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserFileConfiguration : IEntityTypeConfiguration<ParserFile>
{
    public void Configure(EntityTypeBuilder<ParserFile> builder)
    {
        builder.ToTable("ParserFiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdParserRun).IsRequired().HasColumnName("id_parser_run");
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(96).HasColumnName("kind");
        builder.Property(x => x.Path).IsRequired().HasMaxLength(4096).HasColumnName("path");
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64).HasColumnName("sha256");
        builder.Property(x => x.ByteLength).IsRequired().HasColumnName("byte_length");
        builder.Property(x => x.RowCount).HasColumnName("row_count");
        builder.Property(x => x.DateRegisteredUtc).IsRequired().HasColumnName("date_registered_utc");

        builder.HasOne<ParserRun>()
            .WithMany()
            .HasForeignKey(x => x.IdParserRun)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
