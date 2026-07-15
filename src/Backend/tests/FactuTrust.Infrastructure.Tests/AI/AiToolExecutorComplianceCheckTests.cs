using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Invoices.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// compliance_check_invoice doit résoudre lui-même la facture : identifiant exact, numéro
/// (invoice_number ou invoice_id non-GUID ressemblant à un numéro), ou la plus récente si rien
/// n'est fourni. Le petit modèle ne connaît jamais de GUID interne — « vérifie la conformité de
/// ma dernière facture » doit marcher en un seul appel, et aucune erreur ne doit citer « GUID ».
/// </summary>
public sealed class AiToolExecutorComplianceCheckTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<ILogger<AiToolExecutor>> _loggerMock = new();
    private readonly AiToolExecutor _executor;

    private static readonly Guid LatestInvoiceId = Guid.NewGuid();

    public AiToolExecutorComplianceCheckTests()
    {
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserMock.Setup(x => x.Email).Returns("test@example.com");
        _currentUserMock.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        _executor = new AiToolExecutor(
            _mediatorMock.Object,
            _loggerMock.Object,
            TimeProvider.System,
            _currentUserMock.Object,
            Options.Create(new OllamaSettings()));
    }

    private void SetupSearch(params InvoiceListDto[] items)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetInvoicesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<InvoiceListDto>.Create(items, 1, 1, items.Length));
    }

    private void SetupDetail(Guid id, string number = "FAC-2026-000006")
    {
        var detail = new InvoiceDetailDto
        {
            Id = id,
            Number = number,
            Status = InvoiceStatus.Paid,
            StatusDisplay = "Payée",
            Client = new ClientSummaryDto
            {
                Id = Guid.NewGuid(),
                Name = "Client passager",
                Nif = "12345678",
                Email = "c@x.tn",
                Address = "Tunis"
            },
            Lines = new[]
            {
                new InvoiceLineDto { ProductName = "Baignoire simple", Quantity = 1, UnitPrice = 2100m, SubTotal = 2100m, Total = 2499m }
            },
            SubTotal = 2100m,
            TotalVat = 399m,
            TotalAmount = 2499m,
            Currency = "TND",
            SignatureHash = "abc"
        };
        _mediatorMock
            .Setup(m => m.Send(It.Is<GetInvoiceByIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InvoiceDetailDto>.Success(detail));
    }

    private static InvoiceListDto ListRow(Guid id, string number = "FAC-2026-000006") => new()
    {
        Id = id,
        Number = number,
        Status = "Payée",
        StatusCssClass = "paid",
        ClientName = "Client passager",
        TotalAmount = 2499m,
        Currency = "TND"
    };

    private Task<AiToolResult> ExecuteAsync(Dictionary<string, object?> args) =>
        _executor.ExecuteAsync("compliance_check_invoice", args, AiToolExecutionContext.Empty, CancellationToken.None);

    [Fact]
    public async Task Guid_Path_Unchanged_ResolvedBy_Id()
    {
        var id = Guid.NewGuid();
        SetupDetail(id);

        var result = await ExecuteAsync(new Dictionary<string, object?> { ["invoice_id"] = id.ToString() });

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal("id", doc.RootElement.GetProperty("resolvedBy").GetString());
        // Aucune recherche nécessaire quand l'identifiant est fourni.
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetInvoicesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_Args_Resolves_Latest_Invoice()
    {
        SetupSearch(ListRow(LatestInvoiceId));
        SetupDetail(LatestInvoiceId);

        var result = await ExecuteAsync(new Dictionary<string, object?>());

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal("latest", doc.RootElement.GetProperty("resolvedBy").GetString());
        // La plus récente = recherche sans terme, une seule ligne (SearchAsync trie par CreatedAt DESC).
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetInvoicesQuery>(q => q.SearchTerm == null && q.PageSize == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetInvoiceByIdQuery>(q => q.Id == LatestInvoiceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invoice_Number_Resolves_By_Number()
    {
        var id = Guid.NewGuid();
        SetupSearch(ListRow(id, "FAC-2026-000123"));
        SetupDetail(id, "FAC-2026-000123");

        var result = await ExecuteAsync(new Dictionary<string, object?> { ["invoice_number"] = "fac-2026-000123" });

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal("number", doc.RootElement.GetProperty("resolvedBy").GetString());
        // Le numéro est normalisé en majuscules avant recherche.
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetInvoicesQuery>(q => q.SearchTerm == "FAC-2026-000123" && q.PageSize == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Non_Guid_Invoice_Id_Containing_Number_Uses_Number_Path()
    {
        var id = Guid.NewGuid();
        SetupSearch(ListRow(id, "FAC-2026-000123"));
        SetupDetail(id, "FAC-2026-000123");

        var result = await ExecuteAsync(new Dictionary<string, object?> { ["invoice_id"] = "facture FAC-2026-000123" });

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal("number", doc.RootElement.GetProperty("resolvedBy").GetString());
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetInvoicesQuery>(q => q.SearchTerm == "FAC-2026-000123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Free_Text_Invoice_Id_Falls_Back_To_Latest()
    {
        SetupSearch(ListRow(LatestInvoiceId));
        SetupDetail(LatestInvoiceId);

        var result = await ExecuteAsync(new Dictionary<string, object?> { ["invoice_id"] = "ma dernière facture" });

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        Assert.Equal("latest", doc.RootElement.GetProperty("resolvedBy").GetString());
    }

    [Fact]
    public async Task Empty_Tenant_Returns_Friendly_French_Error_Without_Guid_Word()
    {
        SetupSearch(); // aucune facture

        var result = await ExecuteAsync(new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal("Aucune facture trouvée pour cette entreprise.", result.ErrorMessage);
        Assert.DoesNotContain("GUID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_Number_Returns_Friendly_Error_With_Number()
    {
        SetupSearch(); // rien pour ce numéro

        var result = await ExecuteAsync(new Dictionary<string, object?> { ["invoice_number"] = "FAC-2026-999999" });

        Assert.False(result.Success);
        Assert.Contains("FAC-2026-999999", result.ErrorMessage);
        Assert.DoesNotContain("GUID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
