namespace AshmesMarketplaces.Application.Categories.Dtos;

public sealed class CreateCategoryRequest
{
    public Guid? IdParentCategory { get; init; }
    public string? IdOnMp { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public bool IsActive { get; init; }
    public DateTime DateUpdate { get; init; }
}
