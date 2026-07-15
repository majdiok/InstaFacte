using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// get_sales_revenue doit renvoyer une enveloppe { totalRevenue, rowCount, currency, groupBy, rows }
/// où le total est calculé sur le jeu COMPLET (avant top_n). C'est ce qui empêche le petit modèle de
/// confondre la 1re ligne (la plus grosse) avec le total — la cause du bug « total = 1498 ».
/// </summary>
public sealed class AiToolExecutorSalesRevenueEnvelopeTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<ILogger<AiToolExecutor>> _loggerMock = new();
    private readonly AiToolExecutor _executor;

    public AiToolExecutorSalesRevenueEnvelopeTests()
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

    private void SetupRevenue(params decimal[] revenues)
    {
        IReadOnlyList<SalesRevenueReportRowDto> rows = revenues
            .Select((r, i) => new SalesRevenueReportRowDto
            {
                GroupKey = $"Produit {i + 1}",
                Revenue = r,
                Quantity = 1,
                Currency = "TND"
            })
            .ToList();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetSalesRevenueByProductReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SalesRevenueReportRowDto>>.Success(rows));
    }

    [Fact]
    public async Task Returns_Envelope_With_Deterministic_Total()
    {
        // Lignes inspirées de la copie d'écran (TTC) : portable 1498, bureau 476, lit 169,50.
        SetupRevenue(1498m, 476m, 169.5m);

        var result = await _executor.ExecuteAsync(
            "get_sales_revenue",
            new Dictionary<string, object?> { ["preset"] = "today" },
            AiToolExecutionContext.Empty,
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        var root = doc.RootElement;

        Assert.Equal(2143.5m, root.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(3, root.GetProperty("rowCount").GetInt32());
        Assert.Equal("TND", root.GetProperty("currency").GetString());
        Assert.Equal("Product", root.GetProperty("groupBy").GetString());
        Assert.Equal(3, root.GetProperty("rows").GetArrayLength());

        // Période réellement interrogée (citée par le modèle et le repli déterministe).
        var period = root.GetProperty("period");
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", period.GetProperty("from").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", period.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Total_Is_Computed_Over_Full_Set_Not_Limited_Rows()
    {
        SetupRevenue(200m, 100m, 50m, 25m, 10m);

        var result = await _executor.ExecuteAsync(
            "get_sales_revenue",
            new Dictionary<string, object?> { ["preset"] = "today", ["top_n"] = 2 },
            AiToolExecutionContext.Empty,
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        var root = doc.RootElement;

        // top_n=2 borne la liste affichée à 2, mais le total reste la somme des 5 lignes.
        Assert.Equal(2, root.GetProperty("rows").GetArrayLength());
        Assert.Equal(5, root.GetProperty("rowCount").GetInt32());
        Assert.Equal(385m, root.GetProperty("totalRevenue").GetDecimal());
    }

    [Fact]
    public async Task Empty_Result_Yields_Zero_Total_And_Empty_Rows()
    {
        SetupRevenue();

        var result = await _executor.ExecuteAsync(
            "get_sales_revenue",
            new Dictionary<string, object?> { ["preset"] = "today" },
            AiToolExecutionContext.Empty,
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var doc = JsonDocument.Parse(result.Data!);
        var root = doc.RootElement;

        Assert.Equal(0m, root.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(0, root.GetProperty("rowCount").GetInt32());
        Assert.Equal("TND", root.GetProperty("currency").GetString());
        Assert.Equal(0, root.GetProperty("rows").GetArrayLength());
    }
}
