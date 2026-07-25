using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

public sealed class Shareholder : Entity
{
    public Guid PermanentFileId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsLegalEntity { get; private set; }
    public string? CinOrNif { get; private set; }
    public decimal ShareCount { get; private set; }
    public decimal SharePercentage { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Shareholder() { }

    public static Result<Shareholder> Create(
        Guid permanentFileId,
        string name,
        bool isLegalEntity,
        decimal shareCount,
        decimal sharePercentage)
    {
        if (permanentFileId == Guid.Empty)
            return Result.Failure<Shareholder>(Error.Validation("PermanentFileId", "Dossier permanent requis"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Shareholder>(Error.Validation("Name", "Nom obligatoire"));

        return Result.Success(new Shareholder
        {
            PermanentFileId = permanentFileId,
            Name = name.Trim(),
            IsLegalEntity = isLegalEntity,
            ShareCount = Math.Max(0, shareCount),
            SharePercentage = Math.Clamp(sharePercentage, 0, 100)
        });
    }

    public void Update(string? cinOrNif, decimal shareCount, decimal sharePercentage)
    {
        CinOrNif = cinOrNif?.Trim();
        ShareCount = Math.Max(0, shareCount);
        SharePercentage = Math.Clamp(sharePercentage, 0, 100);
    }

    public Result Update(string name, bool isLegalEntity, string? cinOrNif, decimal shareCount, decimal sharePercentage)
    {
        if (!IsActive)
            return Result.Failure(Error.NotFound("Shareholder", Id));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Nom obligatoire"));

        Name = name.Trim();
        IsLegalEntity = isLegalEntity;
        Update(cinOrNif, shareCount, sharePercentage);
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
}
