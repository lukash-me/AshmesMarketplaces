using AshmesMarketplaces.Domain.IDs;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Reviews;

public class Review
{
    private Review() { }

    public Review(
        ProductId idProduct,
        string idOnMp,
        int rating,
        string? text,
        bool isReplied,
        DateTime dateCreate,
        DateTime? dateReply)
    {
        if (idProduct.Value == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(idProduct));

        if (string.IsNullOrWhiteSpace(idOnMp))
            throw new ArgumentException("Marketplace external id is required", nameof(idOnMp));

        if (rating < 0)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be non-negative");

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateReply, nameof(dateReply));

        if (dateReply.HasValue && dateReply < dateCreate)
            throw new ArgumentException("DateReply cannot be earlier than DateCreate", nameof(dateReply));

        Id = Guid.NewGuid();
        IdProduct = idProduct;
        IdOnMp = idOnMp;
        Rating = rating;
        Text = text;
        IsReplied = isReplied;
        DateCreate = dateCreate;
        DateReply = dateReply;
    }

    public Guid Id { get; private set; }
    public ProductId IdProduct { get; private set; } = ProductId.EmptyId();
    public string IdOnMp { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public string? Text { get; private set; }
    public bool IsReplied { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime? DateReply { get; private set; }
}
