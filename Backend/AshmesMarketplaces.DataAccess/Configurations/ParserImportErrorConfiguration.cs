using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AshmesMarketplaces.DataAccess.Configurations;

public sealed class ParserImportErrorConfiguration : IEntityTypeConfiguration<ParserImportError>
{
    public void Configure(EntityTypeBuilder<ParserImportError> builder)
    {
        builder.ToTable("ParserImportErrors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");
        builder.Property(x => x.IdImportExecution).IsRequired().HasColumnName("id_import_execution");
        builder.Property(x => x.IdParserRun).HasColumnName("id_parser_run");
        builder.Property(x => x.IdParserFile).HasColumnName("id_parser_file");
        builder.Property(x => x.SourceLineNumber).HasColumnName("source_line_number");
        builder.Property(x => x.Phase).IsRequired().HasMaxLength(96).HasColumnName("phase");
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(32).HasColumnName("severity");
        builder.Property(x => x.Message).IsRequired().HasColumnType("text").HasColumnName("message");
        builder.Property(x => x.Details).HasColumnType("jsonb").HasColumnName("details");
        builder.Property(x => x.DateCreatedUtc).IsRequired().HasColumnName("date_created_utc");

        builder.HasOne<ParserImportExecution>()
            .WithMany()
            .HasForeignKey(x => x.IdImportExecution)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParserRun>()
            .WithMany()
            .HasForeignKey(x => x.IdParserRun)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParserFile>()
            .WithMany()
            .HasForeignKey(x => x.IdParserFile)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
