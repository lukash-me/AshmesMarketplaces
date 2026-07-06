using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleConstructor.Dtos;
using AshmesMarketplaces.Application.RuleConstructor.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.RuleConstructor;

public sealed class RuleConstructorServiceTests
{
    [Fact]
    public async Task GetFiltersAsync_returns_initial_catalog_with_disabled_unavailable_rules()
    {
        await using var context = CreateContext();
        var service = new RuleConstructorService(context);

        var result = await service.GetFiltersAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var filters = result.Value!;
        Assert.Contains(filters, x =>
            x.Id == "bad_recent_reviews" &&
            x.Name == "Плохие последние" &&
            x.Status == RuleConstructorFilterStatuses.Active &&
            x.RequiresReviewByUser);
        Assert.Contains(filters, x =>
            x.Id == "characteristics_less_than_cluster" &&
            x.Group == "Оформление карточки" &&
            x.Name == "Характеристик меньше относительно похожих" &&
            x.Status == RuleConstructorFilterStatuses.Disabled &&
            !string.IsNullOrWhiteSpace(x.UnavailableReason));
        Assert.Contains(filters, x =>
            x.Id == "high_price_relative_to_niche" &&
            x.Name == "Высокая относительно ниши");
        Assert.Contains(filters, x =>
            x.Id == "price_increased" &&
            x.Name == "Выросла");
        Assert.Contains(filters, x =>
            x.Id == "in_top_700" &&
            x.Name == "Топ 700");
        Assert.Contains(filters, x =>
            x.Id == "beyond_top_700" &&
            x.Name == "Не топ 700");
        Assert.Contains(filters, x =>
            x.Id == "slower_delivery_than_similar" &&
            x.Group == "Логистика" &&
            x.Name == "Медленнее похожих");
        Assert.DoesNotContain(filters, x => x.Group == "Характеристики");
        Assert.DoesNotContain(filters, x => x.Id == "high_price_without_review_or_position_advantage");
        Assert.DoesNotContain(filters, x => x.Id == "position_unknown");

        Assert.Contains(filters, x => x.Id == "good_recent_reviews" && x.Tone == RuleConstructorFilterTones.Positive);
        Assert.Contains(filters, x => x.Id == "in_top_700" && x.Tone == RuleConstructorFilterTones.Positive);
        Assert.Contains(filters, x => x.Id == "bad_recent_reviews" && x.Tone == RuleConstructorFilterTones.Negative);
        Assert.Contains(filters, x => x.Id == "high_price_relative_to_niche" && x.Tone == RuleConstructorFilterTones.Neutral);
        Assert.Contains(filters, x => x.Id == "price_increased" && x.Tone == RuleConstructorFilterTones.Neutral);
        Assert.Contains(filters, x =>
            x.Id == "good_recent_reviews" &&
            x.VerificationStatus == RuleConstructorFilterVerificationStatuses.NeedsDataExport);
        Assert.Contains(filters, x =>
            x.Id == "bad_recent_reviews" &&
            x.VerificationStatus == RuleConstructorFilterVerificationStatuses.NeedsDataExport);
        Assert.All(
            filters.Where(x => x.Id is not ("good_recent_reviews" or "bad_recent_reviews")),
            filter => Assert.Equal(RuleConstructorFilterVerificationStatuses.NotReady, filter.VerificationStatus));

        var cardDesignRuleIds = filters
            .Where(x => x.Group == "Оформление карточки")
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(new[]
        {
            "no_characteristics",
            "characteristics_less_than_niche",
            "characteristics_less_than_cluster",
            "short_description",
            "no_description",
            "few_images"
        }.All(cardDesignRuleIds.Contains));
    }

