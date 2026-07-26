using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Accounting;

/// <summary>
/// Superpose les personnalisations du comptable aux notes annexes détaillées générées par le
/// catalogue : titre de remplacement, texte narratif, masquage.
/// <para>
/// PUR et NEUTRE PAR CONSTRUCTION : sans override, la liste est renvoyée à l'identique (mêmes
/// instances). C'est la garantie de non-régression de la liasse NCT.
/// </para>
/// </summary>
public static class NctNoteOverrideApplier
{
    public static IReadOnlyList<NctDetailedNoteDto> Apply(
        IReadOnlyList<NctDetailedNoteDto> notes,
        IReadOnlyList<NctNoteOverride> overrides)
    {
        if (notes.Count == 0 || overrides is null || overrides.Count == 0)
            return notes;

        // Dernière personnalisation gagnante si un doublon échappait à l'index unique (défensif).
        var byNumber = new Dictionary<int, NctNoteOverride>();
        foreach (var o in overrides)
            byNumber[o.NoteNumber] = o;

        var result = new List<NctDetailedNoteDto>(notes.Count);
        foreach (var note in notes)
        {
            if (!byNumber.TryGetValue(note.Number, out var ov))
            {
                result.Add(note);
                continue;
            }

            // Une note masquée disparaît de la liasse ET, par voie de conséquence, de l'export PDF
            // (l'auto-sélection et le filtre d'export partent de cette même liste).
            if (ov.IsHidden)
                continue;

            result.Add(note with
            {
                Title = string.IsNullOrWhiteSpace(ov.CustomTitle) ? note.Title : ov.CustomTitle,
                Description = ov.CustomDescription
            });
        }

        return result;
    }
}
