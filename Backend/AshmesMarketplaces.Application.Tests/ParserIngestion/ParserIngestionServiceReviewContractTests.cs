using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Dtos;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserIngestion;

public sealed class ParserIngestionServiceReviewContractTests
{
    [Fact]
    public async Task ValidateReviewsAsync_AcceptsRootVariantFilteredAttribution()
    {
        await using var context = CreateContext();
        var service = new ParserIngestionService(context);
        var runDirectory = CreateReviewRunDirectory();

        try
        {
            var result = await service.ValidateReviewsAsync(
                runDirectory,
                new ParserIngestionOptions(),
                CancellationToken.None);

            Assert.Equal(2, result.RowsWritten);
            Assert.Equal(0, result.RowsSkipped);
            Assert.Equal(0, result.ErrorCount);
        }
        finally
        {
            Directory.Delete(runDirectory, recursive: true);
        }
    }

    private static string CreateReviewRunDirectory()
    {
        var runDirectory = Path.Combine(Path.GetTempPath(), $"parser-review-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runDirectory);

        File.WriteAllText(
            Path.Combine(runDirectory, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                parser_run_id = "wb_reviews_contract_test",
                marketplace = "wb",
                status = "succeeded",
                schema_version = 1,
                parser_version = "test",
                started_at_utc = "2026-07-06T19:00:00Z",
                finished_at_utc = "2026-07-06T19:00:01Z",
                counters = new { }
            }));
        File.WriteAllText(Path.Combine(runDirectory, "review_fetch_results.jsonl"), string.Empty);
        File.WriteAllText(Path.Combine(runDirectory, "review_replies.jsonl"), string.Empty);
        File.WriteAllText(Path.Combine(runDirectory, "errors.jsonl"), string.Empty);
        File.WriteAllText(Path.Combine(runDirectory, "runner.log"), string.Empty);
        File.WriteAllText(
            Path.Combine(runDirectory, "reviews.jsonl"),
            JsonSerializer.Serialize(new
            {
                schema_version = 1,
                parser_run_id = "wb_reviews_contract_test",
                parsed_at_utc = "2026-07-06T19:00:00Z",
                marketplace = "wb",
                source_wb_root_id = "698637250",
                wb_product_id = "682826795",
                review_attribution_mode = "root_variant_filtered",
                review_id_on_mp = "review-1",
                rating = 5,
                text = "ok",
                created_at_on_mp = "2026-07-06T18:00:00Z",
                raw_observed_fields = new[] { "id", "nmId", "text" }
            }) + Environment.NewLine);
        File.WriteAllText(
            Path.Combine(runDirectory, "review_coverage.jsonl"),
            JsonSerializer.Serialize(new
            {
                timestamp_utc = "2026-07-06T19:00:00Z",
                source_wb_product_id = "682826795",
                source_wb_root_id = "698637250",
                marketplace_feedback_count = 1,
                fetched_reviews_count = 1,
                coverage_status = "full",
                coverage_source = "root_variant_filtered"
            }) + Environment.NewLine);

        return runDirectory;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-ingestion-review-contract-{Guid.NewGuid()}")
            .Options;
        return new ParserIngestionReviewContractTestDbContext(options);
    }

    private sealed class ParserIngestionReviewContractTestDbContext : ApplicationDbContext
    {
        public ParserIngestionReviewContractTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
    }
}
