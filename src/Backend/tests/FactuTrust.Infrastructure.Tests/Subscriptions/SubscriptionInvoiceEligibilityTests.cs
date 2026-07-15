using System.Reflection;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Subscriptions;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Subscriptions;

public sealed class SubscriptionInvoiceEligibilityTests
{
    private static void SetInvoicesThisMonth(Subscription sub, int count)
    {
        typeof(Subscription)
            .GetProperty(nameof(Subscription.InvoicesThisMonth))!
            .SetValue(sub, count);
    }

    private static void SetStatus(Subscription sub, SubscriptionStatus status)
    {
        typeof(Subscription)
            .GetProperty(nameof(Subscription.Status))!
            .SetValue(sub, status);
    }

    [Fact]
    public void Evaluate_MonthlyActive_Unlimited_Allows()
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        SetStatus(sub, SubscriptionStatus.Active);
        SetInvoicesThisMonth(sub, 999);

        var result = SubscriptionInvoiceEligibility.Evaluate(sub, int.MaxValue);

        Assert.True(result.CanCreateInvoice);
        Assert.Null(result.BlockCode);
    }

    [Fact]
    public void Evaluate_MonthlySuspended_BlocksWithCode()
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        sub.Suspend("Test");

        var result = SubscriptionInvoiceEligibility.Evaluate(sub, int.MaxValue);

        Assert.False(result.CanCreateInvoice);
        Assert.Equal(SubscriptionInvoiceEligibility.SubscriptionSuspendedCode, result.BlockCode);
    }

    [Fact]
    public void Evaluate_FreeAtQuota_BlocksWithQuotaCode()
    {
        var sub = Subscription.CreateFree(Guid.NewGuid());
        SetInvoicesThisMonth(sub, SubscriptionLimits.Free.MaxInvoicesPerMonth);

        var result = SubscriptionInvoiceEligibility.Evaluate(
            sub,
            SubscriptionLimits.Free.MaxInvoicesPerMonth);

        Assert.False(result.CanCreateInvoice);
        Assert.Equal(SubscriptionInvoiceEligibility.QuotaExceededCode, result.BlockCode);
        Assert.Contains("10/10", result.BlockMessage);
    }

    [Fact]
    public void Evaluate_Trial_AllowsUnderQuota()
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        SetInvoicesThisMonth(sub, 5);

        var result = SubscriptionInvoiceEligibility.Evaluate(sub, int.MaxValue);

        Assert.True(result.CanCreateInvoice);
    }

    [Theory]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionInvoiceEligibility.SubscriptionPastDueCode)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionInvoiceEligibility.SubscriptionExpiredCode)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionInvoiceEligibility.SubscriptionCancelledCode)]
    public void Evaluate_BlockedStatuses_ReturnExpectedCode(SubscriptionStatus status, string expectedCode)
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        if (status == SubscriptionStatus.Cancelled)
            sub.Cancel("Test reason for cancel");
        else if (status == SubscriptionStatus.Suspended)
            sub.Suspend("Test");
        else
            SetStatus(sub, status);

        var result = SubscriptionInvoiceEligibility.Evaluate(sub, int.MaxValue);

        Assert.False(result.CanCreateInvoice);
        Assert.Equal(expectedCode, result.BlockCode);
    }

    [Fact]
    public void EvaluateForDisplay_MonthlyActive_SetsCanCreateOnDto()
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        SetStatus(sub, SubscriptionStatus.Active);

        var dto = SubscriptionDtoMapper.ToDto(sub);

        Assert.True(dto.CanCreateInvoice);
        Assert.Null(dto.InvoiceBlockCode);
    }

    [Fact]
    public void EvaluateForDisplay_MonthlySuspended_SetsBlockOnDto()
    {
        var sub = Subscription.CreateTrial(Guid.NewGuid());
        sub.Suspend("Payment overdue");

        var dto = SubscriptionDtoMapper.ToDto(sub);

        Assert.False(dto.CanCreateInvoice);
        Assert.Equal(SubscriptionInvoiceEligibility.SubscriptionSuspendedCode, dto.InvoiceBlockCode);
        Assert.Contains("suspendu", dto.InvoiceBlockMessage, StringComparison.OrdinalIgnoreCase);
    }
}
