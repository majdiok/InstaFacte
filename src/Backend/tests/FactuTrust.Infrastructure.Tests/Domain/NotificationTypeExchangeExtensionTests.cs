using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class NotificationTypeExchangeExtensionTests
{
    [Fact]
    public void Existing_assignment_notification_values_unchanged()
    {
        Assert.Equal(0, (int)NotificationType.FirmAssignmentRequested);
        Assert.Equal(1, (int)NotificationType.FirmAssignmentAccepted);
        Assert.Equal(2, (int)NotificationType.FirmAssignmentRejected);
        Assert.Equal(3, (int)NotificationType.FirmAssignmentRevoked);
        Assert.Equal(4, (int)NotificationType.FirmAssignmentCancelled);
    }

    [Fact]
    public void Exchange_notification_types_appended_after_assignment_types()
    {
        Assert.Equal(5, (int)NotificationType.ExchangeMessageReceived);
        Assert.Equal(6, (int)NotificationType.ExchangeRequestCreated);
        Assert.Equal(7, (int)NotificationType.ExchangeRequestStatusChanged);
        Assert.Equal(8, (int)NotificationType.ExchangeTaskAssigned);
        Assert.Equal(9, (int)NotificationType.ExchangeThreadClosed);
        Assert.Equal(10, (int)NotificationType.ExchangeRequestCommented);
        Assert.Equal(11, (int)NotificationType.ExchangeDocumentShared);
        Assert.Equal(12, (int)NotificationType.ExchangeTaskCreated);
    }

    [Fact]
    public void Firm_governance_notification_types_appended_after_exchange_types()
    {
        Assert.Equal(13, (int)NotificationType.FirmLeaveRequestSubmitted);
        Assert.Equal(14, (int)NotificationType.FirmTimeSheetSubmitted);
    }
}