    [Fact]
    public async Task SearchAsync_rejects_disabled_filters()
    {
        await using var context = CreateContext();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["characteristics_less_than_cluster"],
                "all",
                null,
                null,
                null,
                1,
                20),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
        Assert.Contains("недоступен", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_all_mode_returns_only_products_matching_every_active_rule()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Платья и сарафаны", price: 3000, reviewRating: 3.2m, feedbackCount: 20, totalQuantity: 5);
        AddCurrentProduct(context, "1002", "Платья и сарафаны", price: 1200, reviewRating: 3.1m, feedbackCount: 15, totalQuantity: 40);
        AddCurrentProduct(context, "1003", "Платья и сарафаны", price: 900, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 30);
        AddReviewRow(context, "1001", 2, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1002", 2, Utc(2026, 7, 4, 10));
        AddReviewSummary(context, "1001", recentNegativeCount: 2, averageRating: 3.2m);
        AddReviewSummary(context, "1002", recentNegativeCount: 2, averageRating: 3.1m);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["bad_recent_reviews", "high_price_relative_to_niche"],
                "all",
                null,
                "Платья и сарафаны",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("1001", item.WbProductId);
        Assert.DoesNotContain(item.MatchedFacts, x => x.Id is "score" or "confidence");
    }

    [Fact]
    public async Task SearchAsync_any_mode_returns_products_matching_at_least_one_rule()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Платья и сарафаны", price: 3000, reviewRating: 4.8m, feedbackCount: 70, totalQuantity: 5);
        AddCurrentProduct(context, "1002", "Платья и сарафаны", price: 900, reviewRating: 3.0m, feedbackCount: 8, totalQuantity: 40);
        AddCurrentProduct(context, "1003", "Платья и сарафаны", price: 1000, reviewRating: 4.9m, feedbackCount: 80, totalQuantity: 30);
        AddReviewRow(context, "1002", 2, Utc(2026, 7, 4, 10));
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 3.0m);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["bad_recent_reviews", "high_price_relative_to_niche"],
                "any",
                null,
                "Платья и сарафаны",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["1001", "1002"], result.Value!.Items.Select(x => x.WbProductId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_good_recent_reviews_uses_last_ten_saved_review_ratings()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Платья и сарафаны", price: 1000, reviewRating: 3.2m, feedbackCount: 200, totalQuantity: 10);
        AddCurrentProduct(context, "1002", "Платья и сарафаны", price: 1000, reviewRating: 4.9m, feedbackCount: 200, totalQuantity: 10);
        AddCurrentProduct(context, "1003", "Платья и сарафаны", price: 1000, reviewRating: 4.8m, feedbackCount: 3, totalQuantity: 10);
        AddCurrentProduct(context, "1004", "Платья и сарафаны", price: 1000, reviewRating: 4.8m, feedbackCount: 0, totalQuantity: 10);

        for (var index = 0; index < 10; index++)
            AddReviewRow(context, "1001", index % 2 == 0 ? 4 : 5, Utc(2026, 7, 4, 10).AddMinutes(index));
        AddReviewRow(context, "1001", 1, Utc(2026, 7, 3, 10));

        AddReviewRow(context, "1002", 5, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1002", 3, Utc(2026, 7, 4, 11));

        AddReviewRow(context, "1003", 4, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1003", 5, Utc(2026, 7, 4, 11));
        AddReviewRow(context, "1003", 4, Utc(2026, 7, 4, 12));
        AddReviewSummary(context, "1001", recentNegativeCount: 1, averageRating: 3.2m, reviewsCount: 11, marketplaceFeedbackCount: 11, coverageStatus: "full");
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 4.9m, reviewsCount: 2, marketplaceFeedbackCount: 2, coverageStatus: "full");
        AddReviewSummary(context, "1003", recentNegativeCount: 0, averageRating: 4.4m, reviewsCount: 3, marketplaceFeedbackCount: 3, coverageStatus: "full");

        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["good_recent_reviews"],
                "all",
                null,
                "Платья и сарафаны",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["1001", "1003"], result.Value!.Items.Select(x => x.WbProductId).Order(StringComparer.Ordinal));
        Assert.Contains(result.Value.Items.Single(x => x.WbProductId == "1001").MatchedFacts, x =>
            x.Id == "good_recent_reviews" &&
            x.Value == "Последние 10 отзывов: все оценки 4 или 5");
        Assert.Contains(result.Value.Items.Single(x => x.WbProductId == "1003").MatchedFacts, x =>
            x.Id == "good_recent_reviews" &&
            x.Value == "Последние 3 отзыва: все оценки 4 или 5");
    }

