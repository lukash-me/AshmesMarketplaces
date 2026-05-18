using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Products.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Products.Services;

public sealed class ProductService : IProductService
{
    private readonly ApplicationDbContext _dbContext;

    public ProductService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ProductListItemResponse>>> GetListAsync(
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var products = _dbContext.Products.AsNoTracking();

        if (query.IdMp.HasValue)
            products = products.Where(x => x.IdMp == query.IdMp.Value);

        if (query.IdBrand.HasValue)
            products = products.Where(x => x.IdBrand == query.IdBrand.Value);

        if (query.IdCategory.HasValue)
            products = products.Where(x => x.IdCategory == query.IdCategory.Value);

        if (query.Status.HasValue)
            products = products.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.SkuSeller, $"%{search}%")
                || (x.SkuProduct != null && EF.Functions.ILike(x.SkuProduct, $"%{search}%"))
                || (x.IdOnMp != null && EF.Functions.ILike(x.IdOnMp, $"%{search}%"))
                || (x.Barcode != null && EF.Functions.ILike(x.Barcode, $"%{search}%")));
        }

        products = query.Sort?.Trim() switch
        {
            "-name" => products.OrderByDescending(x => x.Name),
            "dateUpdated" => products.OrderBy(x => x.DateUpdated),
            "-dateUpdated" => products.OrderByDescending(x => x.DateUpdated),
            "dateCreated" => products.OrderBy(x => x.DateCreated),
            "-dateCreated" => products.OrderByDescending(x => x.DateCreated),
            "skuSeller" => products.OrderBy(x => x.SkuSeller),
            "-skuSeller" => products.OrderByDescending(x => x.SkuSeller),
            _ => products.OrderBy(x => x.Name)
        };

        var totalCount = await products.CountAsync(cancellationToken);
        var productItems = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = productItems.Select(MapToListItem).ToList();

        return ServiceResult<PagedResponse<ProductListItemResponse>>.Success(
            new PagedResponse<ProductListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ProductResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ProductResponse>.BadRequest("Product id is required.");

        var product = await GetProductByIdAsync(id, asNoTracking: true, cancellationToken);

        return product is null
            ? ServiceResult<ProductResponse>.NotFound("Product was not found.")
            : ServiceResult<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<ServiceResult<ProductResponse>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(
            request.IdMp,
            request.IdBrand,
            request.IdCategory,
            cancellationToken);

        if (referenceCheck is not null)
            return ServiceResult<ProductResponse>.Conflict(referenceCheck);

        var createResult = Product.Create(
            ProductId.NewId(),
            idSetPrice: null,
            idWorkspace: null,
            request.IdBrand,
            request.IdMp,
            idUser: null,
            request.IdCategory,
            request.IdOnMp,
            request.SkuProduct,
            request.SkuSeller,
            request.Name,
            request.Description,
            GetCharacteristicsJson(request.Characteristics),
            request.Barcode,
            request.Commission,
            request.Status,
            request.DateUpdated,
            request.DateCreated);

        if (createResult.IsFailure)
            return ServiceResult<ProductResponse>.BadRequest(createResult.Error.Message);

        var product = createResult.Value;

        try
        {
            foreach (var imageUrl in request.ImageUrls)
                product.AddImage(imageUrl);

            foreach (var videoUrl in request.VideoUrls)
                product.AddVideo(videoUrl);

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<ProductResponse>.Success(MapToResponse(product));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ProductResponse>.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<ProductResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ProductResponse>.Conflict("Product cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<ProductResponse>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ProductResponse>.BadRequest("Product id is required.");

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(x => x.Id == ProductId.Create(id), cancellationToken);

        if (product is null)
            return ServiceResult<ProductResponse>.NotFound("Product was not found.");

        var referenceCheck = await ValidateReferencesAsync(
            request.IdMp,
            request.IdBrand,
            request.IdCategory,
            cancellationToken);

        if (referenceCheck is not null)
            return ServiceResult<ProductResponse>.Conflict(referenceCheck);

        var validationResult = Product.Create(
            ProductId.Create(id),
            idSetPrice: null,
            idWorkspace: null,
            request.IdBrand,
            request.IdMp,
            idUser: null,
            request.IdCategory,
            request.IdOnMp,
            request.SkuProduct,
            request.SkuSeller,
            request.Name,
            request.Description,
            GetCharacteristicsJson(request.Characteristics),
            request.Barcode,
            request.Commission,
            request.Status,
            request.DateUpdated,
            request.DateCreated);

        if (validationResult.IsFailure)
            return ServiceResult<ProductResponse>.BadRequest(validationResult.Error.Message);

        var validatedProduct = validationResult.Value;
        var entry = _dbContext.Entry(product);
        entry.Property(x => x.IdMp).CurrentValue = request.IdMp;
        entry.Property(x => x.IdBrand).CurrentValue = request.IdBrand;
        entry.Property(x => x.IdCategory).CurrentValue = request.IdCategory;
        entry.Property(x => x.IdOnMp).CurrentValue = request.IdOnMp;
        entry.Property(x => x.SkuProduct).CurrentValue = request.SkuProduct;
        entry.Property(x => x.SkuSeller).CurrentValue = request.SkuSeller;
        entry.Property(x => x.Name).CurrentValue = request.Name;
        entry.Property(x => x.Description).CurrentValue = request.Description;
        entry.Property(x => x.Characteristics).CurrentValue = validatedProduct.Characteristics;
        entry.Property(x => x.Barcode).CurrentValue = request.Barcode;
        entry.Property(x => x.Commission).CurrentValue = request.Commission;
        entry.Property(x => x.Status).CurrentValue = request.Status;
        entry.Property(x => x.DateCreated).CurrentValue = request.DateCreated;
        entry.Property(x => x.DateUpdated).CurrentValue = request.DateUpdated;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ProductResponse>.Conflict("Product cannot be updated because it conflicts with database constraints.");
        }

        var updatedProduct = await GetProductByIdAsync(id, asNoTracking: true, cancellationToken);

        return updatedProduct is null
            ? ServiceResult<ProductResponse>.NotFound("Product was not found.")
            : ServiceResult<ProductResponse>.Success(MapToResponse(updatedProduct));
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Product id is required.");

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(x => x.Id == ProductId.Create(id), cancellationToken);

        if (product is null)
            return ServiceResult.NotFound("Product was not found.");

        _dbContext.Products.Remove(product);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Product cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<Product?> GetProductByIdAsync(
        Guid id,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var productId = ProductId.Create(id);
        var products = _dbContext.Products
            .Include(x => x.Images)
            .Include(x => x.Videos)
            .AsQueryable();

        if (asNoTracking)
            products = products.AsNoTracking();

        return await products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    }

    private async Task<string?> ValidateReferencesAsync(
        Guid idMp,
        Guid? idBrand,
        Guid? idCategory,
        CancellationToken cancellationToken)
    {
        var marketplaceExists = await _dbContext.Marketplaces
            .AsNoTracking()
            .AnyAsync(x => x.Id == idMp, cancellationToken);

        if (!marketplaceExists)
            return "Marketplace was not found.";

        if (idBrand.HasValue)
        {
            var brandExists = await _dbContext.Brands
                .AsNoTracking()
                .AnyAsync(x => x.Id == idBrand.Value, cancellationToken);

            if (!brandExists)
                return "Brand was not found.";
        }

        if (idCategory.HasValue)
        {
            var categoryExists = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(x => x.Id == idCategory.Value, cancellationToken);

            if (!categoryExists)
                return "Category was not found.";
        }

        return null;
    }

    private static ProductListItemResponse MapToListItem(Product product)
    {
        return new ProductListItemResponse(
            product.Id.Value,
            product.IdMp,
            product.IdBrand,
            product.IdCategory,
            product.IdOnMp,
            product.SkuProduct,
            product.SkuSeller,
            product.Name,
            product.Barcode,
            product.Commission,
            product.Status,
            product.DateCreated,
            product.DateUpdated);
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse(
            product.Id.Value,
            product.IdMp,
            product.IdBrand,
            product.IdCategory,
            product.IdOnMp,
            product.SkuProduct,
            product.SkuSeller,
            product.Name,
            product.Description,
            CloneCharacteristics(product.Characteristics),
            product.Barcode,
            product.Commission,
            product.Status,
            product.DateCreated,
            product.DateUpdated,
            product.Images
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductImageResponse(x.Id, x.Url, x.SortOrder, x.IsMain))
                .ToList(),
            product.Videos
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductVideoResponse(x.Id, x.Url, x.SortOrder))
                .ToList());
    }

    private static JsonElement? CloneCharacteristics(JsonDocument? characteristics)
    {
        return characteristics is null
            ? null
            : characteristics.RootElement.Clone();
    }

    private static string? GetCharacteristicsJson(JsonElement? characteristics)
    {
        return characteristics.HasValue
            ? characteristics.Value.GetRawText()
            : null;
    }
}
