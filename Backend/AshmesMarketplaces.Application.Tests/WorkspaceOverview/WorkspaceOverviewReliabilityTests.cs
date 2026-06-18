using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.WorkspaceOverview;

public sealed class WorkspaceOverviewReliabilityTests
{
    [Fact]
    public void IsDefectiveValidationRun_ReturnsTrue_WhenAllProductsFailedContractValidation()
    {
        var warnings = new[]
        {
            "Товар коврик: Request payload validation failed: body.product.positiveReviewCount: Extra inputs are not permitted",
            "Товар 986542381: Request payload validation failed: body.candidates.0.reviewSampleSize: Extra inputs are not permitted"
        };

        Assert.True(WorkspaceOverviewService.IsDefectiveValidationRunForTesting(
            productCount: 2,
            signalCount: 0,
            similarProductCount: 0,
            warnings));
    }

    [Fact]
    public void IsDefectiveValidationRun_ReturnsFalse_WhenRunHasAnyUsefulAnalysis()
    {
        var warnings = new[]
        {
            "Товар 986542381: Request payload validation failed: body.product.positiveReviewCount: Extra inputs are not permitted"
        };

        Assert.False(WorkspaceOverviewService.IsDefectiveValidationRunForTesting(
            productCount: 2,
            signalCount: 0,
            similarProductCount: 12,
            warnings));
    }
}
