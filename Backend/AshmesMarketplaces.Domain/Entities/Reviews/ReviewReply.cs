namespace AshmesMarketplaces.Domain.Entities.Reviews;

public class ReviewReply
{
    private ReviewReply() { }

    public ReviewReply(
        Guid idReview,
        string idOnMp,
        string text,
        int status,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idReview == Guid.Empty)
            throw new ArgumentException("Review id is required", nameof(idReview));

        if (string.IsNullOrWhiteSpace(idOnMp))
            throw new ArgumentException("Marketplace external id is required", nameof(idOnMp));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required", nameof(text));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdReview = idReview;
        IdOnMp = idOnMp;
        Text = text;
        Status = status;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdReview { get; private set; }
    public string IdOnMp { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public int Status { get; private set; } // enum по документации, значения не определены
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }
}
