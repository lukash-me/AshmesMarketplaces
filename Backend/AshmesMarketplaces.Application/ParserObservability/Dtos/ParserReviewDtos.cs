namespace AshmesMarketplaces.Application.ParserObservability.Dtos;

public sealed record ParserReviewListItemDto(
    Guid Id,
    string ParserRunId,
    DateTime ParsedAtUtc,
    string SourceWbRootId,
    string WbProductId,
    string ReviewIdOnMp,
    string ReviewAttributionMode,
    int? Rating,
    string? TextPreview,
    string? ProsPreview,
    string? ConsPreview,
    DateTime? CreatedAtOnMp,
    bool HasObservedReply,
    bool IsPartialSnapshot,
    bool IsCappedRootPayload,
    bool IsFullHistoryUnknown);

public sealed record ParserReviewRootFetchSummaryDto(
    string Status,
    DateTime TimestampUtc,
    int? PayloadFeedbackCount,
    int PayloadFeedbackRowsSeen,
    int SelectedReviewRowsSeen,
    int ReviewsWritten,
    int RepliesWritten);

public sealed record ParserReviewDetailDto(
    Guid Id,
    string ParserRunId,
    DateTime ParsedAtUtc,
    string SourceWbRootId,
    string WbProductId,
    string ReviewIdOnMp,
    string ReviewAttributionMode,
    int? Rating,
    string? TextPreview,
    DateTime? CreatedAtOnMp,
    bool HasObservedReply,
    bool IsPartialSnapshot,
    bool IsCappedRootPayload,
    bool IsFullHistoryUnknown,
    string? Text,
    string? Pros,
    string? Cons,
    string? ReviewerName,
    string? ReviewerCountry,
    bool? ReviewerHasPhoto,
    int? HelpfulPlus,
    int? HelpfulMinus,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string? SourceRegionDest,
    string? InputProductsParserRunId,
    string SourceFileKind,
    string SourceFileSha256,
    long SourceLineNumber,
    string RowHash,
    ParserReviewRootFetchSummaryDto? RootFetch);

public sealed record ParserReviewReplyDto(
    Guid Id,
    string ParserRunId,
    string SourceWbRootId,
    string WbProductId,
    string ReviewIdOnMp,
    string ReviewAttributionMode,
    string? ReplyIdOnMp,
    string? ReplyFallbackHash,
    bool HasStableReplyId,
    string? Text,
    DateTime? CreatedAtOnMp,
    DateTime? UpdatedAtOnMp,
    string? ReplyAuthor,
    string? ReplyState,
    bool IsPartialSnapshot,
    bool IsCappedRootPayload,
    bool IsFullHistoryUnknown,
    string SourceFileKind,
    long SourceLineNumber);

public sealed class ParserReviewListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? ParserRunId { get; init; }
    public string? WbProductId { get; init; }
    public string? SourceWbRootId { get; init; }
    public int? Rating { get; init; }
    public bool? HasObservedReply { get; init; }
    public bool? CappedRootPayload { get; init; }
    public string? ReviewAttributionMode { get; init; }
    public DateTime? CreatedAtOnMpFrom { get; init; }
    public DateTime? CreatedAtOnMpTo { get; init; }
}

public sealed class ParserReviewReplyListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
}
