using AshmesMarketplaces.Application.MarketRecommendations.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.MarketRecommendations;

public sealed class MarketHotProductsProductSelectionTests
{
    [Fact]
    public void SelectLatestProductRowsForDefaultScope_UsesNewestRowPerWbProductAcrossBatches()
    {
        var olderDuplicate = new HotProductsProductSelectionRow(
            Id: Guid.NewGuid(),
            WbProductId: "1001",
            ParserRunId: "batch_0001_products",
            ParsedAtUtc: new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc),
            SourceLineNumber: 1);
        var newerDuplicate = new HotProductsProductSelectionRow(
            Id: Guid.NewGuid(),
            WbProductId: "1001",
            ParserRunId: "batch_0009_products",
            ParsedAtUtc: new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc),
            SourceLineNumber: 1);
        var otherBatchProduct = new HotProductsProductSelectionRow(
            Id: Guid.NewGuid(),
            WbProductId: "1002",
            ParserRunId: "batch_0002_products",
            ParsedAtUtc: new DateTime(2026, 6, 17, 11, 0, 0, DateTimeKind.Utc),
            SourceLineNumber: 1);

        var selected = MarketHotProductsSnapshotBuilder
            .SelectLatestProductRowsForDefaultScope([olderDuplicate, otherBatchProduct, newerDuplicate], maxProducts: 100)
            .Select(x => (x.WbProductId, x.ParserRunId))
            .ToArray();

        Assert.Equal(
            [
                ("1002", "batch_0002_products"),
                ("1001", "batch_0009_products")
            ],
            selected);
    }

    [Fact]
    public void SelectLatestProductRowsForDefaultScope_RespectsMaxProductsAfterDeduplication()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(index => new HotProductsProductSelectionRow(
                Id: Guid.NewGuid(),
                WbProductId: $"100{index}",
                ParserRunId: $"batch_{index:0000}_products",
                ParsedAtUtc: new DateTime(2026, 6, 17, 10 + index, 0, 0, DateTimeKind.Utc),
                SourceLineNumber: index))
            .ToArray();

        var selected = MarketHotProductsSnapshotBuilder
            .SelectLatestProductRowsForDefaultScope(rows, maxProducts: 3)
            .Select(x => x.WbProductId)
            .ToArray();

        Assert.Equal(["1001", "1002", "1003"], selected);
    }
}
