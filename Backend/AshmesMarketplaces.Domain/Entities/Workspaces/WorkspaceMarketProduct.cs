using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Workspaces;

public sealed class WorkspaceMarketProduct
{
    public const string ParserSourceType = "parser";
    public const string DemoSourceType = "demo";
    public const string CompetitorTag = "competitor";
    public const string IdeaTag = "idea";
    public const string CreatedTag = "created";

    private WorkspaceMarketProduct() { }

    public WorkspaceMarketProduct(
        Guid idWorkspace,
        Guid idCreatedByUser,
        Guid? parserProductRowId,
        string? wbProductId,
        string? wbRootId,
        string? sourceCategory,
        string? sourceSubcategory,
        string? sourceRegionDest,
        string? sourceQuery,
        string sourceType,
        string tagKey,
        string? note,
        string name,
        string? brandName,
        string? sellerName,
        string? thumbnailUrl,
        decimal? priceRegular,
        decimal? priceDiscounted,
        decimal? priceWbWallet,
        decimal? reviewRating,
        int? feedbackCount,
        int? positionAbsolute,
        int? totalQuantity,
        decimal? costPrice,
        string? description,
        string? characteristicsJson,
        string? supplierName,
        string? supplierUrl,
        string? demoPayloadJson,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idWorkspace == Guid.Empty)
            throw new ArgumentException("Workspace id is required.", nameof(idWorkspace));

        if (idCreatedByUser == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(idCreatedByUser));

        if (parserProductRowId == Guid.Empty)
            throw new ArgumentException("Parser product row id is required.", nameof(parserProductRowId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        EnsureValidSourceType(sourceType);
        if (string.Equals(sourceType, ParserSourceType, StringComparison.Ordinal))
        {
            if (!parserProductRowId.HasValue)
                throw new ArgumentException("Parser product row id is required for parser source.", nameof(parserProductRowId));

            if (string.IsNullOrWhiteSpace(wbProductId))
                throw new ArgumentException("WB product id is required for parser source.", nameof(wbProductId));
        }

        EnsureValidTag(tagKey);
        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("Date update cannot be earlier than date create.", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdWorkspace = idWorkspace;
        IdCreatedByUser = idCreatedByUser;
        ParserProductRowId = parserProductRowId;
        WbProductId = Normalize(wbProductId);
        WbRootId = Normalize(wbRootId);
        SourceCategory = Normalize(sourceCategory);
        SourceSubcategory = Normalize(sourceSubcategory);
        SourceRegionDest = Normalize(sourceRegionDest);
        SourceSubcategoryKey = BuildContextKey(SourceSubcategory);
        SourceRegionDestKey = BuildContextKey(SourceRegionDest);
        SourceQuery = Normalize(sourceQuery);
        SourceType = sourceType.Trim();
        TagKey = tagKey.Trim();
        Note = Normalize(note);
        Name = name.Trim();
        BrandName = Normalize(brandName);
        SellerName = Normalize(sellerName);
        ThumbnailUrl = Normalize(thumbnailUrl);
        PriceRegular = priceRegular;
        PriceDiscounted = priceDiscounted;
        PriceWbWallet = priceWbWallet;
        ReviewRating = reviewRating;
        FeedbackCount = feedbackCount;
        PositionAbsolute = positionAbsolute;
        TotalQuantity = totalQuantity;
        CostPrice = costPrice;
        Description = Normalize(description);
        CharacteristicsJson = Normalize(characteristicsJson);
        SupplierName = Normalize(supplierName);
        SupplierUrl = Normalize(supplierUrl);
        DemoPayloadJson = Normalize(demoPayloadJson);
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdWorkspace { get; private set; }
    public Guid IdCreatedByUser { get; private set; }
    public Guid? ParserProductRowId { get; private set; }
    public string? WbProductId { get; private set; }
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceRegionDest { get; private set; }
    public string SourceSubcategoryKey { get; private set; } = string.Empty;
    public string SourceRegionDestKey { get; private set; } = string.Empty;
    public string? SourceQuery { get; private set; }
    public string SourceType { get; private set; } = ParserSourceType;
    public string TagKey { get; private set; } = CompetitorTag;
    public string? Note { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? BrandName { get; private set; }
    public string? SellerName { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public decimal? PriceRegular { get; private set; }
    public decimal? PriceDiscounted { get; private set; }
    public decimal? PriceWbWallet { get; private set; }
    public decimal? ReviewRating { get; private set; }
    public int? FeedbackCount { get; private set; }
    public int? PositionAbsolute { get; private set; }
    public int? TotalQuantity { get; private set; }
    public decimal? CostPrice { get; private set; }
    public string? Description { get; private set; }
    public string? CharacteristicsJson { get; private set; }
    public string? SupplierName { get; private set; }
    public string? SupplierUrl { get; private set; }
    public string? DemoPayloadJson { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }

    public void UpdateTracking(string tagKey, string? note, DateTime dateUpdate)
    {
        EnsureValidTag(tagKey);
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        TagKey = tagKey.Trim();
        Note = Normalize(note);
        DateUpdate = dateUpdate;
    }

    public void RefreshSnapshot(
        Guid parserProductRowId,
        string name,
        string? brandName,
        string? sellerName,
        string? thumbnailUrl,
        decimal? priceRegular,
        decimal? priceDiscounted,
        decimal? priceWbWallet,
        decimal? reviewRating,
        int? feedbackCount,
        int? positionAbsolute,
        int? totalQuantity,
        DateTime dateUpdate)
    {
        if (parserProductRowId == Guid.Empty)
            throw new ArgumentException("Parser product row id is required.", nameof(parserProductRowId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        ParserProductRowId = parserProductRowId;
        Name = name.Trim();
        BrandName = Normalize(brandName);
        SellerName = Normalize(sellerName);
        ThumbnailUrl = Normalize(thumbnailUrl);
        PriceRegular = priceRegular;
        PriceDiscounted = priceDiscounted;
        PriceWbWallet = priceWbWallet;
        ReviewRating = reviewRating;
        FeedbackCount = feedbackCount;
        PositionAbsolute = positionAbsolute;
        TotalQuantity = totalQuantity;
        DateUpdate = dateUpdate;
    }

    public void SetThumbnail(string? thumbnailUrl, DateTime dateUpdate)
    {
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        ThumbnailUrl = Normalize(thumbnailUrl);
        DateUpdate = dateUpdate;
    }

    public static bool IsValidTag(string? tagKey) =>
        string.Equals(tagKey, CompetitorTag, StringComparison.Ordinal)
        || string.Equals(tagKey, IdeaTag, StringComparison.Ordinal)
        || string.Equals(tagKey, CreatedTag, StringComparison.Ordinal);

    public static bool IsValidSourceType(string? sourceType) =>
        string.Equals(sourceType, ParserSourceType, StringComparison.Ordinal)
        || string.Equals(sourceType, DemoSourceType, StringComparison.Ordinal);

    private static void EnsureValidTag(string? tagKey)
    {
        if (!IsValidTag(tagKey))
            throw new ArgumentException("Tag key must be one of: competitor, idea, created.", nameof(tagKey));
    }

    private static void EnsureValidSourceType(string? sourceType)
    {
        if (!IsValidSourceType(sourceType))
            throw new ArgumentException("Source type must be one of: parser, demo.", nameof(sourceType));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildContextKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
