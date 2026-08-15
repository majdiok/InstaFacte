using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>
/// Dossier de révision d'un run de contrôle : la mise en forme « note de travail » des anomalies
/// déjà détectées.
///
/// <para>La note n'ajoute aucune détection. Toutes les valeurs chiffrées qu'elle porte
/// (<see cref="TotalImpactAmount"/>, compteurs) sont recalculées depuis les anomalies du run, jamais
/// produites par le modèle de langage : celui-ci ne rédige que de la prose et choisit une action
/// dans un ensemble fermé.</para>
///
/// <para><see cref="AiGenerated"/> à faux signifie que la note est purement déterministe — repli
/// normal en cas de modèle indisponible, de délai dépassé ou de réponse illisible. Une note
/// déterministe reste complète et exploitable.</para>
/// </summary>
public sealed class AccountingRevisionNote : Entity
{
    public Guid RunId { get; set; }
    public AccountingControlRun Run { get; set; } = null!;

    /// <summary>Dupliqués depuis le run pour interroger la note sans jointure.</summary>
    public int FiscalYear { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }

    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public string? GeneratedByUserName { get; set; }

    /// <summary>Vrai si la rédaction provient du modèle ; faux si repli déterministe.</summary>
    public bool AiGenerated { get; set; }

    /// <summary>Référence du modèle utilisé (« ollama:qwen2.5:7b-instruct »…), pour la piste d'audit.</summary>
    public string? ModelRef { get; set; }

    /// <summary>Renseigné quand la génération a échoué et que la note est retombée au déterministe.</summary>
    public string? FallbackReason { get; set; }

    public string ExecutiveSummary { get; set; } = string.Empty;

    /// <summary>
    /// Notes de travail par anomalie, sérialisées. Chaque entrée porte la référence de l'anomalie,
    /// la rédaction, la question client éventuelle et l'action retenue.
    /// </summary>
    public string ItemsJson { get; set; } = "[]";

    /// <summary>
    /// Impact chiffré agrégé, en dinars. Somme des montants des seules anomalies dont la règle
    /// produit un montant signifiant (cf. <c>AuditAmountSemantics</c>) — additionner les autres
    /// donnerait un total faux.
    /// </summary>
    public decimal TotalImpactAmount { get; set; }

    public int AnomalyCount { get; set; }
    public int BlockingCount { get; set; }
}
