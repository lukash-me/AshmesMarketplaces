namespace AshmesMarketplaces.Application.Expenses.Dtos;

public sealed record ExpenseSummaryResponse(
    int TotalCount,
    decimal TotalAmount,
    int PaidCount,
    decimal PaidAmount,
    int PendingPaymentCount,
    decimal PendingPaymentAmount,
    int PlannedCount,
    int CancelledCount,
    int WithoutPaymentDateCount,
    decimal? AverageAmount,
    DateTime? LatestPaymentDate,
    IReadOnlyCollection<ExpenseSummaryBucketResponse> ByCategory,
    IReadOnlyCollection<ExpenseSummaryBucketResponse> ByStatus,
    IReadOnlyCollection<ExpenseSummaryBucketResponse> ByMonth);

public sealed record ExpenseSummaryBucketResponse(
    string Key,
    string Label,
    int Count,
    decimal Amount);
