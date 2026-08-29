namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Per-request revocation check for access tokens (plan §6 Phase 2.5): compares the token's <c>sstamp</c>
/// claim (a snapshot of <c>ApplicationUser.SecurityStamp</c> at issuance) against the current master DB
/// state, and rejects tokens for deactivated users. The repo has no Redis / <c>IDistributedCache</c> —
/// implementations soften the per-request master DB lookup with a short-TTL (&lt;= 5s) memory cache.
/// On the node that performs a stamp-rotating mutation, <see cref="Invalidate"/> makes the very next
/// check immediate instead of waiting out the TTL; on every OTHER node the ≤5s bound alone still
/// applies (no cross-node invalidation bus needed there).
/// Callers MUST treat any exception thrown by <see cref="IsValidAsync"/> as "invalid" (fail-closed) —
/// implementations do not swallow errors into a permissive default.
/// </summary>
public interface ISecurityStampTokenValidator
{
    /// <param name="userId">Resolved from the token's <c>ClaimTypes.NameIdentifier</c>.</param>
    /// <param name="tokenSecurityStamp">Value of the token's <c>sstamp</c> claim, or <c>null</c> if absent.</param>
    /// <param name="requireSecurityStampClaim">
    /// Two-stage rollout flag (<c>JwtSettings:RequireSecurityStampClaim</c>). When <c>false</c> (stage 1),
    /// a token without the claim is still accepted (tokens issued before this feature was deployed).
    /// When <c>true</c> (stage 2), a token without the claim is rejected.
    /// </param>
    Task<bool> IsValidAsync(
        Guid userId,
        string? tokenSecurityStamp,
        bool requireSecurityStampClaim,
        CancellationToken cancellationToken);

    /// <summary>
    /// Proactively evicts <paramref name="userId"/>'s cached stamp snapshot. Callers that just
    /// rotated <c>ApplicationUser.SecurityStamp</c> (plan §6 Phase 2.5) MUST call this AFTER their
    /// mutation transaction has committed, so the very next request on the SAME node re-reads the
    /// master DB instead of serving a pre-mutation cache entry for up to the 5s TTL — this is what
    /// makes revocation immediate on the mutating node rather than merely bounded by the TTL
    /// everywhere (other nodes still fall back to the ≤5s bound, which needs no invalidation bus).
    /// </summary>
    void Invalidate(Guid userId);
}
