using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Recommendations;

public class Recommendation : IDisposable
{
    private Recommendation() { }

    public Recommendation(
        Guid idModel,
        int type,
        int typeObject,
        decimal score,
        JsonDocument? explanation,
        JsonDocument? snapshot,
        DateTime dateCreate,
        DateTime dateUpdate)
    {
        if (idModel == Guid.Empty)
            throw new ArgumentException("Model id is required", nameof(idModel));

        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be non-negative");

        DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
        DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));

        if (dateUpdate < dateCreate)
            throw new ArgumentException("DateUpdate cannot be earlier than DateCreate", nameof(dateUpdate));

        Id = Guid.NewGuid();
        IdModel = idModel;
        Type = type;
        TypeObject = typeObject;
        Score = score;
        Explanation = explanation;
        Snapshot = snapshot;
        DateCreate = dateCreate;
        DateUpdate = dateUpdate;
    }

    public Guid Id { get; private set; }
    public Guid IdModel { get; private set; }
    public int Type { get; private set; } // enum по документации, значения не определены
    public int TypeObject { get; private set; } // enum по документации, значения не определены
    public decimal Score { get; private set; }
    public JsonDocument? Explanation { get; private set; }
    public JsonDocument? Snapshot { get; private set; }
    public DateTime DateCreate { get; private set; }
    public DateTime DateUpdate { get; private set; }

    public void Dispose()
    {
        Explanation?.Dispose();
        Snapshot?.Dispose();
    }
}
