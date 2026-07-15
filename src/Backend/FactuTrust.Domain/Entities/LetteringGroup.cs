using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Lettrage comptable (411/401).
/// </summary>
public sealed class LetteringGroup : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string AccountNumber { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public DateTime LetteredAt { get; private set; }

    /// <summary>
    /// Lettrage partiel : le groupe couvre un règlement incomplet (débits ≠ crédits).
    /// Code préfixé « P » ; se complète en délettrant puis relettrant l'ensemble soldé.
    /// </summary>
    public bool IsPartial { get; private set; }

    private readonly List<LetteringGroupMember> _members = new();
    public IReadOnlyCollection<LetteringGroupMember> Members => _members.AsReadOnly();

    private LetteringGroup() { }

    public static Result<LetteringGroup> Create(
        string code,
        string accountNumber,
        Money amount,
        IReadOnlyList<Guid> journalEntryLineIds,
        bool isPartial = false)
    {
        if (journalEntryLineIds.Count < 2)
            return Result.Failure<LetteringGroup>(Error.Validation("Lines", "Au moins deux lignes pour le lettrage"));

        code = code?.Trim() ?? string.Empty;
        accountNumber = accountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(accountNumber))
            return Result.Failure<LetteringGroup>(Error.Validation("Code", "Code et compte obligatoires"));

        var g = new LetteringGroup
        {
            Code = code,
            AccountNumber = accountNumber,
            Amount = amount,
            LetteredAt = DateTime.UtcNow,
            IsPartial = isPartial
        };
        foreach (var id in journalEntryLineIds)
            g._members.Add(LetteringGroupMember.CreateForGroup(g, id));

        return Result.Success(g);
    }
}
