using FactuTrust.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace FactuTrust.API.Authorization;

/// <summary>
/// Requires permission to execute firm-exclusive payroll operations when the company
/// has an active cabinet assignment.
/// </summary>
public sealed class PayrollFirmOperationRequirement : IAuthorizationRequirement;