    [Fact]
    public async Task SearchAsync_bad_recent_reviews_uses_last_ten_saved_review_ratings()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 1000, reviewRating: 4.9m, feedbackCount: 200, totalQuantity: 10);
        AddCurrentProduct(context, "1002", "Dresses", price: 1000, reviewRating: 3.2m, feedbackCount: 200, totalQuantity: 10);
        AddCurrentProduct(context, "1003", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 3, totalQuantity: 10);
        AddCurrentProduct(context, "1004", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 0, totalQuantity: 10);
        AddCurrentProduct(context, "1005", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 3, totalQuantity: 10);

        AddReviewRow(context, "1001", 5, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1001", 3, Utc(2026, 7, 4, 11));

        for (var index = 0; index < 10; index++)
            AddReviewRow(context, "1002", index % 2 == 0 ? 4 : 5, Utc(2026, 7, 4, 10).AddMinutes(index));
        AddReviewRow(context, "1002", 1, Utc(2026, 7, 3, 10));

        AddReviewRow(context, "1003", 5, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1003", 4, Utc(2026, 7, 4, 11));
        AddReviewRow(context, "1003", 2, Utc(2026, 7, 4, 12));

        AddReviewRow(context, "1005", 2, Utc(2026, 7, 4, 10));

        AddReviewSummary(context, "1001", recentNegativeCount: 1, averageRating: 4.0m, reviewsCount: 2, marketplaceFeedbackCount: 2, coverageStatus: "full");
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 3.2m, reviewsCount: 11, marketplaceFeedbackCount: 11, coverageStatus: "full");
        AddReviewSummary(context, "1003", recentNegativeCount: 1, averageRating: 3.7m, reviewsCount: 3, marketplaceFeedbackCount: 3, coverageStatus: "full");
        AddReviewSummary(context, "1005", recentNegativeCount: 1, averageRating: 2.0m, reviewsCount: 1, marketplaceFeedbackCount: 3, coverageStatus: "incomplete");

        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["bad_recent_reviews"],
                "all",
                null,
                "Dresses",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["1001", "1003"], result.Value!.Items.Select(x => x.WbProductId).Order(StringComparer.Ordinal));
        Assert.All(result.Value.Items.SelectMany(x => x.MatchedFacts).Where(x => x.Id == "bad_recent_reviews"), fact =>
        {
            Assert.DoesNotContain("4.9", fact.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("3.2", fact.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("RecentNegativeCount", fact.Value, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SearchAsync_no_characteristics_matches_products_without_detail_characteristics()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Платья и сарафаны", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 10);
        AddCurrentProduct(context, "1002", "Платья и сарафаны", price: 1100, reviewRating: 4.6m, feedbackCount: 70, totalQuantity: 10);
        AddDetails(context, "1002", description: "Подробное описание товара", characteristicsCount: 3);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["no_characteristics"],
                "all",
                null,
                "Платья и сарафаны",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("1001", item.WbProductId);
        Assert.Contains(item.MatchedFacts, x => x.Id == "no_characteristics");
    }

    [Fact]
    public async Task SearchAsync_no_characteristics_ignores_string_encoded_characteristics()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 10);
        AddStringEncodedDetails(context, "1001", description: "Detailed product description", characteristicsCount: 3);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                ["no_characteristics"],
                "all",
                null,
                "Dresses",
                null,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task SearchAsync_nested_expression_filters_products_by_group_logic()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 3000, reviewRating: 3.2m, feedbackCount: 20, totalQuantity: 5);
        AddCurrentProduct(context, "1002", "Dresses", price: 900, reviewRating: 3.1m, feedbackCount: 15, totalQuantity: 40);
        AddCurrentProduct(context, "1003", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 0);
        AddReviewRow(context, "1001", 2, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1002", 2, Utc(2026, 7, 4, 10));
        AddReviewSummary(context, "1001", recentNegativeCount: 2, averageRating: 3.2m);
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 3.1m);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);
        var expression = Group("or",
            Group("and", Rule("bad_recent_reviews"), Rule("high_price_relative_to_niche")),
            Rule("no_stock"));

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                1,
                20,
                expression),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["1001", "1003"], result.Value!.Items.Select(x => x.WbProductId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_expression_returns_all_facts_for_current_page_items()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 3000, reviewRating: 3.2m, feedbackCount: 20, totalQuantity: 5);
        AddCurrentProduct(context, "1002", "Dresses", price: 900, reviewRating: 3.1m, feedbackCount: 15, totalQuantity: 40);
        AddCurrentProduct(context, "1003", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 30);
        AddReviewRow(context, "1001", 2, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1002", 2, Utc(2026, 7, 4, 10));
        AddReviewSummary(context, "1001", recentNegativeCount: 2, averageRating: 3.2m);
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 3.1m);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var firstPage = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                1,
                1,
                Rule("bad_recent_reviews")),
            CancellationToken.None);
        var secondPage = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                2,
                1,
                Rule("bad_recent_reviews")),
            CancellationToken.None);

        Assert.True(firstPage.IsSuccess);
        Assert.True(secondPage.IsSuccess);
        Assert.Equal(2, firstPage.Value!.Total);
        var item = Assert.Single(firstPage.Value.Items);
        Assert.Equal("1001", item.WbProductId);
        Assert.Contains(item.MatchedFacts, x => x.Id == "bad_recent_reviews");
        Assert.Contains(item.MatchedFacts, x => x.Id == "low_stock");
        Assert.DoesNotContain(item.MatchedFacts, x => x.Id is "high_price_without_review_or_position_advantage" or "position_unknown");
        Assert.Equal("1002", Assert.Single(secondPage.Value!.Items).WbProductId);
    }

    [Fact]
    public async Task SearchAsync_empty_expression_returns_all_products_in_base_selection()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 10);
        AddCurrentProduct(context, "1002", "Dresses", price: 1100, reviewRating: 4.6m, feedbackCount: 70, totalQuantity: 10);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                1,
                20,
                Group("and")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Total);
    }

    [Fact]
    public async Task SearchAsync_returns_parser_product_row_id_for_detail_drawer()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 10);
        await context.SaveChangesAsync();
        var currentRow = await context.ParserCurrentProductRows.SingleAsync(x => x.WbProductId == "1001");
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                1,
                20,
                Group("and")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(currentRow.ProductRowId, item.Id);
        Assert.NotEqual(currentRow.Id, item.Id);
    }

    [Fact]
    public async Task SearchAsync_uses_wb_image_fallback_when_current_image_urls_are_empty()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1142965384", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 10, imageUrls: null);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                "Dresses",
                null,
                1,
                20,
                Group("and")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(
            "https://basket-43.wbbasket.ru/vol11429/part1142965/1142965384/images/big/1.webp",
            item.ThumbnailUrl);
    }

    [Fact]
    public async Task SearchAsync_rejects_disabled_rule_in_expression()
    {
        await using var context = CreateContext();
        var service = new RuleConstructorService(context);

        var result = await service.SearchAsync(
            new RuleConstructorSearchRequest(
                [],
                "all",
                null,
                null,
                null,
                1,
                20,
                Rule("characteristics_less_than_cluster")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.BadRequest, result.Error!.Type);
    }

    [Fact]
    public async Task GetCountsAsync_returns_expression_total_and_counts_for_rules_added_to_active_group()
    {
        await using var context = CreateContext();
        AddCurrentProduct(context, "1001", "Dresses", price: 3000, reviewRating: 3.2m, feedbackCount: 20, totalQuantity: 5);
        AddCurrentProduct(context, "1002", "Dresses", price: 900, reviewRating: 3.1m, feedbackCount: 15, totalQuantity: 40);
        AddCurrentProduct(context, "1003", "Dresses", price: 1000, reviewRating: 4.8m, feedbackCount: 80, totalQuantity: 30);
        AddReviewRow(context, "1001", 2, Utc(2026, 7, 4, 10));
        AddReviewRow(context, "1002", 2, Utc(2026, 7, 4, 10));
        AddReviewSummary(context, "1001", recentNegativeCount: 2, averageRating: 3.2m);
        AddReviewSummary(context, "1002", recentNegativeCount: 1, averageRating: 3.1m);
        await context.SaveChangesAsync();
        var service = new RuleConstructorService(context);

        var result = await service.GetCountsAsync(
            new RuleConstructorCountsRequest(
                Group("and", Rule("bad_recent_reviews")),
                null,
                "Dresses",
                null,
                []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Total);
        var highPrice = Assert.Single(result.Value.RuleCounts, x => x.RuleId == "high_price_relative_to_niche");
        Assert.True(highPrice.Available);
        Assert.Equal(1, highPrice.Count);
        var selectedRule = Assert.Single(result.Value.RuleCounts, x => x.RuleId == "bad_recent_reviews");
        Assert.True(selectedRule.AlreadyUsed);
        Assert.Equal(2, selectedRule.Count);
    }

    private static RuleExpressionDto Rule(string ruleId) =>
        new(RuleExpressionKinds.Rule, ruleId, null, null);

    private static RuleExpressionDto Group(string groupOperator, params RuleExpressionDto[] children) =>
        new(RuleExpressionKinds.Group, null, groupOperator, children);

    private static void AddCurrentProduct(
        ApplicationDbContext context,
        string wbProductId,
        string sourceSubcategory,
        decimal price,
        decimal reviewRating,
        int feedbackCount,
        int totalQuantity,
        int? imageCount = 3,
        JsonDocument? imageUrls = null)
    {
        using var row = new ParserProductRow(
            idParserRun: Guid.NewGuid(),
            idParserFile: Guid.NewGuid(),
            sourceLineNumber: 1,
            rowHash: $"hash-{wbProductId}",
            schemaVersion: 1,
            marketplace: "wildberries",
            parserRunId: "parser-run",
            parsedAtUtc: Utc(2026, 7, 4, 10),
            sourceCategory: "Женщинам",
            sourceSubcategory: sourceSubcategory,
            sourceQuery: sourceSubcategory,
            sourceRegionDest: "12354108",
            wbProductId: wbProductId,
            skuProduct: wbProductId,
            name: $"Товар {wbProductId}",
            entity: null,
            brandIdOnMp: null,
            brandName: "Brand",
            sellerIdOnMp: null,
            sellerName: "Seller",
            priceRegular: price,
            priceDiscounted: price,
            priceWbWallet: price,
            discountPercent: 0,
            totalQuantity: totalQuantity,
            ratingRounded: (int)Math.Round(reviewRating),
            reviewRating: reviewRating,
            feedbackCount: feedbackCount,
            feedbackCountSource: "wb",
            imageUrls: imageUrls,
            imageCount: imageCount,
            wbRootId: $"root-{wbProductId}",
            subjectParentId: null,
            subjectId: null,
            rawObservedFields: null);

        context.ParserCurrentProductRows.Add(new ParserCurrentProductRow(
            row,
            new ProductGroupHashes("identity", "price", "stock", "rating", "reviews", "media", "seller"),
            Utc(2026, 7, 4, 10)));
    }

    private static void AddReviewSummary(
        ApplicationDbContext context,
        string wbProductId,
        int recentNegativeCount,
        decimal averageRating,
        int reviewsCount = 5,
        int? marketplaceFeedbackCount = null,
        string coverageStatus = "full")
    {
        context.ParserCurrentProductReviewsSummaries.Add(new ParserCurrentProductReviewsSummary(
            wbProductId,
            $"root-{wbProductId}",
            "Женщинам",
            "Платья и сарафаны",
            reviewsCount: reviewsCount,
            averageRating: averageRating,
            recentNegativeCount: recentNegativeCount,
            lastReviewDateUtc: Utc(2026, 7, 4, 8),
            marketplaceFeedbackCount: marketplaceFeedbackCount,
            fetchedReviewsCount: reviewsCount,
            oldestReviewDateUtc: Utc(2026, 7, 3, 8),
            coverageStatus: coverageStatus,
            coverageSource: "product_full",
            lastCoverageError: null,
            reviewsHash: $"reviews-{wbProductId}",
            reviewsJson: "{}",
            observedAtUtc: Utc(2026, 7, 4, 8),
            batchId: $"batch-{wbProductId}"));
    }

    private static void AddReviewRow(
        ApplicationDbContext context,
        string wbProductId,
        int rating,
        DateTime? createdAtOnMp)
    {
        var lineNumber = context.ParserReviewRows.Count() + 1;
        context.ParserReviewRows.Add(new ParserReviewRow(
            idParserRun: Guid.NewGuid(),
            idParserFile: Guid.NewGuid(),
            idReviewRootFetch: null,
            sourceLineNumber: lineNumber,
            rowHash: $"review-hash-{wbProductId}-{lineNumber}",
            schemaVersion: 1,
            parserRunId: "parser-run",
            parsedAtUtc: Utc(2026, 7, 4, 13).AddMinutes(lineNumber),
            marketplace: "wildberries",
            inputProductsParserRunId: null,
            inputProductsJsonl: null,
            sourceWbRootId: $"root-{wbProductId}",
            wbProductId: wbProductId,
            reviewAttributionMode: "product",
            reviewIdOnMp: $"review-{wbProductId}-{lineNumber}",
            rating: rating,
            text: null,
            pros: null,
            cons: null,
            createdAtOnMp: createdAtOnMp,
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

    private static void AddDetails(
        ApplicationDbContext context,
        string wbProductId,
        string description,
        int characteristicsCount)
    {
        var characteristics = Enumerable.Range(1, characteristicsCount)
            .Select(x => new Dictionary<string, string> { ["name"] = $"Характеристика {x}" });
        var detailsJson = JsonSerializer.Serialize(new
        {
            description,
            characteristics
        });

        context.ParserCurrentProductDetails.Add(new ParserCurrentProductDetail(
            wbProductId,
            $"root-{wbProductId}",
            "Женщинам",
            "Платья и сарафаны",
            $"details-{wbProductId}",
            detailsJson,
            Utc(2026, 7, 4, 8),
            $"batch-{wbProductId}"));
    }

    private static void AddStringEncodedDetails(
        ApplicationDbContext context,
        string wbProductId,
        string description,
        int characteristicsCount)
    {
        var characteristics = Enumerable.Range(1, characteristicsCount)
            .Select(x => new Dictionary<string, string> { ["name"] = $"Characteristic {x}" })
            .ToList();
        var groupedOptions = new[]
        {
            new
            {
                group_name = "Main",
                options = characteristics
            }
        };
        var detailsJson = JsonSerializer.Serialize(new
        {
            description,
            characteristics = JsonSerializer.Serialize(characteristics),
            groupedOptions = JsonSerializer.Serialize(groupedOptions)
        });

        context.ParserCurrentProductDetails.Add(new ParserCurrentProductDetail(
            wbProductId,
            $"root-{wbProductId}",
            "Women",
            "Dresses",
            $"details-{wbProductId}",
            detailsJson,
            Utc(2026, 7, 4, 8),
            $"batch-{wbProductId}"));
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Utc);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"rule-constructor-{Guid.NewGuid()}")
            .Options;
        return new RuleConstructorTestDbContext(options);
    }

    private sealed class RuleConstructorTestDbContext : ApplicationDbContext
    {
        public RuleConstructorTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var keptTypes = new HashSet<Type>
            {
                typeof(ParserCurrentProductRow),
                typeof(ParserCurrentProductReviewsSummary),
                typeof(ParserReviewRow),
                typeof(ParserCurrentProductDetail),
                typeof(ParserCurrentProductLogistics),
                typeof(ParserCurrentProductRank)
            };

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (!keptTypes.Contains(entityType.ClrType))
                    modelBuilder.Ignore(entityType.ClrType);
            }

            modelBuilder.Entity<ParserCurrentProductRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserCurrentProductReviewsSummary>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserReviewRow>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Ignore(x => x.RawObservedFields);
            });
            modelBuilder.Entity<ParserCurrentProductDetail>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserCurrentProductLogistics>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ParserCurrentProductRank>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
            });
        }
    }
}
