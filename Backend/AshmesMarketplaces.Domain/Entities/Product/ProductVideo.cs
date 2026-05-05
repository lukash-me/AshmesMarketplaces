namespace AshmesMarketplaces.Domain.Entities.Product;

public class ProductVideo
{
    public Guid Id { get; private set; }
    public string Url { get; private set; }
    
    private ProductVideo() { }
    public ProductVideo(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Video url is required");

        Id = Guid.NewGuid();
        Url = url;
    }
}