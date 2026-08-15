using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Search.Providers;

public sealed class InvoiceGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly IInvoiceRepository _invoiceRepository;
    public InvoiceGlobalSearchProvider(IInvoiceRepository invoiceRepository) => _invoiceRepository = invoiceRepository;
    public SearchEntityType EntityType => SearchEntityType.Invoice;
    public string RequiredPermission => Permissions.Invoices.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _invoiceRepository.SearchAsync(query, null, null, null, null, 1, limit, unpaidOnly: false, cancellationToken: cancellationToken);
        return items.Select(i => {
            var number = i.Number.Value;
            var isCreditNote = i.Type == InvoiceType.CreditNote;
            return new GlobalSearchProviderResult {
                Id = i.Id, Title = number,
                Subtitle = $"{(isCreditNote ? "Avoir — " : "")}{i.Client.Name} — {i.TotalAmount.Amount:N3} {i.TotalAmount.Currency}",
                Status = i.Status.ToDisplayString(), Date = i.IssueDate,
                DetailRoute = $"/invoices/{i.Id}", ListRoute = $"/invoices?search={Uri.EscapeDataString(query)}",
                Icon = isCreditNote ? "fa-file-circle-minus" : "fa-file-invoice",
                Score = SearchScoring.ScoreMatch(query, number, i.Client.Name, i.Reference)
            };
        }).ToList();
    }
}

public sealed class QuoteGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly IQuoteRepository _quoteRepository;
    public QuoteGlobalSearchProvider(IQuoteRepository quoteRepository) => _quoteRepository = quoteRepository;
    public SearchEntityType EntityType => SearchEntityType.Quote;
    public string RequiredPermission => Permissions.Quotes.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _quoteRepository.SearchAsync(query, null, null, null, null, 1, limit, cancellationToken);
        return items.Select(q => new GlobalSearchProviderResult
        {
            Id = q.Id,
            Title = q.Number.Value,
            Subtitle = $"{q.Client.Name} — {q.TotalAmount.Amount:N3} {q.TotalAmount.Currency}",
            Status = q.Status.ToDisplayString(),
            Date = q.IssueDate,
            DetailRoute = $"/quotes/{q.Id}",
            ListRoute = $"/quotes?search={Uri.EscapeDataString(query)}",
            Icon = "fa-file-lines",
            Score = SearchScoring.ScoreMatch(query, q.Number.Value, q.Client.Name, q.Reference)
        }).ToList();
    }
}

public sealed class DeliveryNoteGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    public DeliveryNoteGlobalSearchProvider(IDeliveryNoteRepository deliveryNoteRepository) => _deliveryNoteRepository = deliveryNoteRepository;
    public SearchEntityType EntityType => SearchEntityType.DeliveryNote;
    public string RequiredPermission => Permissions.DeliveryNotes.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _deliveryNoteRepository.GetPagedAsync(1, limit, null, null, null, null, query, cancellationToken);
        return items.Select(dn => new GlobalSearchProviderResult
        {
            Id = dn.Id,
            Title = dn.Number.Value,
            Subtitle = dn.Client?.Name ?? "Client inconnu",
            Status = dn.Status.ToDisplayString(),
            Date = dn.IssueDate,
            DetailRoute = $"/delivery-notes/{dn.Id}",
            ListRoute = $"/delivery-notes?search={Uri.EscapeDataString(query)}",
            Icon = "fa-truck",
            Score = SearchScoring.ScoreMatch(query, dn.Number.Value, dn.Client?.Name, dn.DeliveryAddress)
        }).ToList();
    }
}

public sealed class ClientGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly IClientRepository _clientRepository;
    public ClientGlobalSearchProvider(IClientRepository clientRepository) => _clientRepository = clientRepository;
    public SearchEntityType EntityType => SearchEntityType.Client;
    public string RequiredPermission => Permissions.Clients.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _clientRepository.SearchAsync(query, null, null, null, 1, limit, cancellationToken);
        return items.Select(c => new GlobalSearchProviderResult
        {
            Id = c.Id,
            Title = c.Name,
            Subtitle = string.Join(" — ", new[] { c.Email?.Value, c.NIF?.Value }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Status = c.IsActive ? "Actif" : "Inactif",
            Date = c.CreatedAt,
            DetailRoute = $"/clients/{c.Id}",
            ListRoute = $"/clients?search={Uri.EscapeDataString(query)}",
            Icon = "fa-users",
            Score = SearchScoring.ScoreMatch(query, c.Name, c.Email?.Value, c.NIF?.Value)
        }).ToList();
    }
}

public sealed class ProductGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly IProductRepository _productRepository;
    public ProductGlobalSearchProvider(IProductRepository productRepository) => _productRepository = productRepository;
    public SearchEntityType EntityType => SearchEntityType.Product;
    public string RequiredPermission => Permissions.Products.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _productRepository.SearchAsync(query, null, null, null, 1, limit, cancellationToken);
        return items.Select(p => new GlobalSearchProviderResult
        {
            Id = p.Id,
            Title = p.Name,
            Subtitle = p.Code,
            Status = p.IsActive ? "Actif" : "Inactif",
            Date = p.CreatedAt,
            DetailRoute = $"/products/{p.Id}",
            ListRoute = $"/products?search={Uri.EscapeDataString(query)}",
            Icon = "fa-cube",
            Score = SearchScoring.ScoreMatch(query, p.Name, p.Code)
        }).ToList();
    }
}

public sealed class SupplierGlobalSearchProvider : IGlobalSearchProvider
{
    private readonly ISupplierRepository _supplierRepository;
    public SupplierGlobalSearchProvider(ISupplierRepository supplierRepository) => _supplierRepository = supplierRepository;
    public SearchEntityType EntityType => SearchEntityType.Supplier;
    public string RequiredPermission => Permissions.Suppliers.Read;
    public async Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var (items, _) = await _supplierRepository.SearchAsync(query, null, null, 1, limit, cancellationToken);
        return items.Select(s => new GlobalSearchProviderResult
        {
            Id = s.Id,
            Title = s.Name,
            Subtitle = string.Join(" — ", new[] { s.Email?.Value, s.NIF?.Value }.Where(v => !string.IsNullOrWhiteSpace(v))),
            Status = s.IsActive ? "Actif" : "Inactif",
            Date = s.CreatedAt,
            DetailRoute = $"/suppliers/{s.Id}",
            ListRoute = $"/suppliers?search={Uri.EscapeDataString(query)}",
            Icon = "fa-building",
            Score = SearchScoring.ScoreMatch(query, s.Name, s.Email?.Value, s.NIF?.Value)
        }).ToList();
    }
}
