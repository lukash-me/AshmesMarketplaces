using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserObservability;

public sealed class ParserReviewReadServiceTests
{
    [Fact]
    public async Task GetListAsync_treats_reply_from_newer_parser_run_as_observed_reply()
    {
        await using var context = CreateContext();
        AddReviewRow(context, "1001", "review-1", parserRunId: "old-run");
        AddReviewReplyRow(context, "1001", "review-1", "reply-1", parserRunId: "new-run");
        await context.SaveChangesAsync();
        var service = new ParserReviewReadService(context);

        var result = await service.GetListAsync(
            new ParserReviewListQuery
            {
                WbProductId = "1001",
                HasObservedReply = true,
                Page = 1,
                PageSize = 10
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("review-1", item.ReviewIdOnMp);
        Assert.True(item.HasObservedReply);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"parser-review-read-{Guid.NewGuid()}")
            .Options;

        return new ParserReviewReadTestDbContext(options);
    }

    private static void AddReviewRow(
        ApplicationDbContext context,
        string wbProductId,
        string reviewId,
        string parserRunId)
    {
        context.ParserReviewRows.Add(new ParserReviewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            sourceLineNumber: 1,
            rowHash: $"review-row-{wbProductId}-{reviewId}",
            schemaVersion: 1,
            parserRunId: parserRunId,
            parsedAtUtc: Utc(2026, 7, 4, 13),
            marketplace: "wildberries",
            inputProductsParserRunId: null,
            inputProductsJsonl: null,
            sourceWbRootId: $"root-{wbProductId}",
            wbProductId: wbProductId,
            reviewAttributionMode: "product_full",
            reviewIdOnMp: reviewId,
            rating: 5,
            text: "review",
            pros: null,
            cons: null,
            createdAtOnMp: Utc(2026, 7, 4, 10),
            reviewerName: null,
            reviewerCountry: null,
            reviewerHasPhoto: null,
            helpfulPlus: null,
            helpfulMinus: null,
            sourceCategory: "Женщинам",
            sourceSubcategory: "Платья и сарафаны",
            sourceQuery: "Платья и сарафаны",
            sourceRegionDest: "12354108",
            rawObservedFields: null,
            isPartialSnapshot: false,
            isCappedRootPayload: false,
            isFullHistoryUnknown: false));
    }

    private static void AddReviewReplyRow(
        ApplicationDbContext context,
        string wbProductId,
        string reviewId,
        string replyId,
        string parserRunId)
    {
        context.ParserReviewReplyRows.Add(new ParserReviewReplyRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            sourceLineNumber: 1,
            rowHash: $"reply-row-{wbProductId}-{reviewId}-{replyId}",
            schemaVersion: 1,
            parserRunId: parserRunId,
            parsedAtUtc: Utc(2026, 7, 4, 14),
            marketplace: "wildberries",
            inputProductsParserRunId: null,
            inputProductsJsonl: null,
            sourceWbRootId: $"root-{wbProductId}",
            wbProductId: wbProductId,
            reviewAttributionMode: "product_full",
            reviewIdOnMp: reviewId,
            replyIdOnMp: replyId,
            replyFallbackHash: null,
            text: "reply",
            createdAtOnMp: Utc(2026, 7, 4, 14),
            updatedAtOnMp: null,
            replyAuthor: "seller",
            replyState: null,
            sourceCategory: "Женщинам",
            sourceSubcategory: "Платья и сарафаны",
            sourceQuery: "Платья и сарафаны",
            sourceRegionDest: "12354108",
            rawObservedFields: null,
            isPartialSnapshot: false,
            isCappedRootPayload: false,
            isFullHistoryUnknown: false));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class ParserReviewReadTestDbContext : ApplicationDbContext
    {
        public ParserReviewReadTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var allowedTypes = new HashSet<Type>
            {
                typeof(ParserReviewRow),
                typeof(ParserReviewReplyRow)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
            {
                if (!allowedTypes.Contains(entityType))
                    modelBuilder.Ignore(entityType);
            }

            modelBuilder.Entity<ParserReviewRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserReviewReplyRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
