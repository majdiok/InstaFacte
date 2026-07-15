using System.Text.Json.Nodes;
using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Executes one Studio ERP-bridge automation against one record: resolves the action tool, maps the
/// record's fields to its arguments, runs it via the permission-gated tool executor, and writes a run
/// log entry (success/failure + result). Never throws — always returns a recorded run.
/// </summary>
public interface IStudioBridgeExecutor
{
    Task<CustomAutomationRun> ExecuteAsync(
        CustomEntityAutomation automation, Guid tenantId, Guid recordId, JsonObject? recordData,
        Guid? runBy, CancellationToken cancellationToken = default);
}
