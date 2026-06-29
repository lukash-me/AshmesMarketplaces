using System.Text.Json;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserBatchQueueServiceTests
{
    [Fact]
    public async Task SubmitAsync_stores_batch_and_returns_accepted_without_processing_it()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);

        var result = await service.SubmitAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("accepted", result.Value!.Status);
        Assert.Equal(1, await context.ParserBatchSubmissions.CountAsync());
        Assert.Equal(1, await context.ParserBatchArtifacts.CountAsync());
        Assert.Null(await context.ParserBatchSubmissions.Select(x => x.ProcessingStartedAtUtc).SingleAsync());
        Assert.Null(await context.ParserBatchSubmissions.Select(x => x.CompletedAtUtc).SingleAsync());
    }

    [Fact]
    public async Task SubmitAsync_is_idempotent_for_same_parser_instance_batch_id_and_hash()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);
        var request = CreateRequest();

        var first = await service.SubmitAsync(request, CancellationToken.None);
        var second = await service.SubmitAsync(request, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(1, await context.ParserBatchSubmissions.CountAsync());
        Assert.Equal(1, await context.ParserBatchArtifacts.CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_rejects_duplicate_batch_id_with_different_payload_hash()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);

        var first = await service.SubmitAsync(CreateRequest(), CancellationToken.None);
        var second = await service.SubmitAsync(CreateRequest(payload: """{"items":[{"wbProductId":"2"}]}"""), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(1, await context.ParserBatchSubmissions.CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_rejects_request_when_content_hash_does_not_match_payload()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);

        var result = await service.SubmitAsync(CreateRequest(contentHash: "sha256:bad"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.ParserBatchSubmissions.CountAsync());
    }

    [Fact]
    public async Task GetStatusAsync_returns_batch_status_by_external_batch_id()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);
        await service.SubmitAsync(CreateRequest(), CancellationToken.None);

        var result = await service.GetStatusAsync("batch-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("parser-1", result.Value!.ParserInstanceId);
        Assert.Equal("batch-1", result.Value.ExternalBatchId);
        Assert.Equal("accepted", result.Value.Status);
    }

    [Fact]
    public async Task GetPendingAcksAsync_returns_completed_or_failed_batches_not_acknowledged_by_parser()
    {
        await using var context = CreateContext();
        var service = new ParserBatchQueueService(context);
        await service.SubmitAsync(CreateRequest(externalBatchId: "completed"), CancellationToken.None);
        await service.SubmitAsync(CreateRequest(externalBatchId: "queued"), CancellationToken.None);

        var completed = await context.ParserBatchSubmissions.SingleAsync(x => x.ExternalBatchId == "completed");
        completed.MarkCompleted(DateTime.UtcNow);
        await context.SaveChangesAsync();

        var result = await service.GetPendingAcksAsync("parser-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ack = Assert.Single(result.Value!.Items);
        Assert.Equal("completed", ack.ExternalBatchId);
        Assert.Equal("completed", ack.Status);
    }

    private static ParserBatchSubmitRequest CreateRequest(
        string externalBatchId = "batch-1",
        string payload = """{"items":[{"wbProductId":"1"}]}""",
        string? contentHash = null)
    {
        using var document = JsonDocument.Parse(payload);
        return new ParserBatchSubmitRequest(
            "parser-1",
            externalBatchId,
            "Товары для дома",
            "Коврики для ванной",
            "local-proxy",
            "full",
            contentHash,
            document.RootElement.Clone());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-batches-{Guid.NewGuid()}")
            .Options;

        return new ParserBatchTestDbContext(options);
    }

    private sealed class ParserBatchTestDbContext : ApplicationDbContext
    {
        public ParserBatchTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
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
                typeof(ParserBatchSubmissionEvent)
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
        }
    }
}
