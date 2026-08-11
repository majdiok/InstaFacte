using FactuTrust.Application.Common.Interfaces;
using System.Reflection;
using FactuTrust.Application.Authorization;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Authorization;

/// <summary>
/// Blocks firm-exclusive payroll commands for company users when a cabinet assignment is active.
/// </summary>
public sealed class PayrollFirmOperationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IPayrollFirmOperationGuard _guard;

    public PayrollFirmOperationBehavior(IPayrollFirmOperationGuard guard)
    {
        _guard = guard;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(TRequest)))
            return await next();

        if (await _guard.CanExecuteFirmExclusiveOperationsAsync(cancellationToken))
            return await next();

        return CreateFailureResponse();
    }

    private static TResponse CreateFailureResponse()
    {
        var error = PayrollOperationsAccess.WriteDenied();
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failure = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition);
            return (TResponse)failure.MakeGenericMethod(valueType).Invoke(null, [error])!;
        }

        throw new UnauthorizedAccessException(error.Description);
    }
}
