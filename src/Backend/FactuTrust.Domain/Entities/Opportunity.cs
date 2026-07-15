using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class Opportunity : AggregateRoot
{
    public string Title { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public Guid AssignedUserId { get; private set; }
    public string AssignedUserName { get; private set; } = null!;
    public OpportunityStage Stage { get; private set; }
    public Money ExpectedAmount { get; private set; } = null!;
    public int Probability { get; private set; }
    public DateTime ExpectedCloseDate { get; private set; }
    public DateTime? ActualCloseDate { get; private set; }
    public string? LostReason { get; private set; }
    public Guid? LinkedQuoteId { get; private set; }
    public Guid? LinkedInvoiceId { get; private set; }
    public string? Notes { get; private set; }
    public string? Source { get; private set; }

    private Opportunity() { }

    public static Result<Opportunity> Create(
        string title,
        Guid clientId,
        Guid assignedUserId,
        string assignedUserName,
        Money expectedAmount,
        int probability,
        DateTime expectedCloseDate,
        string? source = null,
        string? notes = null)
    {
        title = title?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
            return Result.Failure<Opportunity>(Error.Validation("Title", "Le titre est obligatoire"));

        if (probability is < 0 or > 100)
            return Result.Failure<Opportunity>(Error.Validation("Probability", "La probabilité doit être entre 0 et 100"));

        return Result.Success(new Opportunity
        {
            Title = title,
            ClientId = clientId,
            AssignedUserId = assignedUserId,
            AssignedUserName = assignedUserName?.Trim() ?? string.Empty,
            Stage = OpportunityStage.Prospection,
            ExpectedAmount = expectedAmount,
            Probability = probability,
            ExpectedCloseDate = expectedCloseDate.Date,
            Source = source?.Trim(),
            Notes = notes?.Trim()
        });
    }

    public Result Update(string title, Money expectedAmount, int probability, DateTime expectedCloseDate, string? source, string? notes)
    {
        if (!Stage.IsOpen())
            return Result.Failure(Error.Validation("Stage", "Impossible de modifier une opportunité clôturée"));

        title = title?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
            return Result.Failure(Error.Validation("Title", "Le titre est obligatoire"));

        if (probability is < 0 or > 100)
            return Result.Failure(Error.Validation("Probability", "La probabilité doit être entre 0 et 100"));

        Title = title;
        ExpectedAmount = expectedAmount;
        Probability = probability;
        ExpectedCloseDate = expectedCloseDate.Date;
        Source = source?.Trim();
        Notes = notes?.Trim();
        return Result.Success();
    }

    public Result Advance()
    {
        if (!Stage.IsOpen())
            return Result.Failure(Error.Validation("Stage", "Impossible d'avancer une opportunité clôturée"));

        if (Stage == OpportunityStage.Negotiation)
            return Result.Failure(Error.Validation("Stage", "Utilisez Win ou Lose pour clôturer depuis Négociation"));

        Stage = (OpportunityStage)((int)Stage + 1);
        return Result.Success();
    }

    public Result Win()
    {
        if (!Stage.IsOpen())
            return Result.Failure(Error.Validation("Stage", "Cette opportunité est déjà clôturée"));

        Stage = OpportunityStage.Won;
        Probability = 100;
        ActualCloseDate = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Lose(string reason)
    {
        if (!Stage.IsOpen())
            return Result.Failure(Error.Validation("Stage", "Cette opportunité est déjà clôturée"));

        reason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason))
            return Result.Failure(Error.Validation("LostReason", "Le motif de perte est obligatoire"));

        Stage = OpportunityStage.Lost;
        Probability = 0;
        LostReason = reason;
        ActualCloseDate = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Stage != OpportunityStage.Lost)
            return Result.Failure(Error.Validation("Stage", "Seule une opportunité perdue peut être rouverte"));

        Stage = OpportunityStage.Prospection;
        Probability = 10;
        LostReason = null;
        ActualCloseDate = null;
        return Result.Success();
    }

    public void LinkQuote(Guid quoteId) => LinkedQuoteId = quoteId;
    public void LinkInvoice(Guid invoiceId) => LinkedInvoiceId = invoiceId;

    public decimal WeightedAmount => ExpectedAmount.Amount * Probability / 100m;
}
