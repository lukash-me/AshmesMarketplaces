using System.Text.Json;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserBatchProcessingServiceTests
{
    [Fact]
    public async Task ProcessNextAsync_claims_processes_and_completes_batch()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var processor = new FakePayloadProcessor(_ => new ParserBatchPayloadProcessingSummary("test", 3, 3, 0, 0));
        var service = new ParserBatchProcessingService(context, processor);

        var result = await service.ProcessNextAsync(CancellationToken.None);
        var noWork = await service.ProcessNextAsync(CancellationToken.None);

        Assert.True(result.BatchClaimed);
        var storedError = await context.ParserBatchSubmissions.Select(x => x.Error).SingleAsync();
        Assert.True(
            string.Equals(ParserBatchStatuses.Completed, result.Status, StringComparison.Ordinal),
            $"status={result.Status ?? "<null>"}; message={result.Message ?? "<null>"}; stored={storedError ?? "<null>"}");
        Assert.False(noWork.BatchClaimed);
        Assert.Equal(ParserBatchStatuses.Completed, await context.ParserBatchSubmissions.Select(x => x.Status).SingleAsync());
        Assert.Equal(1, await context.ParserBatchSubmissions.Select(x => x.AttemptsCount).SingleAsync());
        Assert.Equal(3, await context.ParserBatchSubmissionEvents.CountAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_deletes_raw_payload_after_successful_processing_but_keeps_change_events()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var processor = new FakePayloadProcessor(_ =>
        {
            context.ParserProductChangeEvents.Add(new ParserProductChangeEvent(
                "100",
                "10",
                "batch-1",
                "Товары для дома",
                "Коврики для ванной",
                "price",
                "updated",
                "old",
                "new",
                """{"price":100}""",
                """{"price":110}""",
                DateTime.UtcNow));
            context.SaveChanges();
            return new ParserBatchPayloadProcessingSummary("test", 1, 1, 0, 1);
        });
        var service = new ParserBatchProcessingService(context, processor);

        var result = await service.ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ParserBatchStatuses.Completed, result.Status);
        Assert.Equal(0, await context.ParserBatchArtifacts.CountAsync());
        Assert.Equal(1, await context.ParserProductChangeEvents.CountAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_keeps_raw_payload_when_processing_fails()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var processor = new FakePayloadProcessor(_ => throw new TimeoutException("database timeout"));
        var service = new ParserBatchProcessingService(context, processor);

        var result = await service.ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ParserBatchStatuses.FailedRetryable, result.Status);
        Assert.Equal(1, await context.ParserBatchArtifacts.CountAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_retries_retryable_failure()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var calls = 0;
        var processor = new FakePayloadProcessor(_ =>
        {
            calls++;
            if (calls == 1)
                throw new TimeoutException("database timeout");

            return new ParserBatchPayloadProcessingSummary("test", 1, 1, 0, 0);
        });
        var service = new ParserBatchProcessingService(context, processor);

        var first = await service.ProcessNextAsync(CancellationToken.None);
        var second = await service.ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ParserBatchStatuses.FailedRetryable, first.Status);
        Assert.Equal(ParserBatchStatuses.Completed, second.Status);
        Assert.Equal(2, await context.ParserBatchSubmissions.Select(x => x.AttemptsCount).SingleAsync());
        Assert.Equal(ParserBatchStatuses.Completed, await context.ParserBatchSubmissions.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_marks_final_failure_without_endless_retry()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var processor = new FakePayloadProcessor(_ => throw new ParserBatchFinalException("unsupported schema"));
        var service = new ParserBatchProcessingService(context, processor);

        var first = await service.ProcessNextAsync(CancellationToken.None);
        var second = await service.ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ParserBatchStatuses.FailedFinal, first.Status);
        Assert.False(second.BatchClaimed);
        Assert.Equal(1, await context.ParserBatchSubmissions.Select(x => x.AttemptsCount).SingleAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_promotes_retryable_to_final_after_attempt_limit()
    {
        await using var context = CreateContext();
        await SubmitAsync(context, "batch-1");
        var processor = new FakePayloadProcessor(_ => throw new TimeoutException("database timeout"));
        var service = new ParserBatchProcessingService(context, processor);

        ParserBatchProcessingResult result = ParserBatchProcessingResult.NoWork();
        for (var i = 0; i < 5; i++)
            result = await service.ProcessNextAsync(CancellationToken.None);
        var afterLimit = await service.ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ParserBatchStatuses.FailedFinal, result.Status);
        Assert.False(afterLimit.BatchClaimed);
        Assert.Equal(5, await context.ParserBatchSubmissions.Select(x => x.AttemptsCount).SingleAsync());
    }

    private static async Task SubmitAsync(ApplicationDbContext context, string externalBatchId)
    {
        var queue = new ParserBatchQueueService(context);
        var result = await queue.SubmitAsync(CreateRequest(externalBatchId), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private static ParserBatchSubmitRequest CreateRequest(string externalBatchId)
    {
        using var document = JsonDocument.Parse("""{"schemaVersion":1,"artifacts":{"batch_manifest.json":"{}"}}""");
        return new ParserBatchSubmitRequest(
            "parser-1",
            externalBatchId,
            "Товары для дома",
            "Коврики для ванной",
            "local",
            "complete_card_batch",
            null,
            document.RootElement.Clone());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-batch-processing-{Guid.NewGuid()}")
            .Options;
        return new ParserBatchProcessingTestDbContext(options);
    }

    private sealed class FakePayloadProcessor : IParserBatchPayloadProcessor
    {
        private readonly Func<JsonElement, ParserBatchPayloadProcessingSummary> _handler;

        public FakePayloadProcessor(Func<JsonElement, ParserBatchPayloadProcessingSummary> handler)
        {
            _handler = handler;
        }

        public Task<ParserBatchPayloadProcessingSummary> ProcessAsync(
            ParserBatchSubmission batch,
            JsonElement payload,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(payload));
        }
    }

    private sealed class ParserBatchProcessingTestDbContext : ApplicationDbContext
    {
        public ParserBatchProcessingTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(ParserInstance),
                typeof(ParserNicheAssignment),
                typeof(ParserBatchSubmission),
                typeof(ParserBatchArtifact),
                typeof(ParserBatchSubmissionEvent),
                typeof(ParserProductChangeEvent)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<ParserInstance>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserNicheAssignment>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchSubmission>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchArtifact>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserBatchSubmissionEvent>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserProductChangeEvent>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
