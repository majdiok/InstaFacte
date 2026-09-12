namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Codes d'erreur Studio stables (contrat API) qui ne rentrent pas dans les familles génériques de
/// <see cref="Domain.Common.Error"/> (<c>Conflict</c>, <c>Validation.*</c>, <c>*.NotFound</c>).
/// Chaque code est mappé explicitement vers un statut HTTP par <c>StudioErrorMapping</c> côté API.
/// </summary>
public static class StudioErrorCodes
{
    /// <summary>
    /// Une table de jonction (<see cref="Domain.Enums.CustomEntityKind.Junction"/>) contient déjà un
    /// enregistrement actif liant la même paire (source, cible) — HTTP 409.
    /// </summary>
    public const string RecordDuplicateLink = "record.duplicate_link";
}
