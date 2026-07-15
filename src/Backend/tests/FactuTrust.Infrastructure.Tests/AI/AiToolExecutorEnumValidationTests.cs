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
/// Lot 7 — validation stricte de group_by : une valeur explicitement invalide remonte une erreur exploitable
/// par le modèle (sans requêter la BD), tandis qu'un argument omis conserve le défaut (Product).
/// </summary>
public sealed class AiToolExecutorEnumValidationTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<ILogger<AiToolExecutor>> _loggerMock = new();
    private readonly AiToolExecutor _executor;

    public AiToolExecutorEnumValidationTests()
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

    private void SetupRevenueSuccess()
    {
        IReadOnlyList<SalesRevenueReportRowDto> rows = new List<SalesRevenueReportRowDto>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetSalesRevenueByProductReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SalesRevenueReportRowDto>>.Success(rows));
    }

    private static Dictionary<string, object?> Args(string? groupBy)
    {
        var d = new Dictionary<string, object?>
        {
            ["from_date"] = "2026-01-01",
            ["to_date"] = "2026-06-03"
        };
        if (groupBy is not null)
            d["group_by"] = groupBy;
        return d;
    }

    [Fact]
    public async Task Invalid_GroupBy_Returns_Error_And_Does_Not_Query()
    {
        var result = await _executor.ExecuteAsync(
            "get_sales_revenue", Args("Clientt"), AiToolExecutionContext.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("group_by", result.ErrorMessage);
        Assert.Contains("Client", result.ErrorMessage);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetSalesRevenueByProductReportQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Omitted_GroupBy_Defaults_To_Product()
    {
        SetupRevenueSuccess();

        var result = await _executor.ExecuteAsync(
            "get_sales_revenue", Args(null), AiToolExecutionContext.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<GetSalesRevenueByProductReportQuery>(q => q.GroupBy == SalesRevenueGroupBy.Product),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Valid_GroupBy_Client_LowerCase_Passes_Through()
    {
        SetupRevenueSuccess();

        var result = await _executor.ExecuteAsync(
            "get_sales_revenue", Args("client"), AiToolExecutionContext.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<GetSalesRevenueByProductReportQuery>(q => q.GroupBy == SalesRevenueGroupBy.Client),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
