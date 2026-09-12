namespace FactuTrust.Domain.Enums;

/// <summary>
/// Mode de rendu d'une vue enregistrée d'une table Studio (<c>CustomRecordViewDefinition</c>).
/// Persisté en <c>int</c> (colonne <c>Mode</c>, défaut 0) ; sérialisé en chaîne dans l'API
/// (<c>"List"</c>, <c>"Kanban"</c>, <c>"Calendar"</c>). Valeurs append-only.
/// </summary>
public enum CustomRecordViewMode
{
    /// <summary>Liste paginée : colonnes, filtres, tri, recherche.</summary>
    List = 0,

    /// <summary>Tableau kanban groupé par un champ <c>Select</c>.</summary>
    Kanban = 1,

    /// <summary>Calendrier borné par une fenêtre de dates sur un champ <c>Date</c> / <c>DateTime</c>.</summary>
    Calendar = 2
}
