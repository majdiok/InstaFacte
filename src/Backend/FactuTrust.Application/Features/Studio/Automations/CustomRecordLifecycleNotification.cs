using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Automations;

/// <summary>
/// Raised (after persistence) when a custom record is created or updated, so ERP-bridge automations
/// can run best-effort. Carries the canonical record JSON so handlers need no extra load.
/// </summary>
public sealed record CustomRecordLifecycleNotification(
    Guid TenantId,
    Guid EntityDefinitionId,
    Guid RecordId,
    string DataJson,
    StudioAutomationTrigger Trigger,
    Guid? RunBy) : INotification;
