using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class LetteringGroupMember : Entity
{
    public Guid LetteringGroupId { get; private set; }
    public LetteringGroup LetteringGroup { get; private set; } = null!;
    public Guid JournalEntryLineId { get; private set; }

    private LetteringGroupMember() { }

    internal static LetteringGroupMember CreateForGroup(LetteringGroup group, Guid journalEntryLineId)
    {
        return new LetteringGroupMember
        {
            LetteringGroup = group,
            LetteringGroupId = group.Id,
            JournalEntryLineId = journalEntryLineId
        };
    }
}
