using System.Text.Json;
using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;
using CSharpFunctionalExtensions;

namespace AshmesMarketplaces.Domain.Entities.Product;

public class Product : BaseEntity<ProductId>
{
    private bool _isDeleted = false;
    private Product(ProductId id) : base(id) { }
    private Product(
        Guid? idSetPrice,
        Guid? idWorkspace,
        Guid? idBrand,
        Guid idMp,
        Guid? idUser,
        Guid? idCategory,
        int? skuProduct,
        int skuSeller,
        string name,
        string? description,
        string? characteristicsJson,
        string? barcode,
        DateTime dateUpdated,
        DateTime? dateCreated) : base(ProductId.NewId())
    {
        IdSetPrice = idSetPrice;
        IdWorkspace = idWorkspace;
        IdBrand = idBrand;
        IdMp = idMp;
        IdUser = idUser;
        IdCategory = idCategory;
        SkuProduct = skuProduct;
        SkuSeller = skuSeller;
        Name = name;
        Description = description;
        CharacteristicsJson = characteristicsJson;
        Barcode = barcode;
        DateUpdated = dateUpdated;
        DateCreated = dateCreated;
    }

    public Guid? IdSetPrice { get; private set; }
    public Guid? IdWorkspace { get; private set; }
    public Guid? IdBrand { get; private set; }
    public Guid IdMp { get; private set; }
    public Guid? IdUser { get; private set; }
    public Guid? IdCategory { get; private set; }
    public int? SkuProduct { get; private set; }
    public int SkuSeller { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    private readonly List<ProductImage> _images = new();
    public IReadOnlyCollection<ProductImage> Images => _images;
    private readonly List<ProductVideo> _videos = new();
    public IReadOnlyCollection<ProductVideo> Videos => _videos;
    public string? CharacteristicsJson { get; private set; }
    public string? Barcode { get; private set; }
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

        _images.Add(new ProductImage(url, nextOrder));
    }
    
    public void AddVideo(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Video url is required");
        
        if (_videos.Any(v => v.Url == url))
            return;
        
        if (_videos.Count >= Constants.PRODUCT_VIDEOS_MAX_COUNT)
            throw new InvalidOperationException("Maximum number of videos reached");

        _videos.Add(new ProductVideo(url));
    }
    
    public static Result<Product, Error> Create(
        ProductId productId,
        Guid? idSetPrice,
        Guid? idWorkspace,
        Guid? idBrand,
        Guid idMp,
        Guid? idUser,
        Guid? idCategory,
        int? skuProduct,
        int skuSeller,
        string name,
        string? description,
        string? characteristicsJson,
        string? barcode,
        DateTime dateUpdated,
        DateTime? dateCreated)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Errors.General.ValueIsRequired("name");

        if (name.Length > Constants.PRODUCT_NAME_MAX_LENGTH)
            return Errors.General.InvalidLength("name");
        
        if (description?.Length > Constants.PRODUCT_DESCRIPTION_MAX_LENGTH)
            return Errors.General.InvalidLength("description");
        
        if (skuSeller <= 0)
            return Errors.General.ValueIsInvalid("skuSeller");

        if (skuProduct is not null && skuProduct <= 0)
            return Errors.General.ValueIsInvalid("skuProduct");
        
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            if (barcode.Length > Constants.BARCODE_MAX_LENGTH)
                return Errors.General.InvalidLength("barcode");
            
            if (!barcode.All(char.IsDigit))
                return Errors.General.ValueIsInvalid("barcode");
        }
        
        if (!string.IsNullOrWhiteSpace(characteristicsJson))
        {
            try
            {
                JsonDocument.Parse(characteristicsJson);
            }
            catch
            {
                return Errors.General.ValueIsInvalid("characteristicsJson");
            }
        }
        
        if (idMp == Guid.Empty)
            return Errors.General.ValueIsRequired("idMp");
        
        if (dateCreated.HasValue && dateCreated > DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("dateCreated");

        if (dateUpdated > DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("dateUpdated");
        
        var product = new Product(
            idSetPrice,
            idWorkspace,
            idBrand,
            idMp,
            idUser,
            idCategory,
            skuProduct,
            skuSeller,
            name,
            description,
            characteristicsJson,
            barcode,
            dateUpdated,
            dateCreated);
        return product;
    }
}