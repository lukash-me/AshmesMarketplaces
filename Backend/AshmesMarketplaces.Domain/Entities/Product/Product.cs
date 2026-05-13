using System.Text.Json;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using CSharpFunctionalExtensions;

namespace AshmesMarketplaces.Domain.Entities.Product;

public class Product : BaseEntity<ProductId>, IDisposable
{
    private Product() : base(ProductId.EmptyId()) { }

    private Product(
        ProductId id,
        Guid? idSetPrice,
        Guid? idWorkspace,
        Guid? idBrand,
        Guid idMp,
        Guid? idUser,
        Guid? idCategory,
        string? idOnMp,
        string? skuProduct,
        string skuSeller,
        string name,
        string? description,
        JsonDocument? characteristics,
        string? barcode,
        int? commission,
        ProductStatus status,
        DateTime dateUpdated,
        DateTime? dateCreated) : base(id)
    {
        IdSetPrice = idSetPrice;
        IdWorkspace = idWorkspace;
        IdBrand = idBrand;
        IdMp = idMp;
        IdUser = idUser;
        IdCategory = idCategory;
        IdOnMp = idOnMp;
        SkuProduct = skuProduct;
        SkuSeller = skuSeller;
        Name = name;
        Description = description;
        Characteristics = characteristics;
        Barcode = barcode;
        Commission = commission;
        Status = status;
        DateUpdated = dateUpdated;
        DateCreated = dateCreated;
    }

    public Guid? IdSetPrice { get; private set; }
    public Guid? IdWorkspace { get; private set; }
    public Guid? IdBrand { get; private set; }
    public Guid IdMp { get; private set; }
    public Guid? IdUser { get; private set; }
    public Guid? IdCategory { get; private set; }
    public string? IdOnMp { get; private set; }
    public string? SkuProduct { get; private set; }
    public string SkuSeller { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    private readonly List<ProductImage> _images = new();
    public IReadOnlyCollection<ProductImage> Images => _images;
    private readonly List<ProductVideo> _videos = new();
    public IReadOnlyCollection<ProductVideo> Videos => _videos;
    public JsonDocument? Characteristics { get; private set; }
    public string? Barcode { get; private set; }
    public int? Commission { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime DateUpdated { get; private set; }
    public DateTime? DateCreated { get; private set; }
    
    public void AddImage(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Image url is required");
        
        if (_images.Any(v => v.Url == url))
            return;

        var nextOrder = _images.Count == 0
            ? 1
            : _images.Max(i => i.SortOrder) + 1;

        _images.Add(new ProductImage(Id, url, nextOrder, nextOrder == 1));
    }
    
    public void AddVideo(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Video url is required");
        
        if (_videos.Any(v => v.Url == url))
            return;
        
        if (_videos.Count >= Constants.PRODUCT_VIDEOS_MAX_COUNT)
            throw new InvalidOperationException("Maximum number of videos reached");

        var nextOrder = _videos.Count == 0
            ? 1
            : _videos.Max(i => i.SortOrder) + 1;

        _videos.Add(new ProductVideo(Id, url, nextOrder));
    }
    
    public static Result<Product, Error> Create(
        ProductId productId,
        Guid? idSetPrice,
        Guid? idWorkspace,
        Guid? idBrand,
        Guid idMp,
        Guid? idUser,
        Guid? idCategory,
        string? idOnMp,
        string? skuProduct,
        string skuSeller,
        string name,
        string? description,
        string? characteristicsJson,
        string? barcode,
        int? commission,
        ProductStatus status,
        DateTime dateUpdated,
        DateTime? dateCreated)
    {
        if (productId.Value == Guid.Empty)
            return Errors.General.ValueIsRequired("productId");

        if (string.IsNullOrWhiteSpace(name))
            return Errors.General.ValueIsRequired("name");

        if (name.Length > Constants.PRODUCT_NAME_MAX_LENGTH)
            return Errors.General.InvalidLength("name");
        
        if (description?.Length > Constants.PRODUCT_DESCRIPTION_MAX_LENGTH)
            return Errors.General.InvalidLength("description");

        if (string.IsNullOrWhiteSpace(skuSeller))
            return Errors.General.ValueIsInvalid("skuSeller");

        if (skuSeller.Length > Constants.EXTERNAL_ID_MAX_LENGTH)
            return Errors.General.InvalidLength("skuSeller");

        if (skuProduct?.Length > Constants.EXTERNAL_ID_MAX_LENGTH)
            return Errors.General.InvalidLength("skuProduct");

        if (idOnMp?.Length > Constants.EXTERNAL_ID_MAX_LENGTH)
            return Errors.General.InvalidLength("idOnMp");

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            if (barcode.Length > Constants.BARCODE_MAX_LENGTH)
                return Errors.General.InvalidLength("barcode");
            
            if (!barcode.All(char.IsDigit))
                return Errors.General.ValueIsInvalid("barcode");
        }

        JsonDocument? characteristics = null;
        if (!string.IsNullOrWhiteSpace(characteristicsJson))
        {
            try
            {
                characteristics = JsonDocument.Parse(characteristicsJson);
            }
            catch
            {
                return Errors.General.ValueIsInvalid("characteristicsJson");
            }
        }

        if (commission is < 0)
            return Errors.General.ValueIsInvalid("commission");

        if (idMp == Guid.Empty)
            return Errors.General.ValueIsRequired("idMp");

        if (dateCreated.HasValue && dateCreated > DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("dateCreated");

        if (dateUpdated > DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("dateUpdated");
        
        var product = new Product(
            productId,
            idSetPrice,
            idWorkspace,
            idBrand,
            idMp,
            idUser,
            idCategory,
            idOnMp,
            skuProduct,
            skuSeller,
            name,
            description,
            characteristics,
            barcode,
            commission,
            status,
            dateUpdated,
            dateCreated);
        return product;
    }

    public void Dispose()
    {
        Characteristics?.Dispose();
    }
}
