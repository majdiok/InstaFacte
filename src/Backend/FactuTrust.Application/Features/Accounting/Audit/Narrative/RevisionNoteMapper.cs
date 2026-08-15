using System.Text.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.AccountingAudit;

namespace FactuTrust.Application.Features.Accounting.Audit.Narrative;

/// <summary>
/// Passage entre la note de révision persistée et son DTO.
///
/// <para>Les entrées de la note vivent en JSON dans une colonne, et non dans une table : elles sont
/// toujours lues en bloc avec leur note, jamais interrogées ni triées individuellement. Une table
/// aurait ajouté une jointure et une migration sans rien apporter.</para>
///
/// <para>La lecture est <b>tolérante</b> : un JSON illisible rend une note sans entrées plutôt que
/// de faire tomber l'écran. Le contenu a pu être écrit par une version antérieure du schéma, et une
/// note dégradée reste plus utile qu'une erreur.</para>
/// </summary>
public static class RevisionNoteMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static FirmRevisionNoteDto ToDto(AccountingRevisionNote note) => new()
    {
        Id = note.Id,
        RunId = note.RunId,
        FiscalYear = note.FiscalYear,
        GeneratedAt = note.GeneratedAt,
        GeneratedByUserName = note.GeneratedByUserName,
        AiGenerated = note.AiGenerated,
        ModelRef = note.ModelRef,
        FallbackReason = note.FallbackReason,
        ExecutiveSummary = note.ExecutiveSummary,
        TotalImpactAmount = note.TotalImpactAmount,
        AnomalyCount = note.AnomalyCount,
        BlockingCount = note.BlockingCount,
        Items = DeserializeItems(note.ItemsJson)
    };

    public static string SerializeItems(IReadOnlyList<FirmRevisionNoteItemDto> items) =>
        JsonSerializer.Serialize(items, SerializerOptions);

    private static IReadOnlyList<FirmRevisionNoteItemDto> DeserializeItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<FirmRevisionNoteItemDto>();

        try
        {
            return JsonSerializer.Deserialize<List<FirmRevisionNoteItemDto>>(json, SerializerOptions)
                   ?? (IReadOnlyList<FirmRevisionNoteItemDto>)Array.Empty<FirmRevisionNoteItemDto>();
        }
        catch (JsonException)
        {
            // Note écrite par un schéma antérieur, ou colonne corrompue : on rend l'en-tête de la
            // note, qui reste exploitable, plutôt que de faire échouer toute la lecture du dossier.
            return Array.Empty<FirmRevisionNoteItemDto>();
        }
    }
}
