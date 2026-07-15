namespace FactuTrust.Domain.Common;

/// <summary>
/// Base class for aggregate roots.
/// Aggregates are clusters of entities and value objects 
/// that are treated as a single unit for data changes.
/// </summary>
public abstract class AggregateRoot : Entity
{
    public int Version { get; private set; }

    protected AggregateRoot() : base()
    {
        Version = 1;
    }

    protected AggregateRoot(Guid id) : base(id)
    {
        Version = 1;
    }

    public void IncrementVersion()
    {
        Version++;
    }
}
