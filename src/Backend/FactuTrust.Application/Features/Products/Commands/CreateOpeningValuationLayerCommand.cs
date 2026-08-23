using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Products.Commands;

public sealed record CreateOpeningValuationLayerCommand(
    Guid ProductId,
    CostingMethod CostingMethod) : IRequest<Result>;

public sealed class CreateOpeningValuationLayerCommandHandler
    : IRequestHandler<CreateOpeningValuationLayerCommand, Result>
{
    private readonly IStockMutationService _mutation;
    private readonly StockTraceabilityOptions _options;

    public CreateOpeningValuationLayerCommandHandler(
        IStockMutationService mutation,
        IOptions<StockTraceabilityOptions> options)
    {
        _mutation = mutation;
        _options = options.Value;
    }

    public Task<Result> Handle(CreateOpeningValuationLayerCommand request, CancellationToken cancellationToken)
    {
        if (!_options.FifoLifoValuationEnabled)
        {
            return Task.FromResult(Result.Failure(Error.Validation(
                "CostingMethod",
                "La valorisation FIFO/LIFO n'est pas activée pour ce tenant.")));
        }

        return _mutation.CreateOpeningValuationLayersAsync(
            request.ProductId,
            request.CostingMethod,
            cancellationToken);
    }
}
