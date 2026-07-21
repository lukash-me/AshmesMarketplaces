using AshmesMarketplaces.Domain.Entities.Workspaces;
using Xunit;

namespace AshmesMarketplaces.Application.Tests.WorkspaceMarketProducts;

public sealed class WorkspaceMarketProductTagTests
{
    [Fact]
    public void CreatedTag_IsValid_ForDemoCards()
    {
        Assert.True(WorkspaceMarketProduct.IsValidTag("created"));
    }
}
