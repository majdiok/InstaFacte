using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Personnalisations des notes annexes NCT, par exercice. Sans ligne, la liasse conserve les
/// libellés du catalogue — la superposition est neutre.
/// </summary>
public interface INctNoteOverrideRepository
{
    Task<IReadOnlyList<NctNoteOverride>> GetByYearAsync(int fiscalYear, CancellationToken ct = default);

    /// <summary>Crée ou met à jour la personnalisation d'une note (clé métier : exercice + numéro).</summary>
    Task<Result<NctNoteOverride>> UpsertAsync(
        int fiscalYear, int noteNumber, string? customTitle, string? customDescription, bool isHidden,
        CancellationToken ct = default);

    /// <summary>Supprime la personnalisation (« Rétablir ») ; sans effet si elle n'existe pas.</summary>
    Task DeleteAsync(int fiscalYear, int noteNumber, CancellationToken ct = default);
}
