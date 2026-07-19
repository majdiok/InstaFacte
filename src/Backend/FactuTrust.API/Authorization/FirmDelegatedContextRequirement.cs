using Microsoft.AspNetCore.Authorization;

namespace FactuTrust.API.Authorization;

/// <summary>
/// Requires an accounting firm user operating in delegated client dossier context.
/// </summary>
public sealed class FirmDelegatedContextRequirement : IAuthorizationRequirement;
