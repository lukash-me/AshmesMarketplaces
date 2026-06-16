using System.Text.Json;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public static class ParserDefectiveCardReasons
{
    public const string InvalidProductIdentity = "invalid_product_identity";
    public const string MissingImages = "missing_images";
    public const string MissingDetails = "missing_details";
    public const string MissingLogistics = "missing_logistics";
    public const string MissingReviewFetch = "missing_review_fetch";
}

public sealed record ParserCardCompletenessEvidence(
    bool HasSuccessfulProductDetails,
    bool HasLogisticsAttempt,
    bool HasReviewFetchAttempt);

public sealed record ParserDefectiveCardAssessment(
    bool IsDefective,
    IReadOnlyList<string> Reasons);

public static class ParserDefectiveCardDetector
{
    public static ParserDefectiveCardAssessment Evaluate(
        ParserProductRow product,
        ParserCardCompletenessEvidence evidence)
    {
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(product.WbProductId)
            || string.IsNullOrWhiteSpace(product.WbRootId)
            || string.IsNullOrWhiteSpace(product.Name))
        {
            reasons.Add(ParserDefectiveCardReasons.InvalidProductIdentity);
        }

        if (!HasImages(product))
            reasons.Add(ParserDefectiveCardReasons.MissingImages);

        if (!evidence.HasSuccessfulProductDetails)
            reasons.Add(ParserDefectiveCardReasons.MissingDetails);

        if (!evidence.HasLogisticsAttempt)
            reasons.Add(ParserDefectiveCardReasons.MissingLogistics);

        if (!evidence.HasReviewFetchAttempt)
            reasons.Add(ParserDefectiveCardReasons.MissingReviewFetch);

        return new ParserDefectiveCardAssessment(reasons.Count > 0, reasons);
    }

    private static bool HasImages(ParserProductRow product)
    {
        if (product.ImageCount is > 0)
            return true;

        return product.ImageUrls?.RootElement.ValueKind == JsonValueKind.Array
               && product.ImageUrls.RootElement.GetArrayLength() > 0;
    }
}
