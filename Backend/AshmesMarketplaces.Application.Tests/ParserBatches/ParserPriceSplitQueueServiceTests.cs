using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserBatches;

public sealed class ParserPriceSplitQueueServiceTests
{
    [Fact]
    public async Task EnsureJobAsync_is_idempotent_for_same_niche_and_proxy()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var request = new ParserPriceSplitEnsureJobRequest(
            "parser-1",
            "proxy-1",
            "Женщинам",
            "Платья и сарафаны",
            100,
            100000);

        var first = await service.EnsureJobAsync(request, CancellationToken.None);
        var second = await service.EnsureJobAsync(request, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(1, await context.ParserPriceSplitJobs.CountAsync());
        Assert.Equal(1, await context.ParserPriceSplitRanges.CountAsync());
    }

    [Fact]
    public async Task GetCurrentJobAsync_returns_existing_active_job_without_creating_ranges()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var ensure = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 100000),
            CancellationToken.None);

        var current = await service.GetCurrentJobAsync(
            new ParserPriceSplitCurrentJobRequest("parser-1", "proxy-1", "category", "niche"),
            CancellationToken.None);

        Assert.True(ensure.IsSuccess);
        Assert.True(current.IsSuccess);
        Assert.Equal(ensure.Value!.Id, current.Value!.Id);
        Assert.Equal(1, await context.ParserPriceSplitJobs.CountAsync());
        Assert.Equal(1, await context.ParserPriceSplitRanges.CountAsync());
    }

    [Fact]
    public async Task ClaimRangesAsync_does_not_claim_cooldown_ranges()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var job = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 100000),
            CancellationToken.None);
        var range = await context.ParserPriceSplitRanges.SingleAsync();
        range.MarkCooldown("rate limited", DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow);
        await context.SaveChangesAsync();

        var claim = await service.ClaimRangesAsync(
            new ParserPriceSplitClaimRangesRequest(job.Value!.Id, "parser-1", "proxy-1", 10),
            CancellationToken.None);

        Assert.True(claim.IsSuccess);
        Assert.Empty(claim.Value!.Ranges);
    }

    [Fact]
    public async Task SplitRangeAsync_creates_children_once()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var job = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 1000),
            CancellationToken.None);
        var parent = await context.ParserPriceSplitRanges.SingleAsync();

        var first = await service.SplitRangeAsync(
            parent.Id,
            new ParserPriceSplitSplitRangeRequest("parser-1", "proxy-1", [
                new ParserPriceSplitChildRangeRequest(100, 500, 900),
                new ParserPriceSplitChildRangeRequest(501, 1000, 800)
            ]),
            CancellationToken.None);
        var second = await service.SplitRangeAsync(
            parent.Id,
            new ParserPriceSplitSplitRangeRequest("parser-1", "proxy-1", [
                new ParserPriceSplitChildRangeRequest(100, 500, 900),
                new ParserPriceSplitChildRangeRequest(501, 1000, 800)
            ]),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(ParserPriceSplitRangeStatuses.CompletedSplit, first.Value!.Status);
        Assert.Equal(3, await context.ParserPriceSplitRanges.CountAsync());
        Assert.Equal(2, await context.ParserPriceSplitRanges.CountAsync(x => x.ParentRangeId == parent.Id));
        Assert.Equal(job.Value!.Id, first.Value.JobId);
    }

    [Fact]
    public async Task UpdateRangeAsync_completes_empty_range_and_counts_it_as_completed()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var job = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 1000),
            CancellationToken.None);
        var range = await context.ParserPriceSplitRanges.SingleAsync();

        var result = await service.UpdateRangeAsync(
            range.Id,
            new ParserPriceSplitUpdateRangeRequest(
                "parser-1",
                "proxy-1",
                ParserPriceSplitRangeStatuses.Completed,
                0,
                null,
                null,
                null,
                null),
            CancellationToken.None);
        var refreshedJob = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 1000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParserPriceSplitRangeStatuses.Completed, result.Value!.Status);
        Assert.Equal(0, result.Value.ExpectedTotal);
        Assert.Equal(1, refreshedJob.Value!.CompletedRangesCount);
    }

    [Fact]
    public async Task UpdateRangeAsync_preserves_cursor_for_partially_processed_range()
    {
        await using var context = CreateContext();
        var service = new ParserPriceSplitQueueService(context);
        var job = await service.EnsureJobAsync(
            new ParserPriceSplitEnsureJobRequest("parser-1", "proxy-1", "category", "niche", 100, 1000),
            CancellationToken.None);
        var range = await context.ParserPriceSplitRanges.SingleAsync();

        var partial = await service.UpdateRangeAsync(
            range.Id,
            new ParserPriceSplitUpdateRangeRequest(
                "parser-1",
                "proxy-1",
                ParserPriceSplitRangeStatuses.Pending,
                250,
                null,
                null,
                3,
                40),
            CancellationToken.None);
        var claim = await service.ClaimRangesAsync(
            new ParserPriceSplitClaimRangesRequest(job.Value!.Id, "parser-1", "proxy-1", 1),
            CancellationToken.None);

        Assert.True(partial.IsSuccess);
        Assert.Equal(ParserPriceSplitRangeStatuses.Pending, partial.Value!.Status);
        Assert.Equal(3, partial.Value.NextPage);
        Assert.Equal(40, partial.Value.NextItemOffset);
        Assert.True(claim.IsSuccess);
        Assert.Single(claim.Value!.Ranges);
        Assert.Equal(3, claim.Value.Ranges[0].NextPage);
        Assert.Equal(40, claim.Value.Ranges[0].NextItemOffset);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-price-split-{Guid.NewGuid()}")
            .Options;

        return new ParserPriceSplitTestDbContext(options);
    }

    private sealed class ParserPriceSplitTestDbContext : ApplicationDbContext
    {
        public ParserPriceSplitTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (entityType != typeof(ParserPriceSplitJob) && entityType != typeof(ParserPriceSplitRange))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<ParserPriceSplitJob>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserPriceSplitRange>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
