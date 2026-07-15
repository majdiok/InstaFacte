using FactuTrust.Domain.Common;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Guest checkout order received through the public 3D street.
/// Acts as the audit of truth in the Master DB; individual quotes/orders
/// are then dispatched to each tenant database by <c>StorefrontOrderDispatcher</c>.
/// </summary>
public sealed class StorefrontOrder : AggregateRoot
{
    public const int GuestNameMaxLength = 150;
    public const int GuestNotesMaxLength = 1000;

    private readonly List<StorefrontOrderItem> _items = new();
    public IReadOnlyCollection<StorefrontOrderItem> Items => _items.AsReadOnly();

    public string GuestFullName { get; private set; } = null!;
    public string GuestEmail { get; private set; } = null!;
    public string GuestPhone { get; private set; } = null!;
    public Address GuestDeliveryAddress { get; private set; } = null!;
    public string? GuestNotes { get; private set; }

    public StorefrontOrderStatus Status { get; private set; }
    public string? IpAddressHash { get; private set; }
    public string? UserAgentHash { get; private set; }

    public DateTime SubmittedAt { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    /// <summary>
    /// Per-tenant dispatch state. Serialized as JSON in the DB for simplicity at V1.
    /// Keyed by TenantId.
    /// </summary>
    public string TenantDispatchResultsJson { get; private set; } = "{}";

    private StorefrontOrder() { }

    public static Result<StorefrontOrder> Create(
        string guestFullName,
        string guestEmail,
        string guestPhone,
        Address guestDeliveryAddress,
        string? guestNotes,
        string? ipAddressHash,
        string? userAgentHash)
    {
        if (string.IsNullOrWhiteSpace(guestFullName) || guestFullName.Length > GuestNameMaxLength)
            return Result.Failure<StorefrontOrder>(Error.Validation("GuestFullName", $"Nom complet obligatoire, limité à {GuestNameMaxLength}"));
        if (string.IsNullOrWhiteSpace(guestEmail))
            return Result.Failure<StorefrontOrder>(Error.Validation("GuestEmail", "L'email est obligatoire"));
        if (string.IsNullOrWhiteSpace(guestPhone))
            return Result.Failure<StorefrontOrder>(Error.Validation("GuestPhone", "Le téléphone est obligatoire"));
        if (guestDeliveryAddress is null)
            return Result.Failure<StorefrontOrder>(Error.Validation("GuestDeliveryAddress", "L'adresse de livraison est obligatoire"));
        if (!string.IsNullOrWhiteSpace(guestNotes) && guestNotes.Length > GuestNotesMaxLength)
            return Result.Failure<StorefrontOrder>(Error.Validation("GuestNotes", $"Notes limitées à {GuestNotesMaxLength} caractères"));

        var order = new StorefrontOrder
        {
            GuestFullName = guestFullName.Trim(),
            GuestEmail = guestEmail.Trim().ToLowerInvariant(),
            GuestPhone = guestPhone.Trim(),
            GuestDeliveryAddress = guestDeliveryAddress,
            GuestNotes = string.IsNullOrWhiteSpace(guestNotes) ? null : guestNotes.Trim(),
            IpAddressHash = ipAddressHash,
            UserAgentHash = userAgentHash,
            Status = StorefrontOrderStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };
        return Result.Success(order);
    }

    public Result AddItem(
        Guid storefrontProductId,
        Guid tenantId,
        Guid sourceProductId,
        string productName,
        decimal quantity,
        decimal unitPriceAmount,
        string currency)
    {
        if (storefrontProductId == Guid.Empty)
            return Result.Failure(Error.Validation("ProductId", "Produit invalide"));
        if (tenantId == Guid.Empty)
            return Result.Failure(Error.Validation("TenantId", "Société invalide"));
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "Quantité strictement positive requise"));
        if (unitPriceAmount < 0)
            return Result.Failure(Error.Validation("UnitPrice", "Prix négatif interdit"));

        var itemResult = StorefrontOrderItem.Create(
            storefrontOrderId: Id,
            storefrontProductId: storefrontProductId,
            tenantId: tenantId,
            sourceProductId: sourceProductId,
            productName: productName,
            quantity: quantity,
            unitPriceAmount: unitPriceAmount,
            currency: currency);

        if (itemResult.IsFailure)
            return Result.Failure(itemResult.Error);

        _items.Add(itemResult.Value);
        return Result.Success();
    }

    public void MarkDispatched(string tenantDispatchResultsJson)
    {
        Status = StorefrontOrderStatus.DispatchedToTenants;
        DispatchedAt = DateTime.UtcNow;
        TenantDispatchResultsJson = tenantDispatchResultsJson ?? "{}";
    }

    public void MarkPartiallyConfirmed(string tenantDispatchResultsJson)
    {
        Status = StorefrontOrderStatus.PartiallyConfirmed;
        TenantDispatchResultsJson = tenantDispatchResultsJson ?? "{}";
    }

    public void MarkConfirmed(string tenantDispatchResultsJson)
    {
        Status = StorefrontOrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        TenantDispatchResultsJson = tenantDispatchResultsJson ?? "{}";
    }

    public void MarkFailed(string tenantDispatchResultsJson)
    {
        Status = StorefrontOrderStatus.Failed;
        TenantDispatchResultsJson = tenantDispatchResultsJson ?? "{}";
    }

    public void MarkCancelled()
    {
        Status = StorefrontOrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    public void RaiseSubmittedEvent()
    {
        var tenants = _items.Select(i => i.TenantId).Distinct().ToList();
        AddDomainEvent(new PublicOrderSubmittedEvent(Id, tenants));
    }

    public IReadOnlyCollection<Guid> GetInvolvedTenantIds()
        => _items.Select(i => i.TenantId).Distinct().ToList();
}

/// <summary>
/// Line item of a public storefront order. Immutable snapshot of the product pricing at submission time.
/// </summary>
public sealed class StorefrontOrderItem : Entity
{
    public Guid StorefrontOrderId { get; private set; }
    public Guid StorefrontProductId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Originating product id in the tenant database.</summary>
    public Guid SourceProductId { get; private set; }

    public string ProductName { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPriceAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public decimal LineTotal => Math.Round(UnitPriceAmount * Quantity, 3);

    private StorefrontOrderItem() { }

    internal static Result<StorefrontOrderItem> Create(
        Guid storefrontOrderId,
        Guid storefrontProductId,
        Guid tenantId,
        Guid sourceProductId,
        string productName,
        decimal quantity,
        decimal unitPriceAmount,
        string currency)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Result.Failure<StorefrontOrderItem>(Error.Validation("ProductName", "Nom produit obligatoire"));

        return Result.Success(new StorefrontOrderItem
        {
            StorefrontOrderId = storefrontOrderId,
            StorefrontProductId = storefrontProductId,
            TenantId = tenantId,
            SourceProductId = sourceProductId,
            ProductName = productName.Trim(),
            Quantity = Math.Round(quantity, 3),
            UnitPriceAmount = Math.Round(unitPriceAmount, 3),
            Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant()
        });
    }
}
