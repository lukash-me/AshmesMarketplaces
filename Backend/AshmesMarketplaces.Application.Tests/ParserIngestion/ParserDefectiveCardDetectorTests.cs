using System.Text.Json;
using AshmesMarketplaces.Application.ParserIngestion.Services;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.ParserIngestion;

public sealed class ParserDefectiveCardDetectorTests
{
    [Fact]
    public void Evaluate_AllowsSuccessfulDetailsWithEmptyDescriptionAndCharacteristics()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.False(result.IsDefective);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Evaluate_AllowsCardWithNoReviewRows_WhenReviewFetchWasAttempted()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.DoesNotContain(ParserDefectiveCardReasons.MissingReviewFetch, result.Reasons);
        Assert.False(result.IsDefective);
    }

    [Fact]
    public void Evaluate_RejectsMissingSuccessfulProductDetails()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: false,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.True(result.IsDefective);
        Assert.Contains(ParserDefectiveCardReasons.MissingDetails, result.Reasons);
    }

    [Fact]
    public void Evaluate_RejectsMissingImages()
    {
        using var row = ProductRow(imageUrls: [], imageCount: 0, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.True(result.IsDefective);
        Assert.Contains(ParserDefectiveCardReasons.MissingImages, result.Reasons);
    }

    [Fact]
    public void Evaluate_RejectsMissingLogisticsAttempt()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: false,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.True(result.IsDefective);
        Assert.Contains(ParserDefectiveCardReasons.MissingLogistics, result.Reasons);
    }

    [Fact]
    public void Evaluate_RejectsMissingReviewFetchAttempt()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: "root-1");
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: false);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.True(result.IsDefective);
        Assert.Contains(ParserDefectiveCardReasons.MissingReviewFetch, result.Reasons);
    }

    [Fact]
    public void Evaluate_RejectsMissingRootIdentity()
    {
        using var row = ProductRow(imageUrls: ["https://cdn.example/1.webp"], imageCount: 1, wbRootId: null);
        var evidence = new ParserCardCompletenessEvidence(
            HasSuccessfulProductDetails: true,
            HasLogisticsAttempt: true,
            HasReviewFetchAttempt: true);

        var result = ParserDefectiveCardDetector.Evaluate(row, evidence);

        Assert.True(result.IsDefective);
        Assert.Contains(ParserDefectiveCardReasons.InvalidProductIdentity, result.Reasons);
    }

    private static ParserProductRow ProductRow(
        string[] imageUrls,
        int? imageCount,
        string? wbRootId)
    {
        return new ParserProductRow(
            idParserRun: Guid.NewGuid(),
            idParserFile: Guid.NewGuid(),
            sourceLineNumber: 1,
            rowHash: "hash",
            schemaVersion: 1,
            marketplace: "wildberries",
            parserRunId: "parser-run",
            parsedAtUtc: new DateTime(2026, 06, 15, 10, 00, 00, DateTimeKind.Utc),
            sourceCategory: "Home",
            sourceSubcategory: "Bath mats",
            sourceQuery: "bath mat",
            sourceRegionDest: "12354108",
            wbProductId: "1142540469",
            skuProduct: "1142540469",
            name: "WB product",
            entity: null,
            brandIdOnMp: null,
            brandName: null,
            sellerIdOnMp: null,
            sellerName: null,
            priceRegular: 1000,
            priceDiscounted: 900,
            priceWbWallet: 850,
            discountPercent: 10,
            totalQuantity: 12,
            ratingRounded: 5,
            reviewRating: 4.8m,
            feedbackCount: 42,
            feedbackCountSource: "wb",
            imageUrls: JsonDocument.Parse(JsonSerializer.Serialize(imageUrls)),
            imageCount: imageCount,
            wbRootId: wbRootId,
            subjectParentId: null,
            subjectId: null,
            rawObservedFields: null);
    }
}
